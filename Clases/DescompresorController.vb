Imports System.ComponentModel
Imports SharpCompress.Archives
Imports SharpCompress.Archives.IArchiveEntryExtensions
Imports SharpCompress.Readers
Imports SharpCompress.Archives.Rar
Imports SharpCompress.Archives.Zip
Imports SharpCompress.Readers.Rar
Imports SharpCompress.Common
Imports System.IO


Public Class DescompresorController

    Public Class QueueItem
        Public Path As String
        Public Password As String
        Public CreateDirectory As Boolean
    End Class

#Region "Región Shared"
    Friend Shared Mutex As New Object()

    Private Shared _Controller As DescompresorController
    Public Shared Function GetController() As DescompresorController
        SyncLock Mutex
            If _Controller Is Nothing Then
                _Controller = New DescompresorController
            End If
        End SyncLock
        Return _Controller
    End Function

#End Region

#Region "Región privada"

    ''' <summary>
    ''' Cola de rutas de elementos a descomprimir (pj "C:\temp\a.rar", "C:\temp\b.rar", etc))
    ''' </summary>
    ''' <remarks>La clave es el código DEN, el valor indica la ruta y si ha de crear directorio o no</remarks>
    Private _colaElementos As Generic.Dictionary(Of String, QueueItem)
    Private _codigoElementoActual As String
    Private _pathElementoActual As String
    Private _passwordElementoActual As String
    Private _crearDirectorio As Boolean
    Private _cancelRequested As Boolean

    Private _ExtensionesSoportadas As Generic.List(Of String)


    ' En estas variables (_TamanoTotalExtraido, etc) se guardará el progreso actual

    Friend _TamanoTotal As System.Nullable(Of Long) ' Tamaño total de los ficheros cuando se descompriman
    Friend _TamanoTotalExtraido As System.Nullable(Of Long) ' Tamaño total de los ficheros ya descomprimidos completamente
    Friend _FicActTamanoTotal As System.Nullable(Of Long) ' Tamaño total del fichero que se está descomprimiendo
    Friend _FicActExtraido As System.Nullable(Of Long) ' Bytes extraidos del fichero que se está descomprimiendo
    Friend _FicActNombre As String ' Nombre del fichero que se está descomprimiendo



    Private Sub New()
        SyncLock Mutex
            _colaElementos = New Generic.Dictionary(Of String, QueueItem)()
            _codigoElementoActual = Nothing
            _pathElementoActual = Nothing
            _passwordElementoActual = Nothing
            _ExtensionesSoportadas = New Generic.List(Of String)
            With _ExtensionesSoportadas
                .Add("7z")
                .Add("rar")
                .Add("tar")
                .Add("zip")
            End With
        End SyncLock
    End Sub

    Public Sub RequestCancel()
        SyncLock Mutex
            _cancelRequested = True
        End SyncLock
    End Sub

    Private Function IsCancelRequested() As Boolean
        SyncLock Mutex
            Return _cancelRequested
        End SyncLock
    End Function

    Private Function PonerElementoAProcesar() As Boolean
        SyncLock Mutex
            If Not String.IsNullOrEmpty(_pathElementoActual) Or _colaElementos.Count = 0 Then
                Return False ' Ya hay un elemento procesando o la cola está vacía
            Else
                _codigoElementoActual = _colaElementos.Keys(0)
                _crearDirectorio = _colaElementos(_codigoElementoActual).CreateDirectory
                _pathElementoActual = _colaElementos(_codigoElementoActual).Path
                _passwordElementoActual = _colaElementos(_codigoElementoActual).Password
                _colaElementos.Remove(_codigoElementoActual)
                Return True
            End If
        End SyncLock
    End Function

    ''' <summary>
    ''' P1-2:处理单个队列元素。异常在此被隔离 —— 单个坏元素不得带走整个 worker。
    ''' 此前 ProcesarElemento 的异常一路冒泡到 DescompresorController_DoWork 的
    ''' Try/Catch,Catch 只写日志就结束 DoWork → worker 静默死亡且永不重启,
    ''' 整个解压队列报废到重启进程。
    ''' </summary>
    Private Sub ProcesarElemento(ByRef Cancel As Boolean)
        Dim codigo As String = Nothing
        Try
            SyncLock Mutex
                codigo = _codigoElementoActual
            End SyncLock
            ProcesarElementoInterno(Cancel)
        Catch ex As Exception
            Log.WriteError("Decompressor: unexpected error while extracting '" & codigo & "': " & Log.SafeException(ex))
            ' 只有当内部流程尚未走到收尾(状态仍被占用)时才补报失败,
            ' 否则会与正常收尾路径的 DescompresionFinalizada 重复上报。
            DiscardCurrentElementOnError("Unexpected error while extracting: " & ex.Message)
        End Try
    End Sub

    ''' <summary>
    ''' P1-2:异常中止当前元素时清理残留状态并上报失败。
    ''' _pathElementoActual 残留会让 Ocupado() 恒为 True(每次关闭都弹
    ''' "Files extracting, corruption danger"),元素也永远卡在 Descomprimiendo。
    ''' 若内部已走到正常收尾(状态已清空),则不重复上报失败事件。
    ''' </summary>
    Friend Function DiscardCurrentElementOnError(ByVal motivo As String) As String
        Dim codigo As String = Nothing
        SyncLock Mutex
            codigo = _codigoElementoActual
            Me._pathElementoActual = Nothing
            Me._passwordElementoActual = Nothing
            Me._codigoElementoActual = Nothing
        End SyncLock
        If Not String.IsNullOrEmpty(codigo) Then
            Try
                RaiseEvent DescompresionFinalizada(codigo, False, motivo)
            Catch ex As Exception
                Log.WriteError("Decompressor: failed to report failure for '" & codigo & "': " & Log.SafeException(ex))
            End Try
        End If
        Return codigo
    End Function

    Private Sub ProcesarElementoInterno(ByRef Cancel As Boolean)
        ' P1-2:收尾必须保证执行。此前是"先 RaiseEvent 再清状态"的直线代码,
        ' 若事件处理器抛异常(RaiseEvent 会把处理器异常抛回调用方),
        ' 状态就永远不会被清 → _pathElementoActual 残留 → Ocupado() 恒 True,
        ' 且该元素卡在 Descomprimiendo 直到重启。
        ' 现在用 Finally 兜底清理,保证任何退出路径都释放"当前元素"槽位。
        Dim elementHandled As Boolean = False
        Try
            ProcesarElementoInternoCore(Cancel, elementHandled)
        Finally
            If Not elementHandled Then
                ' 未走到正常收尾(抛异常)→ 释放"当前元素"占用槽位。
                ' 注意:_codigoElementoActual 必须保留 —— 外层 ProcesarElemento 的 Catch
                ' 靠它补报 DescompresionFinalizada 失败;此处一并清掉会让该元素
                ' 永远卡在"解压中"(Fichero 侧只靠这个事件离开该状态)。
                SyncLock Mutex
                    Me._pathElementoActual = Nothing
                    Me._passwordElementoActual = Nothing
                End SyncLock
            End If
        End Try
    End Sub

    Private Sub ProcesarElementoInternoCore(ByRef Cancel As Boolean, ByRef elementHandled As Boolean)
        SyncLock Mutex
            If String.IsNullOrEmpty(Me._pathElementoActual) Then
                ' 空转:没有元素可处理。此路径不占用任何状态,标记为已处理,
                ' 避免 Finally 去动 _codigoElementoActual(那会把上一轮的 code 留下)。
                elementHandled = True
                Exit Sub
            End If
        End SyncLock

        Log.WriteInfo("Extracting '" & _codigoElementoActual & "'")
        Dim Sw As New System.Diagnostics.Stopwatch
        Sw.Start()

        Dim Fichero As String = ""
        Dim Directorio As String = ""
        Dim FicheroSinExtension As String = ""

        If Not ObtenerNombres(_pathElementoActual, Directorio, Fichero, FicheroSinExtension, False, 0) Then
            ' Elemento inválido
            ' P1-2: raise the completion event with a failure. This event is the ONLY exit
            ' from the Descomprimiendo state (Fichero.DescompresionFinalizada), so exiting
            ' without it left the item stuck "extracting" forever — visible in the list and
            ' only cleared by a restart (Paquete.MarcarFicherosComoParados).
            RaiseEvent DescompresionFinalizada(_codigoElementoActual, False, _
                "Invalid archive path (file not found): '" & _pathElementoActual & "'")

            Dim codigoDescartado As String = _codigoElementoActual
            SyncLock Mutex
                Me._pathElementoActual = Nothing
                Me._passwordElementoActual = Nothing
                ' R-P1-2 follow-up: clear the code as well, matching the normal exit below.
                ' Leaving it set meant EleActual_Codigo kept reporting an already-discarded
                ' element while _pathElementoActual was Nothing (an invariant no other exit
                ' path produces). Captured into a local first because the log line follows.
                Me._codigoElementoActual = Nothing
            End SyncLock

            Log.WriteWarning("Decompressor: invalid element, discarding: '" & codigoDescartado & "'")

            ' P1-2:本路径已上报失败并清空状态 → 标记为已处理,Finally 不再重复清理/上报。
            elementHandled = True
            Exit Sub
        End If

        ' Multipart 7z volumes (.7z.001): ObtenerNombres strips only the numeric
        ' suffix, leaving "name.7z" — drop the ".7z" so the extraction folder is
        ' named after the archive like every other format.
        If System.Text.RegularExpressions.Regex.IsMatch(_pathElementoActual, "\.7z\.\d+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase) _
           AndAlso FicheroSinExtension.ToLower.EndsWith(".7z") Then
            FicheroSinExtension = FicheroSinExtension.Substring(0, FicheroSinExtension.Length - 4)
        End If

        Dim DirectorioExtraccion As String = Directorio
        If _crearDirectorio Then
            Dim safeFolder As String = PathGuard.SanitizeFileName(FicheroSinExtension, "extracted")
            DirectorioExtraccion = PathGuard.GetSafePathUnderRoot(Directorio, safeFolder, allowRoot:=False)
        End If
        Directory.CreateDirectory(DirectorioExtraccion)

        Dim desc As New Descompressor(_pathElementoActual, DirectorioExtraccion, _passwordElementoActual)
        Dim extractOk As Boolean = False

        Dim extractThread As New Threading.Thread(AddressOf desc.Extract)
        extractThread.Priority = Threading.ThreadPriority.BelowNormal
        extractThread.IsBackground = True
        extractThread.Start()
        While Not extractThread.Join(500)
            If Cancel OrElse IsCancelRequested() Then
                ' Cooperative cancel only — do not Thread.Abort (unsafe mid-write)
                desc.CancelRequested = True
                Log.WriteWarning("Decompressor: cancel requested for '" & _codigoElementoActual & "'; waiting for worker to stop.")
                ' P1-3: wait until the worker has ACTUALLY stopped. A single Join(5000)
                ' gave up while the worker could still be mid-entry, so files kept
                ' appearing after "cancel" and the next queued item was extracted in
                ' parallel with the cancelled one. The worker checks CancelRequested at
                ' every entry boundary, so it normally stops within a second.
                ' R-P1-3 follow-up: the cap was 60s, but this branch is reachable ONLY at
                ' shutdown — RequestCancel() (Main.vb:740) and bgwDescompresor.CancelAsync()
                ' (Main.vb:738) have no other caller. Main's own budget is TIMEOUT_CLOSE,
                ' which the shipped InternalConfig sets to 8s (the 15 in Main.vb:3434 is only
                ' the pre-TryParse fallback, so it never applies). A 60s cap therefore
                ' guaranteed that closing mid-extraction always burned the full timeout; an 8s
                ' cap merely tied it, and this loop starts after Main's clock, so it still lost.
                ' 5s leaves headroom for the rest of the shutdown sequence while covering ~5x
                ' the normal stop latency. A pathological stall (huge single entry / disk hang)
                ' is logged and left behind — bgwDescompresor and extractThread are both
                ' background threads, so the process still exits.
                Const cancelWaitCapMs As Integer = 5000
                Dim waitedMs As Integer = 0
                While extractThread.IsAlive AndAlso waitedMs < cancelWaitCapMs
                    extractThread.Join(500)
                    waitedMs += 500
                End While
                If extractThread.IsAlive Then
                    Log.WriteError("Decompressor: worker for '" & _codigoElementoActual & "' still running after " & (cancelWaitCapMs \ 1000) & "s of cancel; proceeding anyway.")
                End If
                Exit While
            End If
        End While

        Dim extractErrorMessage As String = ""
        If desc.Exception IsNot Nothing Then
            Log.WriteError("Decompressor: Error extracting '" & _codigoElementoActual & "': " & Log.SafeException(desc.Exception))
            extractOk = False
            extractErrorMessage = desc.Exception.Message
        ElseIf Cancel OrElse desc.CancelRequested OrElse IsCancelRequested() Then
            extractOk = False
            extractErrorMessage = "Extraction cancelled."
        Else
            extractOk = True
        End If
        Sw.Stop()

        If extractOk Then
            Log.WriteInfo("Element '" & _codigoElementoActual & "' extracted in " & Sw.ElapsedMilliseconds & "ms")
            ' P2-4: make the collision policy visible (no silent overwrites anymore).
            If desc.CollisionsKeptOriginal > 0 Then
                Log.WriteInfo("P2-4: " & desc.CollisionsKeptOriginal & " pre-existing file(s) in the extraction folder were kept under a ""(n)"" suffix instead of being overwritten.")
            End If
            If desc.OverwritesSameSize > 0 Then
                Log.WriteInfo("P2-4: " & desc.OverwritesSameSize & " file(s) from a previous extraction of this same archive were replaced (idempotent re-extraction).")
            End If
        Else
            Log.WriteWarning("Element '" & _codigoElementoActual & "' extraction failed or cancelled.")
        End If

        RaiseEvent DescompresionFinalizada(_codigoElementoActual, extractOk, extractErrorMessage)

        ' Hemos terminado
        SyncLock Mutex
            Me._pathElementoActual = Nothing
            Me._passwordElementoActual = Nothing
            Me._codigoElementoActual = Nothing
        End SyncLock

        ' P1-2:正常收尾完成(已上报 + 已清状态)→ Finally 不再重复处理。
        elementHandled = True


    End Sub



    Private Shared Function ObtenerNombres(ByVal Path As String, _
                                           ByRef Directorio As String, _
                                           ByRef Fichero As String, _
                                           ByRef FicheroSinExtension As String, _
                                           ByRef IsRARPart As Boolean, _
                                           ByRef RARPartLength As Integer) As Boolean
        Dim fi As New FileInfo(Path) ' C:\MyDirectory\MySubDirectory\MyFileName.txt
        If fi.Exists Then
            Fichero = fi.Name '  MyFileName.txt
            If Fichero.Contains(".") Then
                FicheroSinExtension = Fichero.Substring(0, Fichero.LastIndexOf("."c)) ' MyFileName
            Else ' No se debería dar...
                FicheroSinExtension = Fichero
            End If
            Directorio = fi.DirectoryName  ' C:\MyDirectory\MySubDirectory

            ' Caso fichero.part01.rar!!
            If Fichero.ToLower.EndsWith(".rar") And FicheroSinExtension.Contains(".") Then
                Dim fin As String = "" & FicheroSinExtension.Substring(FicheroSinExtension.LastIndexOf("."c) + 1)
                If fin.Length > 4 AndAlso fin.ToLower.Substring(0, 4) = "part" AndAlso IsNumeric(fin.ToLower.Substring(4)) Then
                    FicheroSinExtension = FicheroSinExtension.Substring(0, FicheroSinExtension.LastIndexOf("."c))
                    RARPartLength = fin.ToLower.Substring(4).Length
                    IsRARPart = True
                Else
                    IsRARPart = False
                End If
            Else
                IsRARPart = False
            End If

            Return True
        Else
            Return False
        End If
    End Function


#End Region

#Region "Región pública"

    Public Event DescompresionFinalizada(ByVal Code As String, ByVal Success As Boolean, ByVal ErrorMessage As String)

    ' Tamaño total de los ficheros dentro del elemento que se está descomprimiendo
    Public ReadOnly Property EleActual_TamanoTotal As System.Nullable(Of Long)
        Get
            SyncLock Mutex
                Return _TamanoTotal
            End SyncLock
        End Get
    End Property


    ' Tamaño total de los ficheros ya descomprimidos completamente dentro del elemento que se está descomprimiendo
    Public ReadOnly Property EleActual_TamanoTotalExtraido As System.Nullable(Of Long)
        Get
            SyncLock Mutex
                Return _TamanoTotalExtraido
            End SyncLock
        End Get
    End Property

    ' Ruta del elemento que se está descomprimiendo
    Public ReadOnly Property EleActual_Ruta As String
        Get
            SyncLock Mutex
                Return _pathElementoActual
            End SyncLock
        End Get
    End Property

    ' Codigo del elemento que se está descomprimiendo
    Public ReadOnly Property EleActual_Codigo As String
        Get
            SyncLock Mutex
                Return _codigoElementoActual
            End SyncLock
        End Get
    End Property

    ' Tamaño total del fichero que se está descomprimiendo
    Public ReadOnly Property EleActual_FicActTamano As System.Nullable(Of Long)
        Get
            SyncLock Mutex
                Return _FicActTamanoTotal
            End SyncLock
        End Get
    End Property

    ' Bytes extraidos del fichero que se está descomprimiendo
    Public ReadOnly Property EleActual_FicActExtraido As System.Nullable(Of Long)
        Get
            SyncLock Mutex
                Return _FicActExtraido
            End SyncLock
        End Get
    End Property

    ' Nombre del fichero que se está descomprimiendo
    Public ReadOnly Property EleActual_FicActNombre As String
        Get
            SyncLock Mutex
                Return _FicActNombre
            End SyncLock
        End Get
    End Property



    Public Function AgregarElemento(ByVal Code As String, ByVal Path As String, ByVal CrearDirectorio As Boolean, Password As String) As Boolean
        ' Comprobamos que el path no sea nulo, que el fichero exista, y tenga una extensión soportada
        If String.IsNullOrEmpty(Path) Then Return False
        If Not File.Exists(Path) Then Return False
        ' Solo admite 7z, rar, tar, zip
        Dim FicheroSoportado As Boolean = False
        For Each extension As String In _ExtensionesSoportadas
            If Path.ToLower.EndsWith("." & extension) Then
                FicheroSoportado = True
            End If
        Next
        ' Multipart 7z volumes (.7z.001, .7z.002, ...) are also supported via the
        ' 7-Zip CLI — queue the first volume.
        If Not FicheroSoportado AndAlso System.Text.RegularExpressions.Regex.IsMatch(Path.ToLower, "\.7z\.\d+$") Then
            FicheroSoportado = True
        End If
        If Not FicheroSoportado Then Return False


        Dim Fichero As String = ""
        Dim Directorio As String = ""
        Dim FicheroSinExtension As String = ""
        Dim IsPartRAR As Boolean = False
        Dim RARPartLength As Integer = 0

        If ObtenerNombres(Path, Directorio, Fichero, FicheroSinExtension, IsPartRAR, RARPartLength) Then

            If IsPartRAR Then
                ' Si es un RAR multivolumen intentamos poner en la cola tan solo el primer rar, si existe, claro
                Dim Path2 As String = IO.Path.Combine(Directorio, FicheroSinExtension) & ".part" & "1".PadLeft(RARPartLength, "0"c) & ".rar"
                If File.Exists(Path2) Then Path = Path2 ' Si no existe ya dará error al intentar descomprimir...
            End If

            ' Multipart 7z: queue the first volume, like the RAR case above.
            Dim m7z As System.Text.RegularExpressions.Match = System.Text.RegularExpressions.Regex.Match(Path, "\.7z\.(\d+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            If m7z.Success Then
                Dim primera7z As String = Path.Substring(0, m7z.Index) & ".7z.001"
                If Not String.Equals(Path, primera7z, StringComparison.OrdinalIgnoreCase) AndAlso File.Exists(primera7z) Then
                    Path = primera7z
                End If
            End If

            ' Comprobamos si ya existe en la cola
            SyncLock Mutex
                For Each key As String In _colaElementos.Keys
                    If _colaElementos(key).Path = Path Then
                        Log.WriteInfo("File '" & Path & "' for element '" & Code & "' is already in queue.")
                        Return False
                    End If
                Next
            End SyncLock

            Log.WriteInfo("Adding to decompression queue element '" & Code & "' (file '" & Path & "')")

            SyncLock Mutex
                ' 同一 Code 重复入队时更新既有条目(索引器赋值"存在即更新"):
                ' 此前重复项被静默跳过却仍返回 True,上游状态会卡在"正在解压"且进度条不动
                _colaElementos(Code) = New QueueItem With {.Path = Path, .CreateDirectory = CrearDirectorio, .Password = Password}
            End SyncLock

            Return True
        Else
            Return False
        End If

    End Function

    ''' <summary>
    ''' 同步解压单个压缩包（不支持队列/取消/进度事件）：成功返回 ""，失败返回错误描述。
    ''' 供回归测试（Tests/DescompresorTests）及无需后台队列的调用方使用。
    ''' </summary>
    Public Shared Function ExtractArchiveSync(ByVal archivePath As String, ByVal outputDir As String, ByVal password As String) As String
        Try
            Dim d As New Descompressor(archivePath, outputDir, password)
            d.Extract()
            If d.Exception IsNot Nothing Then Return d.Exception.Message
            Return ""
        Catch ex As Exception
            Return ex.Message
        End Try
    End Function

    Public Shared Sub DescompresorController_DoWork(sender As Object, e As DoWorkEventArgs)
        Try
            Log.WriteWarning("Starting worker bgwDescompresor")
            Dim worker As BackgroundWorker = CType(sender, BackgroundWorker)

            While Not worker.CancellationPending

                ' P1-2:逐次迭代独立兜底。此前整个 While 被一个 Try 包住,任何异常
                ' (哪怕只是 PonerElementoAProcesar 的一次瞬时失败)都会跳出循环 →
                ' Catch 只写日志 → DoWork 正常返回 → RunWorkerCompleted 只置标志不重启 →
                ' worker 静默死亡,解压队列永久报废到重启进程。
                ' 现在单次迭代出错只跳过本次,worker 继续消费队列。
                Try
                    If GetController.PonerElementoAProcesar Then

                        GetController.ProcesarElemento(worker.CancellationPending)

                    End If
                Catch ex As Exception
                    Log.WriteError("Error on worker bgwDescompresor (iteration, worker continues): " & ex.ToString)
                    ' 该迭代取出的元素可能已占据"当前元素"槽位,必须清掉,否则
                    ' PonerElementoAProcesar 永远返回 False(队列卡死)且 Ocupado() 恒 True。
                    GetController.DiscardCurrentElementOnError("Unexpected error while processing the extraction queue: " & ex.Message)
                End Try

                If worker.CancellationPending Then
                    Exit While
                End If

                System.Threading.Thread.Sleep(600)
            End While

            Log.WriteWarning("Finishing worker bgwDescompresor")
        Catch ex As Exception
            ' 最外层兜底:走到这里说明 worker 自身已不可用。记录后由 Main.vb 的
            ' bgwDescompresor 看门狗重启(见 Main.vb:bgwDescompresor_RunWorkerCompleted)。
            Log.WriteError("Error on worker bgwDescompresor: " & ex.ToString)
        End Try
    End Sub

    Public Function GetCola() As Generic.List(Of String)
        SyncLock Mutex
            Dim l As New Generic.List(Of String)
            For Each key As String In _colaElementos.Keys
                l.Add(_colaElementos(key).Path)
            Next
            Return l
        End SyncLock
    End Function

    ''' <summary>
    ''' Indica si el proceso está ocupado descomprimiendo un fichero o tiene elementos en cola pendientes de ser procesados
    ''' </summary>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function Ocupado() As Boolean
        SyncLock Mutex
            Return Not String.IsNullOrEmpty(_pathElementoActual) Or _colaElementos.Count > 0
        End SyncLock
    End Function

#End Region

#Region "Clase descompresora"




    Private Class Descompressor


        Public Password As String
        Private PathFichero As String
        Private PathExtraccion As String
        Public Exception As Exception
        Public CancelRequested As Boolean

        ' P2-4: collision accounting for the extraction folder. Read by the controller
        ' thread after extractThread.Join (the join provides the happens-before edge).
        Public OverwritesSameSize As Integer = 0
        Public CollisionsKeptOriginal As Integer = 0

        Public Sub New(ByVal _Fichero As String, ByVal _PathExtraccion As String, ByVal _Password As String)

            PathFichero = _Fichero
            PathExtraccion = _PathExtraccion
            Password = _Password
            CancelRequested = False

            If String.IsNullOrEmpty(Password) Then Password = Nothing ' Evitamos string.empty
        End Sub

        Private Shared Function ReaderOpts(ByVal Password As String) As ReaderOptions
            Dim opts As New ReaderOptions()
            If Not String.IsNullOrEmpty(Password) Then opts.Password = Password
            Return opts
        End Function

        Private Shared Function ExtractOpts() As ExtractionOptions
            Dim opts As New ExtractionOptions()
            opts.ExtractFullPath = True
            ' P2-4: Overwrite stays True ONLY for the idempotent case — the destination
            ' is resolved by ResolveEntryDestination first, which moves foreign files
            ' out of the way (or lets a repeat extraction through). See that function.
            opts.Overwrite = True
            Return opts
        End Function

        Private Shared Function getIArchive(PathFichero As String, Password As String, Volumenes As List(Of String)) As IArchive

            If PathFichero.ToUpper.EndsWith(".RAR") Then
                ' P2-3: 分卷 RAR 必须把整组卷一起交给 SharpCompress。此前只开第一卷,
                ' 于是 IsComplete 反映的只是"第一卷在不在",缺卷检测与后续卷的条目
                ' 全都拿不到(不带密码的多卷 RAR 只能解出第一卷的内容)。
                If Volumenes IsNot Nothing AndAlso Volumenes.Count > 1 Then
                    Dim archivos As New List(Of FileInfo)()
                    For Each v As String In Volumenes
                        Try
                            archivos.Add(New FileInfo(v))
                        Catch ex As Exception
                            Log.WriteWarning("Could not stat RAR volume '" & v & "': " & Log.SafeException(ex))
                        End Try
                    Next
                    If archivos.Count > 1 Then
                        Return RarArchive.OpenArchive(archivos, ReaderOpts(Password))
                    End If
                End If
                Return RarArchive.OpenArchive(PathFichero, ReaderOpts(Password))
            ElseIf PathFichero.ToUpper.EndsWith(".ZIP") Then
                Return ZipArchive.OpenArchive(PathFichero, ReaderOpts(Password))
            Else
                Return ArchiveFactory.OpenArchive(PathFichero, ReaderOpts(Nothing))
            End If

        End Function

        ''' <summary>
        ''' P2-3: 收集 RAR 分卷,返回按卷序排列的完整路径列表(第一卷在最前)。
        ''' 此前这里靠文件名后缀反推卷号(只认 part1/part01/part001/part0001.rar),于是:
        '''  - .r00/.r01 这类旧式命名算出的 LengthPart=0 → 只读第一卷,解压结果不完整;
        '''  - 单卷 RAR 只要文件名碰巧叫 xxx.part1.rar 就会被当成多卷,再叠加原 :687
        '''    那个"在单卷判定之前就抛"的 throw → 带密码的单卷 RAR 直接被拒。
        ''' 现在文件名只用来"找兄弟卷",不再用来"判定是不是多卷"(判定交给 archive
        ''' 自己的 rar.IsMultipartVolume())。支持两种通行命名:
        '''  - 新式 foo.part1.rar / foo.part2.rar ...
        '''  - 旧式 foo.rar(第一卷)+ foo.r00 + foo.r01 ...
        ''' </summary>
        Private Shared Function DescubrirVolumenesRar(primeraRuta As String) As List(Of String)
            Dim resultado As New List(Of String)
            If String.IsNullOrWhiteSpace(primeraRuta) Then Return resultado

            Dim directorio As String = Path.GetDirectoryName(primeraRuta)
            If String.IsNullOrEmpty(directorio) Then directorio = "."
            ' 注意:必须用完整文件名做判定。Path.GetFileNameWithoutExtension("foo.r00")
            ' 会把 ".r00" 当成扩展名剥掉(得到 "foo"),旧式分卷就会被误判并重复入列。
            Dim nombre As String = Path.GetFileName(primeraRuta)   ' foo.part01.rar / foo.rar / foo.r00

            ' --- 新式 foo.partN.rar ---
            Dim mPart As System.Text.RegularExpressions.Match =
                System.Text.RegularExpressions.Regex.Match(nombre, "^(?<base>.+)\.part(?<num>\d+)\.rar$",
                                                           System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            If mPart.Success Then
                Dim baseNombre As String = mPart.Groups("base").Value
                Dim digitos As Integer = mPart.Groups("num").Value.Length
                Dim num As Integer = Integer.Parse(mPart.Groups("num").Value)
                resultado.Add(primeraRuta)
                Dim n As Integer = num + 1
                Do
                    Dim cand As String = Path.Combine(directorio, baseNombre & ".part" & n.ToString().PadLeft(digitos, "0"c) & ".rar")
                    If Not File.Exists(cand) Then Exit Do
                    resultado.Add(cand)
                    n += 1
                Loop While n <= 9999
                Return resultado
            End If

            ' --- 旧式:选中的是 foo.rNN 这一卷,回到 foo.rar 第一卷 ---
            Dim mOld As System.Text.RegularExpressions.Match =
                System.Text.RegularExpressions.Regex.Match(nombre, "^(?<base>.+)\.r(?<num>\d+)$",
                                                           System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            If mOld.Success Then
                Dim baseNombre As String = mOld.Groups("base").Value
                Dim cabecera As String = Path.Combine(directorio, baseNombre & ".rar")
                If Not File.Exists(cabecera) Then
                    ' 没有第一卷:交回给 archive 自己报"这不是第一卷"。
                    resultado.Add(primeraRuta)
                    Return resultado
                End If
                resultado.Add(cabecera)
                Dim n As Integer = 0
                Do
                    Dim cand As String = Path.Combine(directorio, baseNombre & ".r" & n.ToString().PadLeft(2, "0"c))
                    If Not File.Exists(cand) Then Exit Do
                    If Not resultado.Contains(cand) Then resultado.Add(cand)
                    n += 1
                Loop While n <= 9999
                Return resultado
            End If

            ' --- foo.rar:单卷,或者旧式分卷的第一卷 ---
            If nombre.ToLowerInvariant().EndsWith(".rar") Then
                Dim baseNombre As String = nombre.Substring(0, nombre.Length - 4)
                resultado.Add(primeraRuta)
                Dim n As Integer = 0
                Do
                    Dim cand As String = Path.Combine(directorio, baseNombre & ".r" & n.ToString().PadLeft(2, "0"c))
                    If Not File.Exists(cand) Then Exit Do
                    resultado.Add(cand)
                    n += 1
                Loop While n <= 9999
                Return resultado
            End If

            resultado.Add(primeraRuta)
            Return resultado
        End Function


        ''' <summary>
        ''' P3-12: 判断是否 7z 容器。只有结尾 ".7z",或 ".7z.NNN"(NNN 纯数字分卷)
        ''' 才算 7z。此前用 Contains(".7z."),于是 xxx.7z.zip / xxx.7z.rar 这类
        ''' zip/rar 也被送去 7-Zip CLI,而 7zr 打不开 zip/rar → 退出码非 0 →
        ''' 报 "7-Zip failed with exit code N",原本能正常解的包反而失败。
        ''' </summary>
        Private Shared Function Is7zContainer(PathFichero As String) As Boolean
            Dim p As String = PathFichero.ToLowerInvariant()
            If p.EndsWith(".7z") Then Return True
            Return System.Text.RegularExpressions.Regex.IsMatch(p, "\.7z\.\d+$")
        End Function

        Public Sub Extract()
            Try


                ' 7z archives (single .7z or multipart .7z.001/.002/...) are handled by
                ' the external 7-Zip CLI — SharpCompress does not support the 7z container
                ' format at all, which made every .7z extraction fail. Prefer a system
                ' 7-Zip install; fall back to the embedded 7zr.exe (public domain).
                If Is7zContainer(PathFichero) Then
                    Extract7z()
                    Return
                End If

                ' P2-3: RAR 分卷按命名约定收集(见 DescubrirVolumenesRar)。打开压缩包时
                ' 把整组卷一起交给 RarArchive,这样 IsComplete 反映的是"卷齐不齐",
                ' 而不是"只有第一卷";不带密码的多卷 RAR 也才能把后续卷的条目一并解出。
                Dim VolumenesRar As List(Of String) = Nothing
                If PathFichero.ToUpperInvariant().EndsWith(".RAR") Then
                    VolumenesRar = DescubrirVolumenesRar(PathFichero)
                End If

                Using archive As IArchive = getIArchive(PathFichero, Password, VolumenesRar)

                    If archive.IsComplete Then

                        Dim rar As IRarArchive = TryCast(archive, IRarArchive)
                        If rar IsNot Nothing AndAlso _
                              rar.IsMultipartVolume() AndAlso _
                              Not rar.IsFirstVolume() Then
                            ' P1-4: a non-first volume used to Exit Sub silently, so the
                            ' caller saw "no exception" and reported a successful extraction
                            ' that had not extracted a single byte (false success).
                            Throw New ApplicationException(Language.GetText( _
                                "This file is not the first volume of a multipart RAR archive, so there is nothing to extract from it alone. Add the first volume (part1.rar) instead."))
                        End If


                        If (archive.IsSolid Or Not String.IsNullOrEmpty(Password)) And rar IsNot Nothing Then

                            ' No nos sirve el ArchiveFactory... debemos usar el reader, pero solo para ficheros solidos RAR
                            ' Debemos pasar la lista con todos los part... pero como hemos comprobado antes
                            ' que "IsComplete" entonces estamos seguros que los tenemos todos...
                            ' P2-3: 分卷列表改由 DescubrirVolumenesRar 按命名约定收集(见该函数)。
                            ' 此前靠文件名后缀反推卷号(只认 part1/part01/part001/part0001.rar):
                            '  - .r00/.r01 旧式命名 LengthPart=0 → 只读第一卷,解压结果不完整;
                            '  - 单卷 RAR 恰好叫 xxx.part1.rar 时会被误判成多卷,再叠加下面
                            '    那个"在单卷判定之前就抛"的 throw → 带密码的单卷 RAR 直接被拒。
                            Dim ListaFicheros As New Generic.List(Of Stream)
                            Try
                                Dim rutasVolumen As List(Of String) = VolumenesRar
                                If rutasVolumen Is Nothing OrElse rutasVolumen.Count = 0 Then
                                    rutasVolumen = New List(Of String)()
                                    rutasVolumen.Add(PathFichero)
                                End If
                                For Each v As String In rutasVolumen
                                    ListaFicheros.Add(File.OpenRead(v))
                                Next

                                If ListaFicheros.Count = 1 Then

                                    Using reader As IReader = RarReader.OpenReader(ListaFicheros(0), ReaderOpts(Password))
                                       
                                        Dim c As DescompresorController = DescompresorController.GetController
                                        Try

                                            SyncLock Mutex
                                                c._TamanoTotal = 0
                                                c._FicActTamanoTotal = 0
                                                c._TamanoTotalExtraido = 0
                                                c._FicActExtraido = 0
                                                c._FicActNombre = ""
                                                For Each entry As IArchiveEntry In archive.Entries
                                                    c._TamanoTotal += entry.Size
                                                Next
                                                EnsureExtractWithinQuota(c._TamanoTotal.GetValueOrDefault(), archive.Entries.Count())
                                            End SyncLock

                                            While reader.MoveToNextEntry
                                                If CancelRequested Then Throw New OperationCanceledException("Extraction cancelled.")
                                                If Not reader.Entry.IsDirectory Then
                                                    c._FicActNombre = reader.Entry.Key

                                                    c._TamanoTotalExtraido += c._FicActTamanoTotal
                                                    c._FicActTamanoTotal = reader.Entry.Size
                                                    c._FicActExtraido = 0

                                                    WriteSafeEntry(reader, PathExtraccion, reader.Entry.Key)
                                                    ' 新版无 CompressedBytesRead 流式事件：按条目结算进度，
                                                    ' 再按解压优先级让出（旧 ListeningStream.Read 内 Sleep 的替代）。
                                                    c._FicActExtraido = reader.Entry.Size
                                                    DescompresionThrottle.ApplyThrottle()
                                                End If
                                            End While
                                        Finally
                                            SyncLock Mutex
                                                c._TamanoTotal = Nothing
                                                c._FicActTamanoTotal = Nothing
                                                c._TamanoTotalExtraido = Nothing
                                                c._FicActExtraido = Nothing
                                                c._FicActNombre = Nothing
                                            End SyncLock
                                           
                                        End Try

                                    End Using


                                Else
                                    ' P2-3: 多卷分支此前用 ReaderOpts(Nothing) 把密码显式丢掉
                                    ' (单卷分支是 ReaderOpts(Password))→ 带密码的分卷 RAR
                                    ' 必然解不出来;而且原来那个"多卷+密码就抛
                                    ' NotSupportedException"的硬编码英文拦截发生在单卷
                                    ' 判定之前,单卷 RAR 只要文件名带 part1 也会被拒。
                                    ' 现在两个分支都带上密码,失败时给出本地化原因。
                                    Try
                                        Using reader As IReader = RarReader.OpenReader(ListaFicheros, ReaderOpts(Password))
                                 
                                            Dim c As DescompresorController = DescompresorController.GetController
                                            Try
                                                SyncLock Mutex
                                                    c._TamanoTotal = 0
                                                    c._FicActTamanoTotal = 0
                                                    c._TamanoTotalExtraido = 0
                                                    c._FicActExtraido = 0
                                                    c._FicActNombre = ""
                                                    For Each entry As IArchiveEntry In archive.Entries
                                                        c._TamanoTotal += entry.Size
                                                    Next
                                                    EnsureExtractWithinQuota(c._TamanoTotal.GetValueOrDefault(), archive.Entries.Count())
                                                End SyncLock

                                                While reader.MoveToNextEntry
                                                    If CancelRequested Then Throw New OperationCanceledException("Extraction cancelled.")
                                                    If Not reader.Entry.IsDirectory Then
                                                        c._FicActNombre = reader.Entry.Key

                                                        c._TamanoTotalExtraido += c._FicActTamanoTotal
                                                        c._FicActTamanoTotal = reader.Entry.Size
                                                        c._FicActExtraido = 0

                                                        WriteSafeEntry(reader, PathExtraccion, reader.Entry.Key)
                                                        ' 新版无 CompressedBytesRead 流式事件：按条目结算进度，
                                                        ' 再按解压优先级让出（旧 ListeningStream.Read 内 Sleep 的替代）。
                                                        c._FicActExtraido = reader.Entry.Size
                                                        DescompresionThrottle.ApplyThrottle()
                                                    End If
                                                End While
                                            Finally
                                                SyncLock Mutex
                                                    c._TamanoTotal = Nothing
                                                    c._FicActTamanoTotal = Nothing
                                                    c._TamanoTotalExtraido = Nothing
                                                    c._FicActExtraido = Nothing
                                                    c._FicActNombre = Nothing
                                                End SyncLock
                                          
                                            End Try

                                        End Using
                                    Catch ex As Exception
                                        ' P2-3: 失败原因对用户要有指向性(卷不齐/密码不对),
                                        ' 不再把 SharpCompress 的原始异常直接抛出去。
                                        If Not String.IsNullOrEmpty(Password) Then
                                            Throw New ApplicationException( _
                                                Language.GetText("Could not extract the RAR archive. Check that all volumes are present and that the password is correct.") & _
                                                " (" & ex.Message & ")")
                                        End If
                                        Throw
                                    End Try
                                End If





                            Finally
                                ' Cerramos los stream
                                For Each s As Stream In ListaFicheros
                                    s.Close()
                                    s.Dispose()
                                Next
                            End Try

                        Else

                            Dim c As DescompresorController = DescompresorController.GetController
                            Try
                                SyncLock Mutex
                                    c._TamanoTotal = 0
                                    c._FicActTamanoTotal = 0
                                    c._TamanoTotalExtraido = 0
                                    c._FicActExtraido = 0
                                    c._FicActNombre = ""
                                    For Each entry As IArchiveEntry In archive.Entries
                                        c._TamanoTotal += entry.Size
                                    Next
                                    EnsureExtractWithinQuota(c._TamanoTotal.GetValueOrDefault(), archive.Entries.Count())
                                End SyncLock

                                Dim entryKeys As New Generic.List(Of String)
                                For Each entry As IArchiveEntry In archive.Entries
                                    If Not entry.IsDirectory Then
                                        entryKeys.Add(entry.Key)
                                    End If
                                Next
                                PathGuard.ValidateArchiveEntries(PathExtraccion, entryKeys)

                                For Each entry As IArchiveEntry In archive.Entries
                                    If CancelRequested Then
                                        Throw New OperationCanceledException("Extraction cancelled.")
                                    End If
                                    If Not entry.IsDirectory Then
                                        c._FicActNombre = entry.Key
                                        ' 新版无 EntryExtractionBegin/CompressedBytesRead 事件：
                                        ' 沿用 reader 分支的记账方式，按条目推进。
                                        c._TamanoTotalExtraido += c._FicActTamanoTotal
                                        c._FicActTamanoTotal = entry.Size
                                        c._FicActExtraido = 0
                                        WriteSafeEntry(entry, PathExtraccion)
                                        c._FicActExtraido = entry.Size
                                        DescompresionThrottle.ApplyThrottle()
                                    End If
                                Next
                            Finally
                                SyncLock Mutex
                                    c._TamanoTotal = Nothing
                                    c._FicActTamanoTotal = Nothing
                                    c._TamanoTotalExtraido = Nothing
                                    c._FicActExtraido = Nothing
                                    c._FicActNombre = Nothing
                                End SyncLock
                            End Try


                        End If
                    Else
                        ' 缺分卷等不完整压缩包必须显式失败:此前静默跳过整个解压块且不置
                        ' Exception,上游会把一个字节都没解出来的任务误报为"解压成功"
                        Throw New ApplicationException(Language.GetText("Archive is incomplete: one or more volumes are missing"))
                    End If

                End Using
            Catch ex As Exception
                Exception = ex
            End Try
        End Sub

        Private Const MaxExtractTotalBytes As Long = 50L * 1024L * 1024L * 1024L ' 50 GiB hard cap
        Private Const MaxExtractEntries As Integer = 100000

#Region "7z CLI support"

        ''' <summary>
        ''' Extracts a .7z archive (single or multipart .7z.001) via the 7-Zip command
        ''' line. A system-installed 7-Zip is preferred; otherwise the embedded
        ''' 7zr.exe (public domain, 7z format only) is dropped next to the user's
        ''' application data and used. Entries are validated against path traversal
        ''' BEFORE extraction using the same PathGuard rules as the SharpCompress path.
        ''' </summary>
        Private Sub Extract7z()
            Dim cli As String = FindSystemSevenZip()
            If cli Is Nothing Then cli = EnsureEmbedded7zr()
            If cli Is Nothing Then
                Throw New ApplicationException("No 7-Zip executable available to extract .7z archives.")
            End If

            ' Both passes need the password: a header-encrypted archive (-mhe=on) cannot
            ' even be listed without it, so Pass 1 used to fail with exit code 255 no
            ' matter how correct the configured password was.
            ' Quoted because Windows argv splitting would otherwise break a password
            ' containing spaces into several arguments.
            Dim passwordArg As String = ""
            If Not String.IsNullOrEmpty(Password) Then
                ' P2-1: 7-Zip's own command-line parser cannot express a literal double
                ' quote inside -p. Verified empirically with the bundled 7zr 26.02 via
                ' CreateProcessW (exact .NET argument passing): neither backslash escaping
                ' (-p"pa\"ss") nor doubled quotes (-p"pa""ss") reaches the password — both
                ' corrupt the argument and fail. Reject with an actionable, localized
                ' message instead of a raw exception. Trailing backslashes, on the other
                ' hand, ARE delivered correctly with plain quoting (same test: a trailing
                ' backslash changed the password as expected), so the old backslash
                ' rejection was unnecessary and has been removed.
                If Password.Contains(""""c) Then
                    Log.WriteWarning("Extract7z: password contains a double quote, which the 7-Zip CLI cannot express; rejecting.")
                    Throw New ApplicationException(Language.GetText( _
                        "The extraction password contains a double quote, which the built-in 7-Zip extractor cannot pass on its command line. Extract this archive with the full 7-Zip application, or use an archive whose password has no double quotes."))
                End If
                passwordArg = " -p""" & Password & """"
            End If

            ' ---- Pass 1: list entries and validate them against path traversal ----
            ' -sccUTF-8 必须与 Pass2(x -y -bd -sccUTF-8)一致:列表输出的条目名要与
            ' 解压真实落盘名逐字节对得上,否则碰撞检测/路径校验比对的是乱码名,
            ' 非 ASCII 文件名(中文等)的防覆盖保护会完全失效。ASCII 对照无影响。
            Dim listing As String = RunSevenZip(cli, "l -ba -slt -sccUTF-8" & passwordArg & " -- """ & PathFichero & """", checkCancel:=True)
            Dim entryKeys As New Generic.List(Of String)
            Dim entrySizes As New Generic.Dictionary(Of String, Long)(StringComparer.OrdinalIgnoreCase)
            Dim archiveFullPath As String = IO.Path.GetFullPath(PathFichero)
            ' P1-1:7z CLI 路径此前只校验条目数与路径穿越,50GiB 体积上限被绕过。
            ' 此处累加各条目 Size(=解压后体积),复用同一 EnsureExtractWithinQuota。
            Dim totalUncompressed As Long = 0
            Dim inArchiveBlock As Boolean = False
            Dim currentEntry As String = Nothing
            For Each line As String In listing.Split(New String() {vbCrLf, vbLf, vbCr}, StringSplitOptions.RemoveEmptyEntries)
                If line.StartsWith("Path = ", StringComparison.Ordinal) Then
                    Dim entryPath As String = line.Substring(7).Trim()
                    ' Some 7-Zip builds include the archive itself as the first
                    ' listing block ("Path = <archive>"). Skip it defensively by
                    ' comparing full paths — an entry key is always relative.
                    Dim isArchiveItself As Boolean = False
                    Try
                        isArchiveItself = String.Equals(IO.Path.GetFullPath(entryPath), archiveFullPath, StringComparison.OrdinalIgnoreCase)
                    Catch
                        ' Relative entry key — cannot be the archive file itself.
                    End Try
                    inArchiveBlock = isArchiveItself
                    currentEntry = If(isArchiveItself, Nothing, entryPath)
                    If Not isArchiveItself Then entryKeys.Add(entryPath)
                ElseIf line.StartsWith("Size = ", StringComparison.Ordinal) AndAlso Not inArchiveBlock Then
                    Dim sizeVal As Long = 0
                    If Long.TryParse(line.Substring(7).Trim(), sizeVal) Then
                        If sizeVal > 0 Then
                            totalUncompressed += sizeVal
                        End If
                        ' P2-4: remember the per-entry size for the collision policy below.
                        ' Zero-size entries must participate too — otherwise a foreign
                        ' non-empty file sitting at an empty entry's path would be
                        ' silently overwritten by the CLI's -y (the managed path
                        ' protects it via ResolveEntryDestination).
                        If currentEntry IsNot Nothing AndAlso Not entrySizes.ContainsKey(currentEntry) Then
                            entrySizes(currentEntry) = sizeVal
                        End If
                        If totalUncompressed > MaxExtractTotalBytes Then Exit For
                    End If
                End If
            Next
            EnsureExtractWithinQuota(totalUncompressed, entryKeys.Count)
            PathGuard.ValidateArchiveEntries(PathExtraccion, entryKeys)

            ' P2-4: same collision policy as the managed extractors — move existing
            ' files whose size differs from the entry to "name (2).ext" so the CLI's
            ' -y never silently replaces foreign content. A same-sized file is the
            ' output of a previous extraction of this archive and is left for -y to
            ' refresh (idempotent re-extraction).
            For Each kvp In entrySizes
                Dim dest7z As String = PathGuard.GetSafeArchiveEntryPath(PathExtraccion, kvp.Key)
                If System.IO.File.Exists(dest7z) Then
                    Dim fi7z As New System.IO.FileInfo(dest7z)
                    If fi7z.Length = kvp.Value Then
                        OverwritesSameSize += 1
                        Log.WriteWarning("Extract7z: replacing '" & dest7z & "' with an identically sized entry (repeat extraction of the same archive).")
                    Else
                        Dim kept7z As String = GetNonCollidingPath(dest7z)
                        Try
                            System.IO.File.Move(dest7z, kept7z)
                            CollisionsKeptOriginal += 1
                            Log.WriteWarning("Extract7z: '" & dest7z & "' already exists with different content; keeping it as '" & kept7z & "'.")
                        Catch ex As Exception
                            CollisionsKeptOriginal += 1
                            Log.WriteError("Extract7z: could not rename '" & dest7z & "' (" & Log.SafeException(ex) & "); the CLI may overwrite it (-y).")
                        End Try
                    End If
                End If
            Next

            ' ⑩:把 Pass1 已算出的解压后总量发布到控制器,解压窗不再全程显示 " -"。
            ' CLI 无逐字节回调,已解字节只能显示 0/总量(托管分支才有细粒度);结束后按惯例清回 Nothing。
            Dim c As DescompresorController = DescompresorController.GetController
            SyncLock Mutex
                c._TamanoTotal = totalUncompressed
                c._FicActTamanoTotal = 0
                c._TamanoTotalExtraido = 0
                c._FicActExtraido = 0
                c._FicActNombre = PathFichero
            End SyncLock

            Try
                ' ---- Pass 2: extract ----
                ' -p<password>: no space after -p; no -p at all means "no password",
                ' and an empty -p would make 7-Zip prompt (impossible headless).
                Dim args As New System.Text.StringBuilder()
                args.Append("x -y -bd -sccUTF-8 -o""")
                args.Append(PathExtraccion)
                args.Append("""")
                args.Append(passwordArg)
                args.Append(" -- """).Append(PathFichero).Append(""""c)

                RunSevenZip(cli, args.ToString(), checkCancel:=True)
            Finally
                SyncLock Mutex
                    c._TamanoTotal = Nothing
                    c._FicActTamanoTotal = Nothing
                    c._TamanoTotalExtraido = Nothing
                    c._FicActExtraido = Nothing
                    c._FicActNombre = Nothing
                End SyncLock
            End Try
        End Sub

        ''' <summary>
        ''' Locates a system-installed 7-Zip (7z.exe). Checks both registry views
        ''' (the process is x86, but 7-Zip is commonly installed as x64) and the
        ''' well-known install folders. Returns Nothing when not found.
        ''' </summary>
        Private Shared Function FindSystemSevenZip() As String
            Try
                For Each view As Microsoft.Win32.RegistryView In {Microsoft.Win32.RegistryView.Registry64, Microsoft.Win32.RegistryView.Default}
                    Using base As Microsoft.Win32.RegistryKey = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, view)
                        Using key As Microsoft.Win32.RegistryKey = base.OpenSubKey("SOFTWARE\7-Zip")
                            If key IsNot Nothing Then
                                Dim dir As Object = key.GetValue("Path")
                                If dir IsNot Nothing Then
                                    Dim exe As String = IO.Path.Combine(CStr(dir), "7z.exe")
                                    If File.Exists(exe) Then Return exe
                                End If
                                Dim root As Object = key.GetValue("Root")
                                If root IsNot Nothing Then
                                    Dim exe2 As String = IO.Path.Combine(CStr(root), "7z.exe")
                                    If File.Exists(exe2) Then Return exe2
                                End If
                            End If
                        End Using
                    End Using
                Next
            Catch ex As Exception
                Log.WriteWarning("7z lookup: registry probe failed: " & Log.SafeException(ex))
            End Try

            For Each candidate As String In New String() {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) & "\7-Zip\7z.exe",
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) & "\7-Zip\7z.exe"
            }
                If File.Exists(candidate) Then Return candidate
            Next
            Return Nothing
        End Function

        ''' <summary>
        ''' Extracts the embedded 7zr.exe (resource "MegaDownloader.7zr.exe", public
        ''' domain) to %LOCALAPPDATA%\MegaDownloader\bin and returns its path. The file
        ''' is (re)written whenever the embedded payload differs in size, so upgrades
        ''' replace the stale binary automatically.
        ''' </summary>
        Private Shared Function EnsureEmbedded7zr() As String
            Dim dir As String = IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MegaDownloader\bin")
            Dim target As String = IO.Path.Combine(dir, "7zr.exe")

            Try
                Dim asm As System.Reflection.Assembly = System.Reflection.Assembly.GetExecutingAssembly()
                Using src As Stream = asm.GetManifestResourceStream("MegaDownloader.7zr.exe")
                    If src Is Nothing Then
                        Log.WriteError("7z extraction: embedded 7zr.exe resource is missing.")
                        Return Nothing
                    End If

                    Dim rewrite As Boolean = True
                    If File.Exists(target) Then
                        Try
                            rewrite = (New FileInfo(target).Length <> src.Length)
                        Catch
                            rewrite = True
                        End Try
                    End If

                    If rewrite Then
                        Directory.CreateDirectory(dir)
                        Using dst As New FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None)
                            src.CopyTo(dst)
                        End Using
                        Log.WriteInfo("7z extraction: embedded 7zr.exe deployed to " & target)
                    End If
                End Using
                Return target
            Catch ex As Exception
                Log.WriteError("7z extraction: failed to deploy embedded 7zr.exe: " & Log.SafeException(ex))
                Return Nothing
            End Try
        End Function

        ''' <summary>
        ''' Runs the 7-Zip CLI with the given arguments and returns its stdout.
        ''' Exit code 0/1 (OK/warnings) pass; 2 (fatal, e.g. wrong password) and any
        ''' other code throw with the tail of stderr/stdout for the error message.
        ''' Honors CancelRequested by killing the process.
        ''' </summary>
        Private Function RunSevenZip(ByVal cliPath As String, ByVal arguments As String, ByVal checkCancel As Boolean) As String
            Dim psi As New ProcessStartInfo()
            psi.FileName = cliPath
            psi.Arguments = arguments
            psi.UseShellExecute = False
            psi.CreateNoWindow = True
            psi.RedirectStandardOutput = True
            psi.RedirectStandardError = True
            ' Give the CLI an immediate EOF on stdin. A header-encrypted archive with no
            ' password configured makes 7-Zip prompt for one; with an inherited (possibly
            ' still open) stdin it would block forever, and because stdout is drained with
            ' ReadToEnd before the cancellation loop below, the cancel check would never run.
            psi.RedirectStandardInput = True
            psi.StandardOutputEncoding = System.Text.Encoding.UTF8
            psi.StandardErrorEncoding = System.Text.Encoding.UTF8

            ' Never log the raw arguments: they can carry "-p<password>". The quoted
            ' alternative must come first so a password containing spaces is redacted
            ' whole instead of only up to its first space.
            Dim safeArgs As String = System.Text.RegularExpressions.Regex.Replace(arguments, "-p(?:""[^""]*""|\S+)", "-p[redacted]")
            Log.WriteInfo("7z CLI: " & IO.Path.GetFileName(cliPath) & " " & safeArgs)

            Using proc As New Process()
                proc.StartInfo = psi
                proc.Start()
                proc.StandardInput.Close()

                ' Read stderr asynchronously to avoid the classic pipe deadlock:
                ' if 7-Zip fills the stderr buffer while we are still blocked on
                ' ReadToEnd(stdout), the child stalls and both sides wait forever.
                Dim stderrTask As Threading.Tasks.Task(Of String) = proc.StandardError.ReadToEndAsync()
                Dim stdout As String = proc.StandardOutput.ReadToEnd()
                Dim stderr As String = stderrTask.Result

                ' Cooperative cancellation while waiting for the CLI to finish.
                While Not proc.WaitForExit(250)
                    If checkCancel AndAlso CancelRequested Then
                        Try
                            proc.Kill()
                        Catch
                        End Try
                        proc.WaitForExit(5000)
                        Throw New OperationCanceledException("Extraction cancelled.")
                    End If
                End While

                If proc.ExitCode <> 0 AndAlso proc.ExitCode <> 1 Then
                    Dim tail As String = (stderr & " " & stdout).Trim()
                    If tail.Length > 300 Then tail = tail.Substring(tail.Length - 300)
                    Throw New ApplicationException("7-Zip failed with exit code " & proc.ExitCode & ": " & tail)
                End If
                Return stdout
            End Using
        End Function

#End Region

        Private Shared Sub EnsureExtractWithinQuota(ByVal totalUncompressed As Long, ByVal entryCount As Integer)
            If entryCount > MaxExtractEntries Then
                Throw New InvalidOperationException("Archive rejected: too many entries (" & entryCount & ").")
            End If
            If totalUncompressed < 0 OrElse totalUncompressed > MaxExtractTotalBytes Then
                Throw New InvalidOperationException("Archive rejected: uncompressed size exceeds safety limit.")
            End If
        End Sub

        ''' <summary>
        ''' P2-4: resolve the final destination for an archive entry without silently
        ''' destroying pre-existing files on disk.
        '''  - A file of exactly the entry's size is treated as the output of a previous
        '''    extraction of the SAME archive (an entry is written whole, so a completed
        '''    entry always matches its size) and may be overwritten idempotently.
        '''  - Anything else — a foreign file, or a half-written file from a cancelled
        '''    run — is kept: it is renamed to "name (2).ext" (project convention, see
        '''    FileDownloader.RenamePartToReal) and the extracted content takes the
        '''    original name.
        ''' Returns the path the entry must be WRITTEN to: the original name when the
        ''' existing file was renamed aside (or no file existed), or the free
        ''' "name (2).ext"-style path when the rename itself failed and the existing
        ''' file must stay untouched.
        ''' </summary>
        Private Function ResolveEntryDestination(ByVal dest As String, ByVal expectedSize As System.Nullable(Of Long)) As String
            If Not System.IO.File.Exists(dest) Then Return dest

            Dim fi As New System.IO.FileInfo(dest)
            If expectedSize.HasValue AndAlso fi.Length = expectedSize.Value Then
                OverwritesSameSize += 1
                Log.WriteWarning("Extraction: replacing '" & dest & "' with an identically sized entry (repeat extraction of the same archive).")
                Return dest
            End If

            Dim kept As String = GetNonCollidingPath(dest)
            Try
                System.IO.File.Move(dest, kept)
                CollisionsKeptOriginal += 1
                Log.WriteWarning("Extraction: '" & dest & "' already exists with different content; keeping it as '" & kept & "' and extracting the archive entry under the original name.")
                ' The rename freed the original name — that is where the entry goes.
                ' (Returning `kept` here would overwrite the very file we just
                ' preserved, since ExtractOpts.Overwrite is True.)
                Return dest
            Catch ex As Exception
                ' Locked / ACLs / ...: do not destroy the file either — extract the
                ' entry to the free name and leave the existing file untouched.
                CollisionsKeptOriginal += 1
                Log.WriteError("Extraction: could not rename '" & dest & "' (" & Log.SafeException(ex) & "); extracting to '" & kept & "' instead.")
            End Try
            Return kept
        End Function

        ''' <summary>First free "name (2).ext"-style path next to dest (9999 tries, then
        ''' a short GUID suffix) — mirrors FileDownloader.RenamePartToReal.</summary>
        Private Shared Function GetNonCollidingPath(ByVal dest As String) As String
            Dim dir As String = IO.Path.GetDirectoryName(dest)
            Dim name As String = IO.Path.GetFileName(dest)
            Dim dot As Integer = name.LastIndexOf("."c)
            Dim baseName As String = If(dot > 0, name.Substring(0, dot), name)
            Dim ext As String = If(dot > 0, name.Substring(dot), "")
            Dim i As Integer = 1
            Do
                i += 1
                Dim candidate As String = IO.Path.Combine(dir, baseName & " (" & i & ")" & ext)
                If Not System.IO.File.Exists(candidate) AndAlso Not System.IO.Directory.Exists(candidate) Then Return candidate
                If i >= 9999 Then
                    Return IO.Path.Combine(dir, baseName & " (" & Guid.NewGuid().ToString("N").Substring(0, 8) & ")" & ext)
                End If
            Loop
        End Function

        Private Sub WriteSafeEntry(ByVal entry As IArchiveEntry, ByVal extractionRoot As String)
            Dim dest As String = PathGuard.GetSafeArchiveEntryPath(extractionRoot, entry.Key)
            dest = ResolveEntryDestination(dest, entry.Size)
            Dim parentDir As String = Path.GetDirectoryName(dest)
            If Not String.IsNullOrEmpty(parentDir) Then
                Directory.CreateDirectory(parentDir)
            End If
            entry.WriteToFile(dest, ExtractOpts())
        End Sub

        Private Sub WriteSafeEntry(ByVal reader As IReader, ByVal extractionRoot As String, ByVal entryKey As String)
            Dim dest As String = PathGuard.GetSafeArchiveEntryPath(extractionRoot, entryKey)
            dest = ResolveEntryDestination(dest, reader.Entry.Size)
            Dim parentDir As String = Path.GetDirectoryName(dest)
            If Not String.IsNullOrEmpty(parentDir) Then
                Directory.CreateDirectory(parentDir)
            End If
            reader.WriteEntryToFile(dest, ExtractOpts())
        End Sub

        'Private Function CreatePercentage(n As Long, d As Long) As Integer
        '    Return CInt((CDbl(n) / CDbl(d)) * 100)
        'End Function
    End Class


#End Region

End Class
