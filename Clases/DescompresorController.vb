Imports System.ComponentModel
Imports SharpCompress.Archives
Imports SharpCompress.Archives.IArchiveEntryExtensions
Imports SharpCompress.Readers
Imports SharpCompress.Archives.Rar
Imports SharpCompress.Archives.Zip
Imports SharpCompress.Readers.Rar
Imports SharpCompress.Common
Imports System.IO
Imports System.Text.RegularExpressions


Public Class DescompresorController

    Public Class QueueItem
        Public Path As String
        Public Password As String
        Public CreateDirectory As Boolean
        Public Deferred As Boolean
    End Class

    ''' <summary>多卷归档必须全部到齐才能解压(实测:只有首卷、甚至只差最后一卷,
    ''' 7zr 都返回 exit 2 "Cannot open the file as archive")。磁盘上看不出总卷数,
    ''' 因此在等待窗口内定期重试;每有新卷落盘就刷新窗口,窗口耗尽才判真失败。</summary>
    Private Structure VolumeLedgerEntry
        Public Length As Long
        Public LastWriteUtc As DateTime
        Public ContentHash As ULong
    End Structure

    Private Class EsperandoVolumenes
        Public Codes As New Generic.HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Public Password As String
        Public CreateDirectory As Boolean
        Public ProximoIntento As Date
        Public VolumenesVistos As Integer
        Public Limite As Date
    End Class

    Private Const VolumeRetryIntervalSeconds As Integer = 20
    Private Const VolumeWaitWindowMinutes As Integer = 30

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
    Private _currentDeferred As Boolean
    Private _currentCodes As New Generic.HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
    Private _cancelRequested As Boolean
    ' 去重必须同时覆盖"待办 / 正在解压 / 已解压完"三态:只扫待办队列时,
    ' N 个分卷会各自触发一次解压,产出 movie.mkv / movie (2).mkv / movie (3).mkv。
    ' 键附带归档的修改时间:重新下载同一路径后时间戳变化,台账自动失效,
    ' 否则用户删掉成品再重试就永远不再解压(且不报错)。
    Private _rutasCompletadas As Generic.Dictionary(Of String, VolumeLedgerEntry)
    Private _esperandoVolumenes As Generic.Dictionary(Of String, EsperandoVolumenes)

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
            _rutasCompletadas = New Generic.Dictionary(Of String, VolumeLedgerEntry)(StringComparer.OrdinalIgnoreCase)
            _esperandoVolumenes = New Generic.Dictionary(Of String, EsperandoVolumenes)(StringComparer.OrdinalIgnoreCase)
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

    Private Shared ReadOnly rxVolumePath As New Regex("(?<base>.+)\.(?:7z\.(?<sz>\d+)|part(?<rp>\d+)\.rar|rar|r(?<ro>\d+))$", RegexOptions.IgnoreCase Or RegexOptions.Compiled)

    ''' <summary>把任意分卷文件名归一到第一卷;找不到首卷时也返回该分卷本身,
    ''' 让控制器可以登记等待而不是让上游把任务误标为完成。</summary>
    Private Shared Function NormalizeQueuePath(ByVal path As String) As String
        If String.IsNullOrWhiteSpace(path) Then Return path
        Dim full As String = IO.Path.GetFullPath(path)
        Dim dir As String = IO.Path.GetDirectoryName(full)
        Dim name As String = IO.Path.GetFileName(full)
        Dim m As Match = rxVolumePath.Match(name)
        If Not m.Success OrElse String.IsNullOrEmpty(dir) Then Return full
        Dim baseName As String = m.Groups("base").Value
        If m.Groups("sz").Success Then
            Dim first As String = IO.Path.Combine(dir, baseName & ".7z." & "1".PadLeft(m.Groups("sz").Value.Length, "0"c))
            Return If(IO.File.Exists(first), first, full)
        End If
        If m.Groups("rp").Success Then
            Dim first As String = IO.Path.Combine(dir, baseName & ".part" & "1".PadLeft(m.Groups("rp").Value.Length, "0"c) & ".rar")
            Return If(IO.File.Exists(first), first, full)
        End If
        If m.Groups("ro").Success Then
            Dim first As String = IO.Path.Combine(dir, baseName & ".rar")
            Return If(IO.File.Exists(first), first, full)
        End If
        Return full
    End Function

    Private Shared Function IsPotentialMultiVolumePath(ByVal path As String) As Boolean
        If String.IsNullOrWhiteSpace(path) Then Return False
        Dim name As String = IO.Path.GetFileName(path)
        If rxVolumePath.Match(name).Groups("ro").Success Then Return True
        Dim full As String = IO.Path.GetFullPath(path)
        If name.ToLowerInvariant().EndsWith(".rar") AndAlso IO.File.Exists(full.Substring(0, full.Length - 4) & ".r00") Then Return True
        Return IsMultiVolumePath(full)
    End Function

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

    ''' <summary>The worker may be restarted after a cancelled shutdown. Clear the
    ''' one-shot shutdown signal before it dequeues another archive; otherwise every
    ''' subsequent extraction in this process would immediately report "cancelled".</summary>
    Private Sub ClearCancelIfIdle()
        SyncLock Mutex
            If _cancelRequested AndAlso String.IsNullOrEmpty(_pathElementoActual) AndAlso _colaElementos.Count = 0 Then
                _cancelRequested = False
            End If
        End SyncLock
    End Sub

    Private Function PonerElementoAProcesar() As Boolean
        SyncLock Mutex
            If Not String.IsNullOrEmpty(_pathElementoActual) Or _colaElementos.Count = 0 Then
                Return False ' Ya hay un elemento procesando o la cola está vacía
            Else
                _codigoElementoActual = _colaElementos.Keys(0)
                Dim item As QueueItem = _colaElementos(_codigoElementoActual)
                _crearDirectorio = item.CreateDirectory
                _pathElementoActual = item.Path
                _passwordElementoActual = item.Password
                _currentDeferred = item.Deferred
                _currentCodes.Clear()
                _currentCodes.Add(_codigoElementoActual)
                For Each kvp As KeyValuePair(Of String, QueueItem) In _colaElementos
                    If String.Equals(kvp.Value.Path, item.Path, StringComparison.OrdinalIgnoreCase) Then _currentCodes.Add(kvp.Key)
                Next
                Dim stale As New Generic.List(Of String)
                For Each kvp As KeyValuePair(Of String, QueueItem) In _colaElementos
                    If String.Equals(kvp.Value.Path, item.Path, StringComparison.OrdinalIgnoreCase) Then stale.Add(kvp.Key)
                Next
                For Each code As String In stale
                    _colaElementos.Remove(code)
                Next
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
        Dim codes As New Generic.List(Of String)
        SyncLock Mutex
            codes.AddRange(_currentCodes)
            If Not String.IsNullOrEmpty(_codigoElementoActual) AndAlso Not codes.Contains(_codigoElementoActual) Then codes.Add(_codigoElementoActual)
            If Not String.IsNullOrEmpty(_pathElementoActual) Then _esperandoVolumenes.Remove(_pathElementoActual)
            Me._pathElementoActual = Nothing
            Me._passwordElementoActual = Nothing
            Me._codigoElementoActual = Nothing
            Me._currentDeferred = False
            Me._currentCodes.Clear()
        End SyncLock
        For Each codigo As String In codes
            Try
                RaiseEvent DescompresionFinalizada(codigo, False, motivo)
            Catch ex As Exception
                Log.WriteError("Decompressor: failed to report failure for '" & codigo & "': " & Log.SafeException(ex))
            End Try
        Next
        Return If(codes.Count > 0, codes(0), Nothing)
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
                    ' 等分卷记录必须在这里清:外层 Catch 调 DiscardCurrentElementOnError 时
                    ' _pathElementoActual 已被下面置空,那里的清理够不着,记录会孤立在
                    ' ProximoIntento=MaxValue,该压缩包本次会话再也无法入队。
                    If Not String.IsNullOrEmpty(Me._pathElementoActual) Then _esperandoVolumenes.Remove(Me._pathElementoActual)
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
            ' 路径是等待中的非首卷时,还没有可解压的归档:保持等待态并重新登记,
            ' 不能把"首卷未到"误报成失败。
            If _currentDeferred AndAlso IsPotentialMultiVolumePath(_pathElementoActual) Then
                DeferCurrentElement("First volume or remaining volumes are not on disk yet.")
                elementHandled = True
                Exit Sub
            End If
            For Each codigo As String In _currentCodes
                RaiseEvent DescompresionFinalizada(codigo, False, _
                    "Invalid archive path (file not found): '" & _pathElementoActual & "'")
            Next

            Dim codigoDescartado As String = _codigoElementoActual
            SyncLock Mutex
                If Not String.IsNullOrEmpty(Me._pathElementoActual) Then _esperandoVolumenes.Remove(Me._pathElementoActual)
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

        Dim codigoActual As String = _codigoElementoActual
        Dim rutaActual As String = _pathElementoActual
        Dim codigosActuales As New Generic.HashSet(Of String)(_currentCodes, StringComparer.OrdinalIgnoreCase)
        If Not String.IsNullOrEmpty(codigoActual) Then codigosActuales.Add(codigoActual)
        Dim diferido As Boolean = False

        If extractOk Then
            Log.WriteInfo("Element '" & codigoActual & "' extracted in " & Sw.ElapsedMilliseconds & "ms")
            ' P2-4: make the collision policy visible (no silent overwrites anymore).
            If desc.CollisionsKeptOriginal > 0 Then
                Log.WriteInfo("P2-4: " & desc.CollisionsKeptOriginal & " pre-existing file(s) in the extraction folder were kept under a ""(n)"" suffix instead of being overwritten.")
            End If
            Dim completedSeal As VolumeLedgerEntry = SelloArchivo(rutaActual)
            SyncLock Mutex
                _rutasCompletadas(rutaActual) = completedSeal
                _esperandoVolumenes.Remove(rutaActual)
            End SyncLock
        Else
            Log.WriteWarning("Element '" & codigoActual & "' extraction failed or cancelled.")
            ' 多卷缺卷不是真失败:登记等待并定期重试,不报红错。每有新卷落盘就刷新窗口,
            ' 窗口耗尽(确实没有新卷再来)才按真失败上报。取消不走等待路径。
            If Not Cancel AndAlso Not desc.CancelRequested AndAlso Not IsCancelRequested() AndAlso
               IsPotentialMultiVolumePath(rutaActual) AndAlso IsIncompleteVolumeError(rutaActual, extractErrorMessage) Then
                ' 目录枚举必须在锁外:Mutex 被 UI 的 300ms 刷新和下载线程的 AgregarElemento 争用,
                ' 圈住 GetFiles 会让大下载目录把界面拖停。卷数只是启发式,读到略旧的值无害。
                Dim present As Integer = CountPresentVolumes(rutaActual)
                SyncLock Mutex
                    Dim w As EsperandoVolumenes = Nothing
                    If _esperandoVolumenes.TryGetValue(rutaActual, w) Then
                        If present > w.VolumenesVistos Then
                            w.VolumenesVistos = present
                            w.Limite = Now.AddMinutes(VolumeWaitWindowMinutes)
                        End If
                        If Now < w.Limite Then
                            w.ProximoIntento = Now.AddSeconds(VolumeRetryIntervalSeconds)
                            diferido = True
                        End If
                    Else
                        _esperandoVolumenes(rutaActual) = New EsperandoVolumenes With {
                            .Codes = New Generic.HashSet(Of String)(codigosActuales, StringComparer.OrdinalIgnoreCase),
                            .Password = _passwordElementoActual,
                            .CreateDirectory = _crearDirectorio,
                            .ProximoIntento = Now.AddSeconds(VolumeRetryIntervalSeconds),
                            .VolumenesVistos = present,
                            .Limite = Now.AddMinutes(VolumeWaitWindowMinutes)}
                        diferido = True
                    End If
                    If diferido AndAlso _esperandoVolumenes.TryGetValue(rutaActual, w) Then w.Codes.UnionWith(codigosActuales)
                End SyncLock
            End If
        End If

        If diferido Then
            Log.WriteInfo("Element '" & codigoActual & "' is a multi-volume archive still waiting for the remaining volumes; retry scheduled, no failure reported.")
            For Each codigo As String In codigosActuales
                RaiseEvent DescompresionDiferida(codigo)
            Next
        Else
            SyncLock Mutex
                _esperandoVolumenes.Remove(rutaActual)
            End SyncLock
            For Each codigo As String In codigosActuales
                RaiseEvent DescompresionFinalizada(codigo, extractOk, extractErrorMessage)
            Next
        End If

        ' Hemos terminado
        SyncLock Mutex
            Me._pathElementoActual = Nothing
            Me._passwordElementoActual = Nothing
            Me._codigoElementoActual = Nothing
            Me._currentDeferred = False
            Me._currentCodes.Clear()
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
    Public Event DescompresionDiferida(ByVal Code As String)

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



    ''' <summary>统计与首卷同组、已落盘的分卷数。非多卷归档返回 1。
    ''' 下载中的分卷用 .megadownloader-*.part 临时名,下完才改回真名,
    ''' 所以"真名存在"就等于"该卷已下完"。</summary>
    Private Shared Function CountPresentVolumes(ByVal firstVolumePath As String) As Integer
        Try
            Dim dir As String = Path.GetDirectoryName(firstVolumePath)
            Dim name As String = Path.GetFileName(firstVolumePath)
            Dim pattern As String = Nothing
            Dim m7z As System.Text.RegularExpressions.Match = System.Text.RegularExpressions.Regex.Match(name, "^(?<base>.+)\.7z\.\d+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            If m7z.Success Then
                pattern = "^" & System.Text.RegularExpressions.Regex.Escape(m7z.Groups("base").Value) & "\.7z\.\d+$"
            Else
                Dim mRar As System.Text.RegularExpressions.Match = System.Text.RegularExpressions.Regex.Match(name, "^(?<base>.+)\.part\d+\.rar$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                If mRar.Success Then
                    pattern = "^" & System.Text.RegularExpressions.Regex.Escape(mRar.Groups("base").Value) & "\.part\d+\.rar$"
                Else
                    Dim mOldRar As System.Text.RegularExpressions.Match = System.Text.RegularExpressions.Regex.Match(name, "^(?<base>.+)\.(?:rar|r\d+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                    If mOldRar.Success Then
                        pattern = "^" & System.Text.RegularExpressions.Regex.Escape(mOldRar.Groups("base").Value) & "\.(?:rar|r\d+)$"
                    End If
                End If
            End If
            If pattern Is Nothing OrElse String.IsNullOrEmpty(dir) Then Return 1
            Dim count As Integer = 0
            For Each f As String In Directory.GetFiles(dir)
                If System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(f), pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase) Then count += 1
            Next
            Return Math.Max(1, count)
        Catch ex As Exception
            Log.WriteWarning("Could not count archive volumes for '" & firstVolumePath & "': " & Log.SafeException(ex))
            Return 1
        End Try
    End Function

    ''' <summary>归档的"长度 + 修改时间 + 前 8KB 内容摘要"共同作为已解压台账版本。
    ''' 单看 LastWriteTimeUtc 会因粗时间粒度或复制时保留 mtime 把重下的不同归档误判成旧文件。</summary>
    Private Shared Function SelloArchivo(ByVal path As String) As VolumeLedgerEntry
        Dim sello As New VolumeLedgerEntry With {.LastWriteUtc = DateTime.MinValue}
        Try
            Dim fi As New FileInfo(path)
            If Not fi.Exists Then Return sello
            sello.Length = fi.Length
            sello.LastWriteUtc = fi.LastWriteTimeUtc
            Using fs As New FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite Or FileShare.Delete)
                Dim buffer(CInt(Math.Min(8192L, Math.Max(1L, fi.Length))) - 1) As Byte
                Dim read As Integer = fs.Read(buffer, 0, buffer.Length)
                Using sha As New System.Security.Cryptography.SHA256Managed()
                    sha.TransformBlock(buffer, 0, read, Nothing, 0)
                    sha.TransformFinalBlock(New Byte() {}, 0, 0)
                    sello.ContentHash = BitConverter.ToUInt64(sha.Hash, 0)
                End Using
            End Using
        Catch ex As Exception
            Return New VolumeLedgerEntry With {.LastWriteUtc = DateTime.MinValue}
        End Try
        Return sello
    End Function

    ''' <summary>是否为分卷归档。只有分卷才允许"等齐再解压"的延迟;
    ''' 单卷压缩包打不开就是真损坏/密码错,必须立刻报错,不能拖到窗口耗尽。</summary>
    Private Shared Function IsMultiVolumePath(ByVal path As String) As Boolean
        If String.IsNullOrEmpty(path) Then Return False
        Dim name As String = IO.Path.GetFileName(path)
        Return System.Text.RegularExpressions.Regex.IsMatch(name, "\.7z\.\d+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase) OrElse
               System.Text.RegularExpressions.Regex.IsMatch(name, "\.part\d+\.rar$", System.Text.RegularExpressions.RegexOptions.IgnoreCase) OrElse
               System.Text.RegularExpressions.Regex.IsMatch(name, "\.r\d+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
    End Function

    ''' <summary>是否为"分卷没齐/打不开归档"这类可等待的错误,而非密码错、损坏等真失败。
    ''' 7-Zip 对缺后续卷有明确的 "Unexpected end of archive" 系列提示;
    ''' "Cannot open the file as archive" 同样可能是首卷损坏或密码错误,不能一概等 30 分钟。
    ''' 只有明确证据表明归档可读但缺卷时才允许延迟。</summary>
    Private Shared Function IsIncompleteVolumeError(ByVal path As String, ByVal message As String) As Boolean
        If String.IsNullOrEmpty(message) Then Return False
        Dim localized As String = Language.GetText("Archive is incomplete: one or more volumes are missing")
        If Not String.IsNullOrEmpty(localized) AndAlso message.IndexOf(localized, StringComparison.OrdinalIgnoreCase) >= 0 Then Return True
        If message.IndexOf("Unexpected end of archive", StringComparison.OrdinalIgnoreCase) < 0 Then Return False
        ' A .7z.NNN or .partN.rar first volume that cannot be recognized may still be
        ' a genuinely corrupt single-volume file. Require at least one sibling volume,
        ' or the CLI's explicit missing-volume diagnostic, before entering the wait window.
        Return CountPresentVolumes(path) > 1
    End Function

    ''' <summary>把到期的"等分卷"项放回待办队列。等待项不留在 _colaElementos 里,
    ''' 否则 Ocupado() 恒为 True,关闭时会误报"正在解压,有损坏风险"。
    ''' 记录本身保留(卷数/窗口要跨多次重试累计),用 ProximoIntento=MaxValue 表示已入队。</summary>
    Private Sub DeferCurrentElement(ByVal reason As String)
        Dim rutaActual As String = _pathElementoActual
        Dim codigosActuales As New Generic.HashSet(Of String)(_currentCodes, StringComparer.OrdinalIgnoreCase)
        If Not String.IsNullOrEmpty(_codigoElementoActual) Then codigosActuales.Add(_codigoElementoActual)
        ' Directory enumeration must stay outside the shared controller lock: the UI
        ' refresh and download-completion handlers contend for Mutex every few hundred ms.
        Dim present As Integer = CountPresentVolumes(rutaActual)
        Dim password As String = _passwordElementoActual
        Dim createDirectory As Boolean = _crearDirectorio
        Dim w As EsperandoVolumenes = Nothing
        SyncLock Mutex
            If Not _esperandoVolumenes.TryGetValue(rutaActual, w) Then
                w = New EsperandoVolumenes With {
                    .Codes = New Generic.HashSet(Of String)(codigosActuales, StringComparer.OrdinalIgnoreCase),
                    .Password = password,
                    .CreateDirectory = createDirectory,
                    .VolumenesVistos = present,
                    .Limite = Now.AddMinutes(VolumeWaitWindowMinutes)}
                _esperandoVolumenes(rutaActual) = w
            Else
                w.Codes.UnionWith(codigosActuales)
                If present > w.VolumenesVistos Then
                    w.VolumenesVistos = present
                    w.Limite = Now.AddMinutes(VolumeWaitWindowMinutes)
                End If
                w.Limite = If(w.Limite > Now, w.Limite, Now.AddMinutes(VolumeWaitWindowMinutes))
            End If
            w.ProximoIntento = Now.AddSeconds(VolumeRetryIntervalSeconds)
            Me._pathElementoActual = Nothing
            Me._passwordElementoActual = Nothing
            Me._codigoElementoActual = Nothing
            Me._currentDeferred = False
            Me._currentCodes.Clear()
        End SyncLock
        For Each codigo As String In codigosActuales
            RaiseEvent DescompresionDiferida(codigo)
        Next
        Log.WriteInfo("Deferred '" & rutaActual & "': " & reason)
    End Sub

    Private Sub PromoteWaitingVolumes()
        SyncLock Mutex
            If _esperandoVolumenes.Count = 0 Then Return
            Dim due As New Generic.List(Of String)
            For Each kvp As KeyValuePair(Of String, EsperandoVolumenes) In _esperandoVolumenes
                If Now >= kvp.Value.ProximoIntento Then due.Add(kvp.Key)
            Next
            For Each path As String In due
                Dim w As EsperandoVolumenes = _esperandoVolumenes(path)
                w.ProximoIntento = Date.MaxValue
                For Each code As String In w.Codes
                    _colaElementos(code) = New QueueItem With {.Path = path, .Password = w.Password, .CreateDirectory = w.CreateDirectory, .Deferred = True}
                Next
            Next
        End SyncLock
    End Sub

    Public Function AgregarElemento(ByVal Code As String, ByVal Path As String, ByVal CrearDirectorio As Boolean, Password As String) As Boolean
        If String.IsNullOrEmpty(Code) OrElse String.IsNullOrEmpty(Path) Then Return False
        Dim originalPath As String = IO.Path.GetFullPath(Path)
        If Not File.Exists(originalPath) Then Return False
        Dim isVolume As Boolean = IsPotentialMultiVolumePath(originalPath)
        Dim normalizedPath As String = NormalizeQueuePath(originalPath)
        Dim extension As String = IO.Path.GetExtension(normalizedPath).ToLowerInvariant()
        If Not _ExtensionesSoportadas.Contains(extension.TrimStart("."c)) AndAlso
           Not Regex.IsMatch(normalizedPath, "\.7z\.\d+$", RegexOptions.IgnoreCase) AndAlso
           Not Regex.IsMatch(normalizedPath, "\.r\d+$", RegexOptions.IgnoreCase) Then Return False

        ' SelloArchivo performs file I/O. Keep it outside the short global queue lock;
        ' every persistent decision below compares and stores the value under Mutex.
        Dim pendingSeal As VolumeLedgerEntry = New VolumeLedgerEntry With {.LastWriteUtc = DateTime.MinValue}
        If Not isVolume Then pendingSeal = SelloArchivo(normalizedPath)

        SyncLock Mutex
            If String.Equals(_pathElementoActual, normalizedPath, StringComparison.OrdinalIgnoreCase) Then Return True
            Dim selloCompletado As VolumeLedgerEntry
            If _rutasCompletadas.TryGetValue(normalizedPath, selloCompletado) AndAlso
               selloCompletado.LastWriteUtc <> DateTime.MinValue AndAlso
               selloCompletado.Length = pendingSeal.Length AndAlso
               selloCompletado.LastWriteUtc = pendingSeal.LastWriteUtc AndAlso
               selloCompletado.ContentHash = pendingSeal.ContentHash AndAlso Not isVolume Then Return True
            Dim w As EsperandoVolumenes = Nothing
            If _esperandoVolumenes.TryGetValue(normalizedPath, w) Then
                w.Codes.Add(Code)
                If isVolume Then w.Limite = Now.AddMinutes(VolumeWaitWindowMinutes)
                _rutasCompletadas.Remove(normalizedPath)
                Return True
            End If
            _rutasCompletadas.Remove(normalizedPath)
            _colaElementos(Code) = New QueueItem With {
                .Path = normalizedPath, .CreateDirectory = CrearDirectorio, .Password = Password,
                .Deferred = isVolume AndAlso Not String.Equals(normalizedPath, originalPath, StringComparison.OrdinalIgnoreCase)}
            Return True
        End SyncLock
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
                GetController.ClearCancelIfIdle()

                ' P1-2:逐次迭代独立兜底。此前整个 While 被一个 Try 包住,任何异常
                ' (哪怕只是 PonerElementoAProcesar 的一次瞬时失败)都会跳出循环 →
                ' Catch 只写日志 → DoWork 正常返回 → RunWorkerCompleted 只置标志不重启 →
                ' worker 静默死亡,解压队列永久报废到重启进程。
                ' 现在单次迭代出错只跳过本次,worker 继续消费队列。
                Try
                    GetController.PromoteWaitingVolumes()
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
        Private ReadOnly PreservedCopies As New Generic.List(Of KeyValuePair(Of String, String))
        Public CancelRequested As Boolean

        ' P2-4: collision accounting for the extraction folder. Read by the controller
        ' thread after extractThread.Join (the join provides the happens-before edge).
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
            ' Resolve collisions first; never overwrite a file created in the meantime.
            opts.Overwrite = False
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
                ' Managed ZIP/RAR/TAR extraction also uses ResolveEntryDestination.
                ' If a later entry fails, restore files that this run renamed aside
                ' instead of leaving the user's originals under "(n)" suffixes.
                PreservedCopies.Clear()
                Dim committed As Boolean = False
                Try
                ' 7z archives (single .7z or multipart .7z.001/.002/...) are handled by
                ' the external 7-Zip CLI — SharpCompress does not support the 7z container
                ' format at all, which made every .7z extraction fail. Prefer a system
                ' 7-Zip install; fall back to the embedded 7zr.exe (public domain).
                If Is7zContainer(PathFichero) Then
                    Extract7z()
                    committed = True
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
                                                If reader.Entry.IsDirectory Then
                                                    Directory.CreateDirectory(PathGuard.GetSafeArchiveEntryPath(PathExtraccion, reader.Entry.Key))
                                                Else
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
                                                    If reader.Entry.IsDirectory Then
                                                        Directory.CreateDirectory(PathGuard.GetSafeArchiveEntryPath(PathExtraccion, reader.Entry.Key))
                                                    Else
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
                                ' P-09: archive.Entries can be a lazy enumerator that re-parses
                                ' the whole archive on every pass. Materialize it once and reuse.
                                Dim entries As New Generic.List(Of IArchiveEntry)(archive.Entries)
                                SyncLock Mutex
                                    c._TamanoTotal = 0
                                    c._FicActTamanoTotal = 0
                                    c._TamanoTotalExtraido = 0
                                    c._FicActExtraido = 0
                                    c._FicActNombre = ""
                                    For Each entry As IArchiveEntry In entries
                                        c._TamanoTotal += entry.Size
                                    Next
                                    EnsureExtractWithinQuota(c._TamanoTotal.GetValueOrDefault(), entries.Count)
                                End SyncLock

                                Dim entryKeys As New Generic.List(Of String)(entries.Count)
                                For Each entry As IArchiveEntry In entries
                                    entryKeys.Add(entry.Key)
                                Next
                                PathGuard.ValidateArchiveEntries(PathExtraccion, entryKeys)

                                For Each entry As IArchiveEntry In entries
                                    If CancelRequested Then
                                        Throw New OperationCanceledException("Extraction cancelled.")
                                    End If
                                    If entry.IsDirectory Then
                                        Directory.CreateDirectory(PathGuard.GetSafeArchiveEntryPath(PathExtraccion, entry.Key))
                                    Else
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
                committed = True
                PreservedCopies.Clear()
            Catch ex As Exception
                Exception = ex
            Finally
                If Not committed Then RestorePreservedCopies(PathExtraccion)
            End Try
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
            ' -sccUTF-8 必须与 Pass2(x -y -aou -bd -sccUTF-8)一致:列表输出的条目名要与
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
                        ' Include empty entries when preserving pre-existing targets.
                        If currentEntry IsNot Nothing AndAlso Not entrySizes.ContainsKey(currentEntry) Then
                            entrySizes(currentEntry) = sizeVal
                        End If
                        If totalUncompressed > MaxExtractTotalBytes Then Exit For
                    End If
                End If
            Next
            EnsureExtractWithinQuota(totalUncompressed, entryKeys.Count)
            PathGuard.ValidateArchiveEntries(PathExtraccion, entryKeys)

            ' 7z CLI 必须先写进空白临时目录,再以托管方式提交:外部 CLI 不认识
            ' PathGuard 截断后的目标名,也无法参与可回滚的预改名事务。
            ' CLI 输出按"规范化原名 → 安全相对路径"的映射搬回;长名截断带稳定哈希,
            ' 既避免两个长条目撞同一文件,也保证同名旧文件用托管后缀规则保留。
            Dim staging As String = IO.Path.Combine(PathExtraccion, ".megadownloader-extract-" & Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory(staging)
            Dim targetKeys As New Generic.List(Of String)
            Dim relativeKeys As New Generic.Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
            For Each key As String In entryKeys
                Dim sourceRelative As String = key.Replace("/"c, Path.DirectorySeparatorChar).Replace("\"c, Path.DirectorySeparatorChar)
                Dim targetRelative As String = PathGuard.GetRelativePath(PathExtraccion, PathGuard.GetSafeArchiveEntryPath(PathExtraccion, key))
                If relativeKeys.ContainsKey(sourceRelative) OrElse targetKeys.Contains(targetRelative, StringComparer.OrdinalIgnoreCase) Then
                    Throw New IOException("Archive contains ambiguous entry names: " & key)
                End If
                relativeKeys(sourceRelative) = targetRelative
                targetKeys.Add(targetRelative)
            Next
            Dim c As DescompresorController = DescompresorController.GetController
            Dim committed As Boolean = False
            Try
                SyncLock Mutex
                    c._TamanoTotal = totalUncompressed
                    c._FicActTamanoTotal = 0
                    c._TamanoTotalExtraido = 0
                    c._FicActExtraido = 0
                    c._FicActNombre = PathFichero
                End SyncLock
                Dim args As New System.Text.StringBuilder()
                args.Append("x -y -bd -sccUTF-8 -o""").Append(staging).Append("""")
                args.Append(passwordArg).Append(" -- """).Append(PathFichero).Append(""""c)
                RunSevenZip(cli, args.ToString(), checkCancel:=True)
                For Each kvp As KeyValuePair(Of String, String) In relativeKeys
                    Dim source As String = PathGuard.GetSafePathUnderRoot(staging, kvp.Key, allowRoot:=False)
                    Dim dest As String = PathGuard.GetSafePathUnderRoot(PathExtraccion, kvp.Value, allowRoot:=False)
                    If Not File.Exists(source) AndAlso Not Directory.Exists(source) Then Throw New IOException("7-Zip did not produce archive entry: " & kvp.Key)
                    If Directory.Exists(source) Then
                        Directory.CreateDirectory(dest)
                    Else
                        Directory.CreateDirectory(Path.GetDirectoryName(dest))
                        Dim preserved As String = ResolveEntryDestination(dest)
                        If Not String.Equals(preserved, dest, StringComparison.OrdinalIgnoreCase) Then Throw New IOException("Could not preserve the existing file before extraction: " & dest)
                        File.Copy(source, dest, False)
                    End If
                Next
                committed = True
            Finally
                If Not committed Then RestorePreservedCopies(PathExtraccion)
                Try
                    If Directory.Exists(staging) Then Directory.Delete(staging, True)
                Catch ex As Exception
                    Log.WriteError("Extraction: could not remove staging directory '" & staging & "': " & Log.SafeException(ex))
                End Try
                SyncLock Mutex
                    c._TamanoTotal = Nothing
                    c._FicActTamanoTotal = Nothing
                    c._TamanoTotalExtraido = Nothing
                    c._FicActExtraido = Nothing
                    c._FicActNombre = Nothing
                End SyncLock
            End Try
        End Sub

        Private Sub RestorePreservedCopies(ByVal root As String)
            For i As Integer = PreservedCopies.Count - 1 To 0 Step -1
                Dim pair As KeyValuePair(Of String, String) = PreservedCopies(i)
                If File.Exists(pair.Value) AndAlso Not File.Exists(pair.Key) Then
                    Try
                        File.Move(pair.Value, pair.Key)
                    Catch ex As Exception
                        Log.WriteError("Extraction: could not restore '" & pair.Value & "': " & Log.SafeException(ex))
                    End Try
                End If
            Next
            PreservedCopies.Clear()
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
            If checkCancel AndAlso CancelRequested Then Throw New OperationCanceledException("Extraction cancelled.")
            Dim psi As New ProcessStartInfo()
            psi.FileName = cliPath
            psi.Arguments = arguments
            psi.UseShellExecute = False
            psi.CreateNoWindow = True
            psi.RedirectStandardOutput = True
            psi.RedirectStandardError = True
            ' Give the CLI an immediate EOF on stdin. A header-encrypted archive with no
            ' password configured makes 7-Zip prompt for one; an inherited open stdin
            ' would keep it waiting for input that this background job cannot provide.
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

                ' Drain both pipes while waiting, so output cannot block cancellation.
                Dim stdoutTask As Threading.Tasks.Task(Of String) = proc.StandardOutput.ReadToEndAsync()
                Dim stderrTask As Threading.Tasks.Task(Of String) = proc.StandardError.ReadToEndAsync()

                ' Cooperative cancellation while waiting for the CLI to finish.
                While Not proc.WaitForExit(250)
                    If checkCancel AndAlso CancelRequested Then
                        Try
                            proc.Kill()
                        Catch
                        End Try
                        proc.WaitForExit(5000)
                        Try
                            Threading.Tasks.Task.WaitAll(New Threading.Tasks.Task() {stdoutTask, stderrTask}, 5000)
                        Catch
                            ' Pipe closure must not hide the cancellation result.
                        End Try
                        Throw New OperationCanceledException("Extraction cancelled.")
                    End If
                End While

                Dim stdout As String = stdoutTask.GetAwaiter().GetResult()
                Dim stderr As String = stderrTask.GetAwaiter().GetResult()

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
        ''' Preserve existing files under a free suffix, regardless of their length.
        ''' Returns the path the entry must be WRITTEN to: the original name when the
        ''' existing file was renamed aside (or no file existed), or the free
        ''' "name (2).ext"-style path when the rename itself failed and the existing
        ''' file must stay untouched.
        ''' </summary>
        Private Function ResolveEntryDestination(ByVal dest As String) As String
            If Not System.IO.File.Exists(dest) Then Return dest

            Dim kept As String = GetNonCollidingPath(dest)
            Try
                System.IO.File.Move(dest, kept)
                PreservedCopies.Add(New KeyValuePair(Of String, String)(dest, kept))
                CollisionsKeptOriginal += 1
                Log.WriteWarning("Extraction: preserving existing file '" & dest & "' as '" & kept & "'.")
                ' The rename freed the original name — that is where the entry goes.
                ' (Returning `kept` here would overwrite the very file we just
                ' preserved.)
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
            dest = ResolveEntryDestination(dest)
            Dim parentDir As String = Path.GetDirectoryName(dest)
            If Not String.IsNullOrEmpty(parentDir) Then
                Directory.CreateDirectory(parentDir)
            End If
            entry.WriteToFile(dest, ExtractOpts())
        End Sub

        Private Sub WriteSafeEntry(ByVal reader As IReader, ByVal extractionRoot As String, ByVal entryKey As String)
            Dim dest As String = PathGuard.GetSafeArchiveEntryPath(extractionRoot, entryKey)
            dest = ResolveEntryDestination(dest)
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
