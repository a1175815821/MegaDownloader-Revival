Imports System.ComponentModel
Imports SharpCompress.Archives
Imports SharpCompress.Readers
Imports SharpCompress.Archives.Rar
Imports SharpCompress.Archives.Zip
Imports SharpCompress.Readers.Rar
Imports SharpCompress.Common
Imports System.IO
Imports System.Text.RegularExpressions


Public Class DescompresorController

    Private Class QueueItem
        Public Key As String
        Public Source As String
        Public Password As String
        Public CreateDirectory As Boolean
        Public ReadOnly Codes As New HashSet(Of String)(StringComparer.Ordinal)
        Public ReadOnly TaskCallbacks As New Dictionary(Of String, Action(Of Boolean, Boolean, String))(StringComparer.Ordinal)
        Public RetryUtc As Date
        Public DeadlineUtc As Date
        Public VolumeCount As Integer
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

    Private ReadOnly _colaElementos As New Dictionary(Of String, QueueItem)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly _esperandoVolumenes As New Dictionary(Of String, QueueItem)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly _rutasCompletadas As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
    Private _current As QueueItem
    Private _currentPath As String
    Private _cancelRequested As Boolean
    Private Const MaxCompletedArchives As Integer = 1024
    Private Shared ReadOnly SupportedExtensions As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {".7z", ".rar", ".tar", ".zip"}

    ' En estas variables (_TamanoTotalExtraido, etc) se guardará el progreso actual

    Friend _TamanoTotal As System.Nullable(Of Long) ' Tamaño total de los ficheros cuando se descompriman
    Friend _TamanoTotalExtraido As System.Nullable(Of Long) ' Tamaño total de los ficheros ya descomprimidos completamente
    Friend _FicActTamanoTotal As System.Nullable(Of Long) ' Tamaño total del fichero que se está descomprimiendo
    Friend _FicActExtraido As System.Nullable(Of Long) ' Bytes extraidos del fichero que se está descomprimiendo
    Friend _FicActNombre As String ' Nombre del fichero que se está descomprimiendo



    Private Sub New()
    End Sub

    Private Shared ReadOnly rxVolumePath As New Regex("^(?<base>.+)\.(?:7z\.(?<sz>[0-9]+)|part(?<rp>[0-9]+)\.rar|(?<rar>rar)|r[0-9]+)$",
                                                      RegexOptions.IgnoreCase Or RegexOptions.CultureInvariant Or RegexOptions.Compiled)

    Private Shared Function ArchiveKey(path As String, volume As Match) As String
        If Not volume.Success Then Return path
        ' Use the group and naming format even when the first volume is absent.
        Dim suffix As String = If(volume.Groups("sz").Success, ".7z.*", If(volume.Groups("rp").Success, ".part*.rar", ".rar"))
        Return IO.Path.Combine(IO.Path.GetDirectoryName(path), volume.Groups("base").Value & suffix)
    End Function

    Private Shared Function FirstVolume(source As String, volumes As String(), volume As Match) As String
        If Not volume.Success Then Return source
        If Not volume.Groups("sz").Success AndAlso Not volume.Groups("rp").Success Then Return ArchiveKey(source, volume)
        Dim numberGroup As String = If(volume.Groups("sz").Success, "sz", "rp")
        Return volumes.FirstOrDefault(Function(path)
                                          Dim number As String = rxVolumePath.Match(IO.Path.GetFileName(path)).Groups(numberGroup).Value
                                          Return number.TrimStart("0"c) = "1"
                                      End Function)
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

    Private Function PonerElementoAProcesar() As Boolean
        SyncLock Mutex
            If _cancelRequested OrElse _current IsNot Nothing OrElse _colaElementos.Count = 0 Then Return False
            Dim key As String = _colaElementos.Keys(0)
            _current = _colaElementos(key)
            _currentPath = _current.Source
            _colaElementos.Remove(key)
            Return True
        End SyncLock
    End Function

    Private Sub ProcesarElemento(worker As BackgroundWorker)
        Dim item As QueueItem
        SyncLock Mutex
            item = _current
        End SyncLock
        If item Is Nothing Then Return
        Try
            If worker.CancellationPending OrElse IsCancelRequested() Then
                FinishCurrent(item, False, "Extraction cancelled.")
                Return
            End If

            Dim volume As Match = rxVolumePath.Match(Path.GetFileName(item.Source))
            Dim volumes As String() = PresentVolumes(item.Source, volume)
            Dim first As String = FirstVolume(item.Source, volumes, volume)
            If first Is Nothing OrElse Not File.Exists(first) Then
                If volume.Success Then
                    DeferCurrentElement(item, volumes.Length, "First archive volume is missing.")
                Else
                    FinishCurrent(item, False, "Invalid archive path (file not found): " & item.Source)
                End If
                Return
            End If

            Dim inputSeal As String = ArchiveSeal(volumes)
            SyncLock Mutex
                _currentPath = first
            End SyncLock

            Dim output As String = Path.GetDirectoryName(first)
            If item.CreateDirectory Then
                Dim folderName As String = If(volume.Success, volume.Groups("base").Value, Path.GetFileNameWithoutExtension(first))
                output = PathGuard.GetSafePathUnderRoot(output, PathGuard.SanitizeFileName(folderName, "extracted"), allowRoot:=False)
            End If

            Dim extractor As New Descompressor(first, output, item.Password)
            Dim extractionThread As New Threading.Thread(AddressOf extractor.Extract) With {
                .Priority = Threading.ThreadPriority.BelowNormal, .IsBackground = True}
            extractionThread.Start()
            Dim cancellationLogged As Boolean = False
            While Not extractionThread.Join(250)
                If Not worker.CancellationPending AndAlso Not IsCancelRequested() Then Continue While
                extractor.CancelRequested = True
                If Not cancellationLogged Then
                    cancellationLogged = True
                    Log.WriteInfo("Waiting for extraction to cancel for " & first)
                End If
            End While
            If extractor.Exception Is Nothing Then
                FinishCurrent(item, True, "", inputSeal)
            ElseIf Not worker.CancellationPending AndAlso Not IsCancelRequested() AndAlso
                   (TypeOf extractor.Exception Is IncompleteArchiveException OrElse
                    TypeOf extractor.Exception Is SharpCompress.Common.IncompleteArchiveException OrElse
                    (volume.Success AndAlso IsIncompleteVolumeError(extractor.Exception.Message))) Then
                DeferCurrentElement(item, volumes.Length, extractor.Exception.Message)
            Else
                FinishCurrent(item, False, extractor.Exception.Message)
            End If
        Catch ex As Exception
            Log.WriteError("Unexpected error while extracting " & item.Source & ": " & Log.SafeException(ex))
            FinishCurrent(item, False, "Unexpected extraction error: " & ex.Message)
        End Try
    End Sub

    Private Sub FinishCurrent(item As QueueItem, success As Boolean, message As String, Optional seal As String = Nothing)
        Dim codes As String()
        SyncLock Mutex
            If Not Object.ReferenceEquals(_current, item) Then Return
            If success AndAlso seal IsNot Nothing Then
                If _rutasCompletadas.Count >= MaxCompletedArchives Then _rutasCompletadas.Remove(_rutasCompletadas.Keys(0))
                _rutasCompletadas(item.Key) = seal
            End If
            codes = item.Codes.ToArray()
            _current = Nothing
            _currentPath = Nothing
        End SyncLock
        NotifyDownloads(codes, item.TaskCallbacks, False, success, message)
    End Sub

    Private Sub NotifyDownloads(codes As IEnumerable(Of String), callbacks As Dictionary(Of String, Action(Of Boolean, Boolean, String)),
                                deferred As Boolean, success As Boolean, message As String)
        For Each code As String In codes
            Dim callback As Action(Of Boolean, Boolean, String) = Nothing
            If callbacks IsNot Nothing Then callbacks.TryGetValue(code, callback)
            NotifyDownload(code, callback, deferred, success, message)
        Next
    End Sub

    Private Sub NotifyDownload(code As String, callback As Action(Of Boolean, Boolean, String), deferred As Boolean, success As Boolean, message As String)
        Try
            If callback IsNot Nothing Then
                callback(deferred, success, message)
            ElseIf deferred Then
                RaiseEvent DescompresionDiferida(code)
            Else
                RaiseEvent DescompresionFinalizada(code, success, message)
            End If
        Catch ex As Exception
            Log.WriteError("Could not report extraction state for " & code & ": " & Log.SafeException(ex))
        End Try
    End Sub


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
                Return _currentPath
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



    Private Shared Function PresentVolumes(path As String, volume As Match) As String()
        If Not volume.Success Then Return If(File.Exists(path), New String() {path}, New String() {})

        Dim baseName As String = Regex.Escape(volume.Groups("base").Value)
        Dim suffix As String = If(volume.Groups("sz").Success, "\.7z\.[0-9]+",
                                 If(volume.Groups("rp").Success, "\.part[0-9]+\.rar", "\.(?:rar|r[0-9]+)"))
        Dim group As New Regex("^" & baseName & suffix & "$", RegexOptions.IgnoreCase Or RegexOptions.CultureInvariant)
        Return Directory.EnumerateFiles(IO.Path.GetDirectoryName(path)).
            Where(Function(candidate) group.IsMatch(IO.Path.GetFileName(candidate))).
            OrderBy(Function(candidate) candidate, StringComparer.OrdinalIgnoreCase).ToArray()
    End Function

    Private Shared Function ArchiveSeal(volumes As String()) As String
        If volumes.Length = 0 Then Return Nothing
        Dim seal As New System.Text.StringBuilder
        Dim buffer(8191) As Byte
        Try
            Using sha As System.Security.Cryptography.SHA256 = System.Security.Cryptography.SHA256.Create()
                For Each path As String In volumes
                    Using stream As New FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)
                        Dim info As New FileInfo(path)
                        Dim count As Integer = 0
                        While count < buffer.Length
                            Dim got As Integer = stream.Read(buffer, count, buffer.Length - count)
                            If got = 0 Then Exit While
                            count += got
                        End While
                        seal.Append(info.Name.ToUpperInvariant()).Append("|"c).Append(info.Length).Append("|"c).
                            Append(info.LastWriteTimeUtc.Ticks).Append("|"c).Append(Convert.ToBase64String(sha.ComputeHash(buffer, 0, count))).AppendLine()
                    End Using
                Next
            End Using
        Catch ex As IOException
            Return Nothing
        Catch ex As UnauthorizedAccessException
            Return Nothing
        End Try
        Return seal.ToString()
    End Function

    Private Class IncompleteArchiveException
        Inherits IOException

        Public Sub New(message As String)
            MyBase.New(message)
        End Sub
    End Class

    Private Shared Function IsIncompleteVolumeError(message As String) As Boolean
        Return message.IndexOf("Unexpected end of archive", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
               message.IndexOf("Missing volume", StringComparison.OrdinalIgnoreCase) >= 0
    End Function

    Private Sub DeferCurrentElement(item As QueueItem, present As Integer, reason As String)
        Dim deferred As Boolean = False
        Dim codes As String()
        Dim callbacks As Dictionary(Of String, Action(Of Boolean, Boolean, String))
        SyncLock Mutex
            If Not Object.ReferenceEquals(_current, item) Then Return
            Dim utc As Date = Date.UtcNow
            If item.DeadlineUtc = Date.MinValue OrElse present > item.VolumeCount Then
                item.VolumeCount = present
                item.DeadlineUtc = utc.AddMinutes(VolumeWaitWindowMinutes)
            End If
            If Not _cancelRequested AndAlso utc < item.DeadlineUtc AndAlso item.Codes.Count > 0 Then
                item.RetryUtc = utc.AddSeconds(VolumeRetryIntervalSeconds)
                _esperandoVolumenes(item.Key) = item
                deferred = True
            End If
            codes = item.Codes.ToArray()
            callbacks = New Dictionary(Of String, Action(Of Boolean, Boolean, String))(item.TaskCallbacks, StringComparer.Ordinal)
            _current = Nothing
            _currentPath = Nothing
        End SyncLock
        If deferred Then Log.WriteInfo("Waiting for archive volumes for " & item.Source)
        NotifyDownloads(codes, callbacks, deferred, False, reason)
    End Sub

    Private Sub PromoteWaitingVolumes()
        SyncLock Mutex
            Dim utc As Date = Date.UtcNow
            Dim due As QueueItem() = _esperandoVolumenes.Values.Where(Function(item) item.RetryUtc <= utc).ToArray()
            For Each item As QueueItem In due
                _esperandoVolumenes.Remove(item.Key)
                _colaElementos(item.Key) = item
            Next
        End SyncLock
    End Sub

    Public Function AgregarElemento(ByVal Code As String, ByVal Path As String, ByVal CrearDirectorio As Boolean, Password As String) As Boolean
        Return EnqueueArchive(Code, Path, CrearDirectorio, Password, Nothing)
    End Function

    Friend Function EnqueueArchive(Code As String, Path As String, CrearDirectorio As Boolean, Password As String,
                                   callback As Action(Of Boolean, Boolean, String)) As Boolean
        If String.IsNullOrEmpty(Code) OrElse String.IsNullOrEmpty(Path) Then Return False
        Dim original As String = IO.Path.GetFullPath(Path)
        If Not File.Exists(original) Then Return False
        Dim volume As Match = rxVolumePath.Match(IO.Path.GetFileName(original))
        If Not volume.Success AndAlso Not SupportedExtensions.Contains(IO.Path.GetExtension(original)) Then Return False
        Dim key As String = ArchiveKey(original, volume)

        Dim seal As String = Nothing
        Dim checkedSeal As Boolean = False
        Do
            Dim deferred As Boolean = False
            Dim completed As Boolean = False
            SyncLock Mutex
                If _cancelRequested Then Return False
                Dim item As QueueItem = Nothing
                If _current IsNot Nothing AndAlso String.Equals(_current.Key, key, StringComparison.OrdinalIgnoreCase) Then
                    _current.Codes.Add(Code)
                    If callback IsNot Nothing Then _current.TaskCallbacks(Code) = callback
                    Return True
                End If
                If _colaElementos.TryGetValue(key, item) Then
                    item.Codes.Add(Code)
                    If callback IsNot Nothing Then item.TaskCallbacks(Code) = callback
                    Return True
                End If
                If _esperandoVolumenes.TryGetValue(key, item) Then
                    If item.Codes.Add(Code) Then item.RetryUtc = Date.UtcNow
                    If callback IsNot Nothing Then item.TaskCallbacks(Code) = callback
                    deferred = True
                Else
                    Dim previousSeal As String = Nothing
                    If Not _rutasCompletadas.TryGetValue(key, previousSeal) OrElse checkedSeal Then
                        completed = seal IsNot Nothing AndAlso seal = previousSeal
                        If Not completed Then
                            _rutasCompletadas.Remove(key)
                            item = New QueueItem With {.Key = key, .Source = original, .Password = Password, .CreateDirectory = CrearDirectorio}
                            item.Codes.Add(Code)
                            If callback IsNot Nothing Then item.TaskCallbacks(Code) = callback
                            _colaElementos(key) = item
                            Return True
                        End If
                    End If
                End If
            End SyncLock
            If completed OrElse deferred Then
                NotifyDownload(Code, callback, deferred, completed, "")
                Return True
            End If
            ' Compute a complete group stamp outside the controller lock.
            seal = ArchiveSeal(PresentVolumes(original, volume))
            checkedSeal = True
        Loop
    End Function

    Friend Sub RemoveDownload(code As String)
        SyncLock Mutex
            If _current IsNot Nothing Then
                _current.Codes.Remove(code)
                _current.TaskCallbacks.Remove(code)
            End If
            For Each queue As Dictionary(Of String, QueueItem) In {_colaElementos, _esperandoVolumenes}
                For Each item As QueueItem In queue.Values.ToArray()
                    If item.Codes.Remove(code) AndAlso item.Codes.Count = 0 Then queue.Remove(item.Key)
                    item.TaskCallbacks.Remove(code)
                Next
            Next
        End SyncLock
    End Sub

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
        Dim worker As BackgroundWorker = DirectCast(sender, BackgroundWorker)
        Dim controller As DescompresorController = GetController()
        While Not worker.CancellationPending AndAlso Not controller.IsCancelRequested()
            controller.PromoteWaitingVolumes()
            If controller.PonerElementoAProcesar() Then controller.ProcesarElemento(worker)
            If Not worker.CancellationPending AndAlso Not controller.IsCancelRequested() Then Threading.Thread.Sleep(600)
        End While
        e.Cancel = True
    End Sub

    Public Function GetCola() As Generic.List(Of String)
        SyncLock Mutex
            Return _colaElementos.Values.Concat(_esperandoVolumenes.Values).Select(Function(item) item.Source).ToList()
        End SyncLock
    End Function

    Public Function Ocupado() As Boolean
        SyncLock Mutex
            Return _current IsNot Nothing OrElse _colaElementos.Count > 0
        End SyncLock
    End Function

    Friend ReadOnly Property IsExtracting As Boolean
        Get
            SyncLock Mutex
                Return _current IsNot Nothing
            End SyncLock
        End Get
    End Property


#End Region

#Region "Clase descompresora"




    Private Class Descompressor


        Public Password As String
        Private PathFichero As String
        Private PathExtraccion As String
        Public Exception As Exception
        Private ReadOnly PreservedCopies As New List(Of KeyValuePair(Of String, String))
        Private ReadOnly CreatedFiles As New List(Of String)
        Private ReadOnly CreatedDirectories As New List(Of String)
        Private _cancelRequested As Boolean

        Public Property CancelRequested As Boolean
            Get
                Return Threading.Volatile.Read(_cancelRequested)
            End Get
            Set(value As Boolean)
                Threading.Volatile.Write(_cancelRequested, value)
            End Set
        End Property

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
            Return PathFichero.EndsWith(".7z", StringComparison.OrdinalIgnoreCase) OrElse
                   rxVolumePath.Match(Path.GetFileName(PathFichero)).Groups("sz").Success
        End Function

        Public Sub Extract()
            Try
                CreateOutputDirectory(Path.GetFullPath(PathExtraccion))
                If Is7zContainer(PathFichero) Then
                    Extract7z()
                Else
                    ExtractManaged()
                End If
            Catch ex As Exception
                Exception = ex
                RestoreOutput()
            End Try
        End Sub

        Private Sub ExtractManaged()
            Dim rarVolumes As List(Of String) = Nothing
            If PathFichero.EndsWith(".rar", StringComparison.OrdinalIgnoreCase) Then rarVolumes = DescubrirVolumenesRar(PathFichero)

            Using archive As IArchive = getIArchive(PathFichero, Password, rarVolumes)
                If Not archive.IsComplete Then
                    Throw New IncompleteArchiveException(Language.GetText("Archive is incomplete: one or more volumes are missing"))
                End If

                Dim rar As IRarArchive = TryCast(archive, IRarArchive)
                Dim name As String = Path.GetFileName(PathFichero)
                ' Older RAR volumes may omit the first-volume flag. For a complete old-style
                ' set, discovery already placed the .rar header before .r00.
                Dim olderVolumeSet As Boolean = rxVolumePath.Match(name).Groups("rar").Success AndAlso rarVolumes IsNot Nothing AndAlso rarVolumes.Count > 1
                If rar IsNot Nothing AndAlso rar.IsMultipartVolume() AndAlso Not rar.IsFirstVolume() AndAlso Not olderVolumeSet Then
                    Throw New IncompleteArchiveException(Language.GetText(
                        "This file is not the first volume of a multipart RAR archive, so there is nothing to extract from it alone. Add the first volume (part1.rar) instead."))
                End If

                Dim entries As New List(Of IArchiveEntry)
                Dim destinations As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
                Dim total As Long = 0
                For Each entry As IArchiveEntry In archive.Entries
                    ThrowIfCancelled()
                    If entry.Size < 0 OrElse entry.Size > MaxExtractTotalBytes - total Then
                        Throw New InvalidOperationException("Archive rejected: invalid or excessive uncompressed size.")
                    End If
                    total += entry.Size
                    EnsureExtractWithinQuota(total, entries.Count + 1)
                    Dim dest As String = PathGuard.GetSafeArchiveEntryPath(PathExtraccion, entry.Key)
                    If Not destinations.Add(dest) Then Throw New IOException("Archive contains ambiguous entry names: " & entry.Key)
                    entries.Add(entry)
                Next

                Dim controller As DescompresorController = GetController()
                SyncLock Mutex
                    controller._TamanoTotal = total
                    controller._FicActTamanoTotal = 0
                    controller._TamanoTotalExtraido = 0
                    controller._FicActExtraido = 0
                    controller._FicActNombre = ""
                End SyncLock
                Try
                    If rar IsNot Nothing AndAlso (archive.IsSolid OrElse Not String.IsNullOrEmpty(Password)) Then
                        ExtractRarEntries(rarVolumes)
                    Else
                        For Each entry As IArchiveEntry In entries
                            ExtractEntry(entry, AddressOf entry.OpenEntryStream)
                        Next
                    End If
                Finally
                    ClearExtractionProgress(controller)
                End Try
            End Using
        End Sub

        Private Sub ExtractRarEntries(volumes As List(Of String))
            Dim streams As New List(Of Stream)
            Try
                For Each path As String In volumes
                    streams.Add(File.OpenRead(path))
                Next
                Using reader As IReader = If(streams.Count = 1, RarReader.OpenReader(streams(0), ReaderOpts(Password)),
                                              RarReader.OpenReader(streams, ReaderOpts(Password)))
                    While reader.MoveToNextEntry()
                        ExtractEntry(reader.Entry, Function() reader.OpenEntryStream())
                    End While
                End Using
            Catch ex As Exception When ex.Message.IndexOf("does not support multi-volume decryption", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
                                       ex.Message.IndexOf("Encrypted multipart RAR archives require an external RAR extractor", StringComparison.OrdinalIgnoreCase) >= 0
                Throw New NotSupportedException(Language.GetText("Encrypted multipart RAR archives require an external RAR extractor to decrypt."), ex)
            Catch ex As Exception When volumes.Count > 1 AndAlso Not String.IsNullOrEmpty(Password) AndAlso
                                       Not TypeOf ex Is OperationCanceledException AndAlso Not TypeOf ex Is SharpCompress.Common.IncompleteArchiveException
                Throw New ApplicationException(Language.GetText(
                    "Could not extract the RAR archive. Check that all volumes are present and that the password is correct.") & " (" & ex.Message & ")", ex)
            Finally
                For Each stream As Stream In streams
                    stream.Dispose()
                Next
            End Try
        End Sub

        Private Sub ExtractEntry(entry As IEntry, openEntry As Func(Of Stream))
            ThrowIfCancelled()
            Dim dest As String = PathGuard.GetSafeArchiveEntryPath(PathExtraccion, entry.Key)
            If Not String.IsNullOrEmpty(entry.LinkTarget) Then Throw New IOException("Archive links are not supported.")
            If entry.IsDirectory Then
                CreateOutputDirectory(dest)
                Return
            End If

            Dim controller As DescompresorController = GetController()
            SyncLock Mutex
                controller._FicActNombre = entry.Key
                controller._TamanoTotalExtraido += controller._FicActTamanoTotal
                controller._FicActTamanoTotal = entry.Size
                controller._FicActExtraido = 0
            End SyncLock
            Using source As Stream = openEntry()
                WriteEntry(source, dest, entry.LastModifiedTime)
            End Using
            SyncLock Mutex
                controller._FicActExtraido = entry.Size
            End SyncLock
            DescompresionThrottle.ApplyThrottle()
        End Sub

        Private Shared Sub ClearExtractionProgress(controller As DescompresorController)
            SyncLock Mutex
                controller._TamanoTotal = Nothing
                controller._FicActTamanoTotal = Nothing
                controller._TamanoTotalExtraido = Nothing
                controller._FicActExtraido = Nothing
                controller._FicActNombre = Nothing
            End SyncLock
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

            Dim listing As String = RunSevenZip(cli, "l -ba -slt -sccUTF-8" & passwordArg & " -- """ & PathFichero & """", checkCancel:=True)
            Dim entryKeys As New List(Of String)
            Dim totalUncompressed As Long = 0
            Dim isEntry As Boolean = False
            For Each line As String In listing.Split(New String() {vbCrLf, vbLf, vbCr}, StringSplitOptions.RemoveEmptyEntries)
                If line.StartsWith("Path = ", StringComparison.Ordinal) Then
                    Dim entryPath As String = line.Substring(7)
                    ' Some CLI builds include the archive header before the entries.
                    isEntry = Not String.Equals(entryPath, PathFichero, StringComparison.OrdinalIgnoreCase)
                    If isEntry Then entryKeys.Add(entryPath)
                ElseIf isEntry AndAlso line.StartsWith("Size = ", StringComparison.Ordinal) Then
                    Dim size As Long
                    If Not Long.TryParse(line.Substring(7), size) OrElse size < 0 OrElse size > MaxExtractTotalBytes - totalUncompressed Then
                        Throw New InvalidOperationException("Archive rejected: invalid or excessive uncompressed size.")
                    End If
                    totalUncompressed += size
                End If
                EnsureExtractWithinQuota(totalUncompressed, entryKeys.Count)
            Next

            Dim destinations As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            Dim staging As String = IO.Path.Combine(PathExtraccion, ".megadownloader-extract-" & Guid.NewGuid().ToString("N"))
            Dim paths As New List(Of KeyValuePair(Of String, String))(entryKeys.Count)
            For Each key As String In entryKeys
                Dim source As String = PathGuard.GetArchiveStagingPath(staging, key)
                Dim dest As String = PathGuard.GetSafeArchiveEntryPath(PathExtraccion, key)
                If Not destinations.Add(dest) Then Throw New IOException("Archive contains ambiguous entry names: " & key)
                paths.Add(New KeyValuePair(Of String, String)(source, dest))
            Next

            Dim c As DescompresorController = DescompresorController.GetController
            Try
                Directory.CreateDirectory(staging)
                SyncLock Mutex
                    c._TamanoTotal = totalUncompressed
                    c._FicActTamanoTotal = 0
                    c._TamanoTotalExtraido = 0
                    c._FicActExtraido = 0
                    c._FicActNombre = PathFichero
                End SyncLock
                Dim args As String = "x -y -bd -sccUTF-8 -o""" & staging & """" & passwordArg & " -- """ & PathFichero & """"
                RunSevenZip(cli, args, checkCancel:=True)
                For Each entry As KeyValuePair(Of String, String) In paths
                    Dim source As String = PathGuard.ArchiveIoPath(entry.Key)
                    Dim attributes As FileAttributes = File.GetAttributes(source)
                    If (attributes And FileAttributes.ReparsePoint) <> 0 Then Throw New IOException("Archive links are not supported.")
                    If (attributes And FileAttributes.Directory) <> 0 Then
                        CreateOutputDirectory(entry.Value)
                    Else
                        CreateOutputDirectory(Path.GetDirectoryName(entry.Value))
                        ResolveEntryDestination(entry.Value, requireOriginalName:=True)
                        File.Move(source, PathGuard.ArchiveIoPath(entry.Value))
                        CreatedFiles.Add(entry.Value)
                    End If
                    ThrowIfCancelled()
                Next
            Finally
                Try
                    If Directory.Exists(staging) Then Directory.Delete(PathGuard.ArchiveIoPath(staging), True)
                Catch ex As Exception
                    Log.WriteError("Extraction: could not remove staging directory '" & staging & "': " & Log.SafeException(ex))
                End Try
                ClearExtractionProgress(c)
            End Try
        End Sub

        Private Sub RestoreOutput()
            For i As Integer = CreatedFiles.Count - 1 To 0 Step -1
                Try
                    Dim output As String = PathGuard.ArchiveIoPath(CreatedFiles(i))
                    If Not File.Exists(output) Then Continue For
                    Dim attributes As FileAttributes = File.GetAttributes(output)
                    If (attributes And FileAttributes.ReadOnly) <> 0 Then File.SetAttributes(output, attributes And Not FileAttributes.ReadOnly)
                    File.Delete(output)
                Catch ex As Exception
                    Log.WriteError("Extraction: could not remove incomplete output '" & CreatedFiles(i) & "': " & Log.SafeException(ex))
                End Try
            Next

            For i As Integer = PreservedCopies.Count - 1 To 0 Step -1
                Dim pair As KeyValuePair(Of String, String) = PreservedCopies(i)
                Try
                    File.Move(PathGuard.ArchiveIoPath(pair.Value), PathGuard.ArchiveIoPath(pair.Key))
                Catch ex As Exception
                    Log.WriteError("Extraction: could not restore '" & pair.Value & "': " & Log.SafeException(ex))
                End Try
            Next

            For i As Integer = CreatedDirectories.Count - 1 To 0 Step -1
                Dim path As String = CreatedDirectories(i)
                Try
                    Dim ioPath As String = PathGuard.ArchiveIoPath(path)
                    If Directory.Exists(ioPath) AndAlso Not Directory.EnumerateFileSystemEntries(ioPath).Any() Then Directory.Delete(ioPath)
                Catch ex As Exception
                    Log.WriteWarning("Extraction: could not remove output directory '" & path & "': " & Log.SafeException(ex))
                End Try
            Next
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
        Private Function ResolveEntryDestination(ByVal dest As String, Optional requireOriginalName As Boolean = False) As String
            If Not File.Exists(PathGuard.ArchiveIoPath(dest)) Then Return dest

            Dim kept As String = GetNonCollidingPath(dest)
            Try
                File.Move(PathGuard.ArchiveIoPath(dest), PathGuard.ArchiveIoPath(kept))
                PreservedCopies.Add(New KeyValuePair(Of String, String)(dest, kept))
                Log.WriteWarning("Extraction: preserving existing file '" & dest & "' as '" & kept & "'.")
                ' The rename freed the original name — that is where the entry goes.
                ' (Returning `kept` here would overwrite the very file we just
                ' preserved.)
                Return dest
            Catch ex As Exception
                ' Locked / ACLs / ...: do not destroy the file either — extract the
                ' entry to the free name and leave the existing file untouched.
                If requireOriginalName Then Throw New IOException("Could not preserve the existing file before extraction: " & dest, ex)
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
                Dim ioPath As String = PathGuard.ArchiveIoPath(candidate)
                If Not File.Exists(ioPath) AndAlso Not Directory.Exists(ioPath) Then Return candidate
                If i >= 9999 Then
                    Return IO.Path.Combine(dir, baseName & " (" & Guid.NewGuid().ToString("N").Substring(0, 8) & ")" & ext)
                End If
            Loop
        End Function

        Private Sub CreateOutputDirectory(path As String)
            Dim ioPath As String = PathGuard.ArchiveIoPath(path)
            If Directory.Exists(ioPath) Then Return
            Dim parent As String = IO.Path.GetDirectoryName(path)
            If Not String.IsNullOrEmpty(parent) Then CreateOutputDirectory(parent)
            Directory.CreateDirectory(ioPath)
            CreatedDirectories.Add(path)
        End Sub

        Private Sub ThrowIfCancelled()
            If CancelRequested Then Throw New OperationCanceledException("Extraction cancelled.")
        End Sub

        Private Sub WriteEntry(source As Stream, dest As String, modified As Date?)
            CreateOutputDirectory(Path.GetDirectoryName(dest))
            dest = ResolveEntryDestination(dest)
            Dim ioPath As String = PathGuard.ArchiveIoPath(dest)
            Using output As New FileStream(ioPath, FileMode.CreateNew, FileAccess.Write, FileShare.None)
                CreatedFiles.Add(dest)
                Dim buffer(65535) As Byte
                Do
                    ThrowIfCancelled()
                    Dim count As Integer = source.Read(buffer, 0, buffer.Length)
                    If count = 0 Then Exit Do
                    output.Write(buffer, 0, count)
                Loop
            End Using
            If modified.HasValue Then File.SetLastWriteTime(ioPath, modified.Value)
        End Sub
    End Class


#End Region

End Class
