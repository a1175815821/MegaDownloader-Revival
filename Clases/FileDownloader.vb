Imports System.IO
Imports System.Net
Imports System.ComponentModel
Imports System.Reflection
Imports System.Linq


#Region "Public Class FileDownloader"

Public Class FileDownloader
    Implements IDisposable

#Region "Nested types"

#Region "Public Structure FileInfo"

    Public Class DataPart

        Public Class Chunk

            Public StartIndex As Long ' Position in the file where the chunk starts
            Public Size As Long ' Total size of the chunk
            Public Index As Long ' Position inside the chunk
            Public Available As Boolean
            ' P0-1 方案B：已 fsync 的边界（≤ Index）。内存游标 Index 照常按每次落盘推进，
            ' XML 只持久化 SyncedIndex（Fichero.GuardarXML 钳制），保证"持久化进度 ≤ 已 fsync 数据"。
            ' 不参与 XML 序列化之外的任何持久化；全 0 默认，首刷必 fsync。
            Public SyncedIndex As Long

            ' Ex: file of 10000 bytes, 4 chunks, the fourth chunk has 60% completed:
            ' * StartIndex: 7500
            ' * Size: 2500
            ' * Index: 1500

            Public Sub New()
                Me.StartIndex = 0
                Me.Size = 0
                Me.Index = 0
                Me.SyncedIndex = 0
                Me.Available = True
            End Sub
        End Class


        Public ChunkList As Generic.List(Of Chunk)
        Public AllFinished As Boolean
        Private _Mutex As Object

        Public Sub New()
            Me._Mutex = New Object
            Me.AllFinished = False
            Me.ChunkList = New Generic.List(Of Chunk)
        End Sub

        Public Sub New(size As Long, numParts As Int32)
            Me.New()
            Dim chunksize As Long = CLng(Math.Ceiling(size / CLng(numParts)))
            Dim k16 As Long = 16 * 1024
            If size < k16 Or numParts < 2 Or chunksize < k16 Then
                ' Just 1 chunk
                Dim Chunk As New Chunk
                Chunk.StartIndex = 0
                Chunk.Index = 0
                Chunk.Size = size
                Chunk.Available = True
                Me.ChunkList.Add(Chunk)
                Log.WriteDebug(String.Format("Setting chunk {0}: From: {1} - Size: {2}", 1, Chunk.StartIndex, Chunk.Size))
            Else
                ' Lo hacemos múltiplo de 16k
                chunksize = CLng(Math.Ceiling(chunksize / k16) * k16)
                Dim lastchunksize As Long = size - (chunksize * CLng(numParts - 1))
                If lastchunksize < 0 Then
                    ' ???
                    ' Just 1 chunk
                    Dim Chunk As New Chunk
                    Chunk.StartIndex = 0
                    Chunk.Index = 0
                    Chunk.Size = size
                    Chunk.Available = True
                    Me.ChunkList.Add(Chunk)
                    Log.WriteDebug(String.Format("Setting chunk {0}: From: {1} - Size: {2}", 1, Chunk.StartIndex, Chunk.Size))
                Else
                    For i As Int32 = 1 To numParts
                        Dim Chunk As New Chunk
                        Chunk.StartIndex = CLng(i - 1) * chunksize
                        Chunk.Index = 0
                        Chunk.Available = True
                        If i = numParts Then
                            Chunk.Size = lastchunksize
                        Else
                            Chunk.Size = chunksize
                        End If
                        Me.ChunkList.Add(Chunk)
                        Log.WriteDebug(String.Format("Setting chunk {0}: From: {1} - Size: {2}", i, Chunk.StartIndex, Chunk.Size))
                    Next
                End If

            End If

        End Sub

        Public Sub ResetAvailableParts()
            SyncLock _Mutex
                For Each c1 As Chunk In Me.ChunkList
                    If c1.Index <> c1.Size Then
                        c1.Available = True
                    End If
                Next
            End SyncLock
        End Sub

        ''' <summary>P0-1:线程安全地汇总已下载字节(各 chunk Index 之和),供看门狗判断有无进度。</summary>
        Public Function GetTotalProgress() As Long
            SyncLock _Mutex
                Dim total As Long = 0
                If Me.ChunkList IsNot Nothing Then
                    For Each c1 As Chunk In Me.ChunkList
                        total += c1.Index
                    Next
                End If
                Return total
            End SyncLock
        End Function


        Public ReadOnly Property NextAvailablePartIndex As Int32?
            Get
                SyncLock _Mutex
                    Dim ret As Int32? = Nothing
                    Dim i As Integer = 0
                    For Each c1 As Chunk In Me.ChunkList
                        If c1.Available Then
                            ret = i
                            c1.Available = False
                            Exit For
                        End If
                        i += 1
                    Next
                    Return ret
                End SyncLock
            End Get
        End Property

        Public Sub SetProgress(chunkIndex As Int32, index As Long)
            SyncLock _Mutex
                Dim c As Chunk = Me.ChunkList(chunkIndex)
                c.Index = index
                If c.Index = c.Size Then
                    Dim Missing As Boolean = False
                    For Each c1 As Chunk In Me.ChunkList
                        If c1.Index <> c1.Size Then
                            Missing = True
                        End If
                    Next
                    If Not Missing Then
                        Me.AllFinished = True
                    End If
                End If
            End SyncLock
        End Sub

        ''' <summary>
        ''' Validates chunk layout and clears AllFinished when incomplete or inconsistent.
        ''' </summary>
        Public Function ValidateAndNormalize(ByVal expectedSize As Long) As Boolean
            SyncLock _Mutex
                If Me.ChunkList Is Nothing OrElse Me.ChunkList.Count = 0 Then
                    Me.AllFinished = False
                    Return False
                End If
                If expectedSize < 0 Then
                    Me.AllFinished = False
                    Return False
                End If

                Dim ordered = Me.ChunkList.OrderBy(Function(c) c.StartIndex).ToList()
                Dim cursor As Long = 0
                For Each c As Chunk In ordered
                    If c.StartIndex < 0 OrElse c.Size < 0 OrElse c.Index < 0 OrElse c.Index > c.Size Then
                        Me.AllFinished = False
                        Return False
                    End If
                    If c.StartIndex <> cursor Then
                        Me.AllFinished = False
                        Return False
                    End If
                    cursor += c.Size
                Next
                If cursor <> expectedSize Then
                    Me.AllFinished = False
                    Return False
                End If

                ' Round any non-aligned partial progress down to a 16-byte boundary.
                ' The CTR keystream can only be seeked in whole blocks; resuming from a
                ' non-aligned offset (persisted by older builds) would decrypt all
                ' following data with the wrong keystream and corrupt the file.
                For Each c As Chunk In ordered
                    If c.Index <> c.Size AndAlso c.Index > 0 AndAlso (c.Index And 15) <> 0 Then
                        Log.WriteWarning("ValidateAndNormalize: rounding chunk progress down to a 16-byte boundary (" &
                                         c.StartIndex & "+" & c.Index & " -> " & (c.StartIndex + (c.Index And Not 15)) & ")")
                        c.Index = c.Index And Not 15L
                    End If
                    ' 方案B：已同步边界永不超过持久化游标（旧版本无此字段时 SyncedIndex=0，
                    ' 钳制只会让重启多重下零星数据，方向安全）。
                    If c.SyncedIndex > c.Index Then c.SyncedIndex = c.Index
                Next

                Dim missing As Boolean = ordered.Any(Function(c) c.Index <> c.Size)
                Me.AllFinished = Not missing
                Return True
            End SyncLock
        End Function

    End Class

    ''' <summary>Simple structure for managing file info</summary>
    Public Class FileInfo

        ''' <summary>The complete path of the file (directory + filename)</summary>
        Public Path As String

        Public FileID As String
        Public FileKey As String
        Public NumParts As Int32
        Private _Size As Long

        Private _dataPart As DataPart
        Private _Mutex As Object
        ''' <summary>The name of the file</summary>
        Private _Name As String

        Public Property Name As String
            Get
                Return _Name
            End Get
            Set(value As String)
                Me._Name = PathGuard.SanitizeFileName(value & "", "file")
            End Set
        End Property
        ''' <summary>Create a new instance of FileInfo</summary>
        ''' <param name="path">The complete path of the file (directory + filename)</param>
        Public Sub New(ByVal path As String)
            Me._Mutex = New Object
            Me.Path = path
            Me.Name = Me.Path.Split("/"c)(Me.Path.Split("/"c).Length - 1)
            Me.NumParts = 1
            Me.Size = 0
            Me._dataPart = Nothing
        End Sub

        Public ReadOnly Property DataPartInitialized As Boolean
            Get
                Return Me._dataPart IsNot Nothing
            End Get
        End Property


        Public ReadOnly Property GetDataPart As DataPart
            Get
                If Size = 0 Then
                    Throw New InvalidOperationException("Must specify size")
                End If
                SyncLock _Mutex
                    If _dataPart Is Nothing Then
                        _dataPart = New DataPart(Me.Size, Me.NumParts)
                    End If
                    Return _dataPart
                End SyncLock
            End Get
        End Property

        Public Sub SetDataPart(ByVal d As DataPart)
            SyncLock _Mutex
                If d IsNot Nothing Then
                    Me._dataPart = d
                    Me._dataPart.ResetAvailableParts()
                End If
            End SyncLock
        End Sub

        Public Property Size As Long
            Get
                Return Me._Size
            End Get
            Set(value As Long)
                Me._Size = value
                If DataPartInitialized Then
                    Dim Tamano As Long = 0
                    SyncLock _Mutex
                        For Each c As DataPart.Chunk In _dataPart.ChunkList
                            'Tamano += c.Size
                            ' Now the chunks can be resized depending on MEGA response... so we will take the biggest chunk
                            If c.StartIndex + c.Size > Tamano Then
                                Tamano = c.StartIndex + c.Size
                            End If
                        Next
                    End SyncLock
                    If value <> Tamano Then
                        Throw New InvalidOperationException("File size does not match [" & value & " - " & Tamano & "]")
                    End If
                End If
            End Set
        End Property

    End Class
#End Region

#Region "Private Enum [Event]"
    ''' <summary>Holder for events that are triggered in the background worker but need to be fired in the main thread</summary>
    Private Enum [Event]
        CalculationFileSizesStarted

        FileSizesCalculationComplete
        CreatingFilesLocal
        FilesLocalCreated
        DeletingFilesAfterCancel

        FileDownloadAttempting
        FileDownloadStarted
        FileDownloadStopped
        FileDownloadSucceeded

        ProgressChanged
    End Enum
#End Region

#Region "Private Enum InvokeType"
    ''' <summary>Holder for the action that needs to be invoked</summary>
    Private Enum InvokeType
        EventRaiser
        FileDownloadFailedRaiser
        ChunkDownloadFailedRaiser
        CalculatingFileNrRaiser
        StartDownloaderRaiser
    End Enum
#End Region

    Private Class DownloaderWorker
        Inherits BackgroundWorker

        Private _ChunkIndex As Int32
        Private _file As FileInfo
        Private _ChunkDownloadFailed As Boolean
        Public Sub New(ChunkIndex As Int32, file As FileInfo)
            Me._ChunkIndex = ChunkIndex
            Me._file = file
            Me._ChunkDownloadFailed = False
        End Sub

        Public Property ChunkDownloadFailed As Boolean
            Get
                Return _ChunkDownloadFailed
            End Get
            Set(value As Boolean)
                _ChunkDownloadFailed = value
            End Set
        End Property
        Public ReadOnly Property File As FileInfo
            Get
                Return _file
            End Get
        End Property
        Public ReadOnly Property ChunkIndex As Int32
            Get
                Return _ChunkIndex
            End Get
        End Property
    End Class

#End Region

#Region "Events"
    ''' <summary>Occurs when the file downloading has started</summary>
    Public Event Started As EventHandler
    ''' <summary>Occurs when the file downloading has been paused</summary>
    Public Event Paused As EventHandler
    ''' <summary>Occurs when the file downloading has been resumed</summary>
    Public Event Resumed As EventHandler
    ''' <summary>Occurs when the user has requested to cancel the downloads</summary>
    Public Event CancelRequested As EventHandler
    ''' <summary>Occurs when the user has requested to cancel the downloads and the cleanup of the downloaded files has started</summary>
    Public Event DeletingFilesAfterCancel As EventHandler
    ''' <summary>Occurs when the file downloading has been canceled by the user</summary>
    Public Event Canceled As EventHandler
    ''' <summary>Occurs when the file downloading has been completed (without canceling it)</summary>
    Public Event Completed As EventHandler
    ''' <summary>Occurs when the file downloading has been stopped by either cancellation or completion</summary>
    Public Event Stopped As EventHandler

    ''' <summary>Occurs when the busy state of the FileDownloader has changed</summary>
    Public Event IsBusyChanged As EventHandler
    ''' <summary>Occurs when the pause state of the FileDownloader has changed</summary>
    Public Event IsPausedChanged As EventHandler
    ''' <summary>Occurs when the either the busy or pause state of the FileDownloader have changed</summary>
    Public Event StateChanged As EventHandler

    ''' <summary>Occurs when the calculation of the file sizes has started</summary>
    Public Event CalculationFileSizesStarted As EventHandler
    ''' <summary>Occurs when the calculation of the file sizes has started</summary>
    Public Event CalculatingFileSize As FileSizeCalculationEventHandler
    ''' <summary>Occurs when the calculation of the file sizes has been completed</summary>
    Public Event FileSizesCalculationComplete As EventHandler

    ''' <summary>Occurs when the FileDownloader attempts to get a web response to download the file</summary>
    Public Event FileDownloadAttempting As EventHandler
    ''' <summary>Occurs when a file download has started</summary>
    Public Event FileDownloadStarted As EventHandler
    ''' <summary>Occurs when a file download has stopped</summary>
    Public Event FileDownloadStopped As EventHandler
    ''' <summary>Occurs when a file download has been completed successfully</summary>
    Public Event FileDownloadSucceeded As EventHandler
    ''' <summary>Occurs when a file download has been completed unsuccessfully</summary>
    Public Event FileDownloadFailed(ByVal sender As Object, ByVal e As Exception)
    ''' <summary>Occurs when a chunk download has been completed unsuccessfully</summary>
    Public Event ChunkDownloadFailed(ByVal sender As Object, ByVal e As Exception)
    ''' <summary>Occurs every time a block of data has been downloaded</summary>
    Public Event ProgressChanged As EventHandler

    Public Event FileLocalCreated As EventHandler

#End Region

#Region "Fields"
    Public Delegate Sub FileSizeCalculationEventHandler(ByVal sender As Object)


    Private WithEvents bgwDownloader As New BackgroundWorker
    Private WithEvents listDownloaders As New Generic.List(Of DownloaderWorker)
    Private Mutex As New Object() ' Sync workers for object manipulation
    Private MutexFile As New Object() ' Sync workers for disk write
    ' P0-1 方案A：.part 长生命周期写流。FlushToDisk 复用本流，只做 Position+Write+Flush(True)，
    ' 省掉每次 open/close；fsync 语义不变，"持久化进度 ≤ 已 fsync 数据"红线不变。
    ' 仅在 MutexFile 保护下访问；.part 重建/重命名前必须先 ClosePartStream。
    Private m_partStream As System.IO.FileStream = Nothing
    Private m_partStreamPath As String = Nothing
    ' P0-1 方案B：fsync 节流记账。达到任一阈值才 Flush(True)，其余落盘只 Write。
    Private m_lastFsyncUtc As Date = Date.MinValue
    Private Const FsyncEveryBytes As Long = 8L * 1024L * 1024L
    Private Const FsyncEverySeconds As Double = 5.0
    Private trigger As New Threading.ManualResetEvent(True)
    Private m_num_connections, m_parts_per_file As Int32


    ' Preferences
    Private m_supportsProgress, m_deleteFiles, m_deleteCompletedFiles As Boolean
    Private m_packageSize, m_bufferSize, m_stopWatchCycles As Int32

    ' State
    Private m_disposed As Boolean = False
    Private m_busy, m_paused, m_canceled As Boolean
    Private m_currentFileProgress, m_totalProgress, m_currentFileSize As Int64
    Private m_currentSpeed As Generic.Dictionary(Of String, Long)

    ' Data
    Private m_localDirectory As String
    Private m_file As FileInfo
    Private m_totalSize As Int64

    ''' <summary>v2.5 beta: 配额异常归一上报。返回 True 表示已按配额处理(调用方不再普通重试)。</summary>
    Friend Shared Function ReportQuotaIfMatch(ex As Exception) As Boolean
        If TypeOf ex Is MegaQuotaExceededException Then
            MegaQuotaManager.ReportQuota()
            Return True
        End If
        Dim wex As System.Net.WebException = TryCast(ex, System.Net.WebException)
        If wex IsNot Nothing AndAlso MegaQuotaManager.IsQuotaWebException(wex) Then
            Dim hint As Long? = MegaQuotaManager.TryGetRetryAfterSeconds(wex)
            MegaQuotaManager.ReportQuota(If(hint.HasValue, hint.Value, 0))
            Return True
        End If
        If ex IsNot Nothing AndAlso Conexion.IsQuotaErrorText(ex.Message) Then
            MegaQuotaManager.ReportQuota()
            Return True
        End If
        Return False
    End Function
#End Region

#Region "Constructors"
    ''' <summary>Create a new instance of a FileDownloader</summary>
    ''' <param name="supportsProgress">Optional. Boolean. Should the FileDownloader support total progress statistics?</param>
    Public Sub New(Optional ByVal supportsProgress As Boolean = False)
        ' Set the bgw properties
        bgwDownloader.WorkerReportsProgress = True
        bgwDownloader.WorkerSupportsCancellation = True
        ' Set the default class preferences
        Me.SupportsProgress = supportsProgress
        Me.BufferSize = 500 * 1024
        Me.PackageSize = 50 * 1024
        Me.StopWatchCyclesAmount = 30
        Me.PartsPerFile = 1
        Me.NumConnections = 1
        Me.DeleteCompletedFilesAfterCancel = False
        Me.DeleteFilesAfterCancel = True
        Me.m_currentSpeed = New Generic.Dictionary(Of String, Long)
    End Sub
#End Region

#Region "Public methods"

    Public Sub AddFileInfo(ByVal FileID As String, ByVal FileKey As String, ByVal path As String, ByVal name As String, ByVal part As DataPart)
        Dim Info As New FileInfo(path)
        Info.Name = name
        Info.NumParts = Me.PartsPerFile
        Info.SetDataPart(part)
        Info.FileID = FileID
        Info.FileKey = FileKey
        Me.File = Info
    End Sub

    ''' <summary>Start the downloads</summary>
    Public Sub Start()
        Me.IsBusy = True
    End Sub

    ''' <summary>pause the downloads</summary>
    Public Sub Pause()
        Me.IsPaused = True
    End Sub

    ''' <summary>Resume the downloads</summary>
    Public Sub [Resume]()
        Me.IsPaused = False
    End Sub

    ''' <summary>Stop the downloads</summary>
    Public Overloads Sub [Stop]()
        Me.IsBusy = False
    End Sub

    ''' <summary>Stop the downloads</summary>
    ''' <param name="deleteCompletedFiles">Required. Boolean. Indicates wether the complete downloads should be deleted</param>
    Public Overloads Sub [Stop](ByVal deleteCompletedFiles As Boolean)
        Me.DeleteCompletedFilesAfterCancel = deleteCompletedFiles
        Me.Stop()
    End Sub

    ''' <summary>Release the recources held by the FileDownloader</summary>
    Public Sub Dispose() Implements IDisposable.Dispose
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub

    ''' <summary>Format an amount of bytes to a more readible notation with binary notation symbols</summary>
    ''' <param name="size">Required. Int64. The raw amount of bytes</param>
    ''' <param name="decimals">Optional. Int32. The amount of decimals you want to have displayed in the notation</param>
    Public Shared Function FormatSizeBinary(ByVal size As Int64, Optional ByVal decimals As Int32 = 2) As String
        ' By De Dauw Jeroen - April 2009 - jeroen_dedauw@yahoo.com
        Dim sizes() As String = {"B", "KiB", "MiB", "GiB", "TiB", "PiB", "EiB", "ZiB", "YiB"}
        Dim formattedSize As Double = Math.Abs(CDbl(size))
        Dim sizeIndex As Int32 = 0
        While formattedSize >= 1024 AndAlso sizeIndex < sizes.Length - 1
            formattedSize /= 1024
            sizeIndex += 1
        End While
        Return Math.Round(formattedSize, decimals).ToString & sizes(sizeIndex)
    End Function

    ''' <summary>Format an amount of bytes to a more readible notation with decimal notation symbols</summary>
    ''' <param name="size">Required. Int64. The raw amount of bytes</param>
    ''' <param name="decimals">Optional. Int32. The amount of decimals you want to have displayed in the notation</param>
    Public Shared Function FormatSizeDecimal(ByVal size As Int64, Optional ByVal decimals As Int32 = 2) As String
        ' By De Dauw Jeroen - April 2009 - jeroen_dedauw@yahoo.com
        Dim sizes() As String = {"B", "kB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB"}
        Dim formattedSize As Double = Math.Abs(CDbl(size))
        Dim sizeIndex As Int32 = 0
        While formattedSize >= 1000 AndAlso sizeIndex < sizes.Length - 1
            formattedSize /= 1000
            sizeIndex += 1
        End While
        Return Math.Round(formattedSize, decimals).ToString & sizes(sizeIndex)
    End Function

    ''' <summary>
    ''' Resolves remote content length via HEAD, falling back to a 1-byte ranged GET.
    ''' </summary>
    Private Shared Function ProbeRemoteFileSize(ByVal url As String) As Long
        Try
            Dim webReq As HttpWebRequest = Conexion.CreateHttpWebRequest(url)
            webReq.Method = "HEAD"
            webReq.Timeout = 15000
            Using webResp As HttpWebResponse = CType(webReq.GetResponse, HttpWebResponse)
                If webResp.ContentLength >= 0 Then Return webResp.ContentLength
            End Using
        Catch
            ' HEAD may be unsupported; try ranged GET below
        End Try

        Dim rangeReq As HttpWebRequest = Conexion.CreateHttpWebRequest(url)
        rangeReq.Method = "GET"
        rangeReq.Timeout = 15000
        rangeReq.AddRange(0, 0)
        Using rangeResp As HttpWebResponse = CType(rangeReq.GetResponse, HttpWebResponse)
            Dim contentRange As String = rangeResp.Headers.Item("Content-Range")
            If Not String.IsNullOrEmpty(contentRange) AndAlso contentRange.Contains("/"c) Then
                Dim totalPart As String = contentRange.Substring(contentRange.LastIndexOf("/"c) + 1).Trim()
                Dim totalLen As Long
                If totalPart <> "*" AndAlso Long.TryParse(totalPart, totalLen) AndAlso totalLen >= 0 Then
                    Return totalLen
                End If
            End If
            If rangeResp.ContentLength >= 0 AndAlso rangeResp.StatusCode = HttpStatusCode.OK Then
                Return rangeResp.ContentLength
            End If
        End Using
        Return -1
    End Function
#End Region

#Region "Private/protected methods"


    Private Sub bgwDownloader_DoWork(ender As Object, e As DoWorkEventArgs) Handles bgwDownloader.DoWork
        Try

            Try
                If Me.SupportsProgress Then calculateFilesSize()
            Catch ex As Exception
                Log.WriteError("Error in bgwDownloader.DoWork/calculateFilesSize: " & ex.ToString)
                bgwDownloader.ReportProgress(InvokeType.FileDownloadFailedRaiser, ex)
            End Try


            If Not Directory.Exists(Me.LocalDirectory) Then Directory.CreateDirectory(Me.LocalDirectory)

            If Not bgwDownloader.CancellationPending Then

                downloadFile()

                If bgwDownloader.CancellationPending Then
                    If DeleteFilesAfterCancel Then
                        fireEventFromBgw([Event].DeletingFilesAfterCancel)
                        ' 取消删 .part 前先关复用流，否则打开中的句柄会挡住 Delete。
                        Try
                            SyncLock MutexFile
                                ClosePartStream()
                            End SyncLock
                        Catch ex As Exception
                            Log.WriteWarning("DoWork: part stream close before cleanup failed: " & Log.SafeException(ex))
                        End Try
                        cleanUpFile()
                    End If
                    e.Cancel = True
                End If
            End If
        Catch ex As Exception
            Log.WriteError("Error in bgwDownloader.DoWork: " & ex.ToString)
            ' Do not call MessageBox.Show from a background thread — it has no parent window,
            ' blocks the worker, and can hang the download pipeline. Surface the failure via
            ' the standard progress channel so the UI thread can present it.
            Try
                bgwDownloader.ReportProgress(InvokeType.FileDownloadFailedRaiser, ex)
            Catch reportEx As Exception
                Log.WriteError("Failed to report bgwDownloader.DoWork error: " & reportEx.ToString)
            End Try
        End Try
    End Sub

    Private Sub downloadFile()

        Dim file As FileInfo = Me.m_file

        Log.WriteWarning("Starting file download " & file.Name)

        Dim FicheroPART As String = PathGuard.GetSafeFilePathUnderRoot(Me.LocalDirectory, file.Name & ".part")
        Dim FicheroReal As String = PathGuard.GetSafeFilePathUnderRoot(Me.LocalDirectory, file.Name)

        Dim exc As Exception = Nothing

        Try

            ' Get size (HEAD, fall back to ranged GET if HEAD unsupported)
            If file.Size = 0 Then
                Try
                    Dim TotalSize As Long = ProbeRemoteFileSize(Me.File.Path)
                    If TotalSize < 0 Then
                        Throw New ApplicationException("Could not determine remote file size.")
                    End If
                    file.Size = TotalSize
                    m_currentFileSize = TotalSize
                    Log.WriteInfo("File size " & file.Name & ": " & TotalSize)
                Catch ex As WebException
                    ReportQuotaIfMatch(ex)
                    exc = ex
                Catch ex As Exception
                    ReportQuotaIfMatch(ex)
                    exc = ex
                End Try
            End If

            ' Check errors
            If exc IsNot Nothing Then
                Log.WriteError("Error downloading file " & file.Name & " - " & Log.SafeException(exc))
                bgwDownloader.ReportProgress(InvokeType.FileDownloadFailedRaiser, exc)
                exc = Nothing
                Exit Try
            End If

            ' Disk space precheck (file size + 64 MiB margin)
            Try
                Dim root As String = Path.GetPathRoot(Path.GetFullPath(Me.LocalDirectory))
                If Not String.IsNullOrEmpty(root) Then
                    Dim di As New DriveInfo(root)
                    If di.IsReady AndAlso di.AvailableFreeSpace < file.Size + (64L * 1024L * 1024L) Then
                        Throw New IOException("Insufficient disk space for download.")
                    End If
                End If
            Catch ex As IOException
                exc = ex
            Catch ex As Exception
                Log.WriteWarning("Disk space check skipped: " & Log.SafeException(ex))
            End Try
            If exc IsNot Nothing Then
                Log.WriteError("Error preparing download " & file.Name & " - " & Log.SafeException(exc))
                bgwDownloader.ReportProgress(InvokeType.FileDownloadFailedRaiser, exc)
                exc = Nothing
                Exit Try
            End If

            ' B1-③:0 字节空文件短路。探活后仍为 0 即 MEGA 占位空文件:不建 DataPart
            ' (FileInfo.Size=0 时 GetDataPart 抛 "Must specify size")、不开连接,
            ' 直接落 0 字节文件、验 MetaMAC(空文件期望 (0,0))、走正常重命名与成功事件。
            ' 此前必经 GetDataPart 抛错 + 自愈每 15 分钟空转,永不成功。
            If file.Size = 0 AndAlso Not bgwDownloader.CancellationPending Then
                Try
                    Log.WriteInfo("Finalizing empty (0-byte) file " & file.Name)
                    SyncLock MutexFile
                        Using fs As New System.IO.FileStream(FicheroPART, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None)
                        End Using
                    End SyncLock
                    Dim keyForEmpty As String = file.FileKey
                    If Not String.IsNullOrEmpty(keyForEmpty) AndAlso keyForEmpty.Contains("=###n=") Then
                        keyForEmpty = keyForEmpty.Substring(0, keyForEmpty.IndexOf("=###n="))
                    End If
                    If String.IsNullOrEmpty(keyForEmpty) Then
                        Throw New ApplicationException("FileKey not defined")
                    End If
                    If Not Criptografia.VerifyMegaMetaMac(FicheroPART, keyForEmpty) Then
                        Throw New ApplicationException("MEGA MetaMAC verification failed for empty file " & file.Name & ".")
                    End If
                    SyncLock MutexFile
                        RenamePartToReal(FicheroPART, FicheroReal)
                    End SyncLock
                    Log.WriteWarning("File downloaded successfully")
                    fireEventFromBgw([Event].FileDownloadSucceeded)
                Catch emptyEx As Exception
                    Log.WriteError("Error finalizing empty file " & file.Name & " - " & Log.SafeException(emptyEx))
                    bgwDownloader.ReportProgress(InvokeType.FileDownloadFailedRaiser, emptyEx)
                End Try
                Exit Try
            End If

            ' Validate resume metadata before trusting AllFinished
            If file.DataPartInitialized Then
                If Not file.GetDataPart.ValidateAndNormalize(file.Size) Then
                    Log.WriteWarning("Invalid resume metadata for " & file.Name & "; restarting chunks.")
                    file.SetDataPart(New DataPart(file.Size, file.NumParts))
                End If
            End If

            ' Check size
            If Not System.IO.File.Exists(FicheroPART) Then
                If file.GetDataPart.AllFinished Then
                    ' XML said complete but .part is missing — never invent a finished file
                    file.GetDataPart.AllFinished = False
                    For Each c As DataPart.Chunk In file.GetDataPart.ChunkList
                        c.Index = 0
                        c.SyncedIndex = 0
                        c.Available = True
                    Next
                End If

                fireEventFromBgw([Event].CreatingFilesLocal)
                ' Creamos un fichero vacío con ese tamaño
                SyncLock MutexFile
                Try
                    ' 复用流必须先关，否则 File.Create 会 sharing violation；语义不变。
                    ClosePartStream()
                    Using Stream As System.IO.FileStream = System.IO.File.Create(FicheroPART, 64 * 1024, FileOptions.RandomAccess)
                        Stream.SetLength(file.Size)
                        Stream.Flush(True)
                    End Using
                Catch ex As Exception
                    exc = ex
                End Try
                End SyncLock
            Else
                ' ①:.part 尺寸与远端不一致(远端替换/截断/磁盘满短文件)必须就地修复,
                ' 否则报一次错后自愈 preserve 原样保留,下次必进同一分支,每 15min 空转一次。
                ' 此处删坏件、重置分块、按正确尺寸重建后继续下载,不再抛错。
                Dim rebuiltPart As Boolean = False
                SyncLock MutexFile
                Try
                    Dim inf As New System.IO.FileInfo(FicheroPART)
                    If inf.Length <> file.Size Then
                        Log.WriteWarning("Part size mismatch for " & file.Name & " (disk " & inf.Length & " vs expected " & file.Size & "); rebuilding .part.")
                        ' 重建前关闭复用流，否则 Delete/Create 会被占用中的句柄挡住。
                        ClosePartStream()
                        Try
                            System.IO.File.Delete(FicheroPART)
                        Catch delEx As Exception
                            exc = New ApplicationException("The file exists and does not have the expected size [" & inf.Length & " - " & file.Size & "]")
                            Log.WriteWarning("Could not delete mismatched .part: " & Log.SafeException(delEx))
                        End Try
                        If exc Is Nothing Then
                            file.SetDataPart(New DataPart(file.Size, file.NumParts))
                            Using Stream As System.IO.FileStream = System.IO.File.Create(FicheroPART, 64 * 1024, FileOptions.RandomAccess)
                                Stream.SetLength(file.Size)
                                Stream.Flush(True)
                            End Using
                            rebuiltPart = True
                        End If
                    End If
                Catch ex As Exception
                    exc = ex
                End Try
                End SyncLock
                If rebuiltPart Then fireEventFromBgw([Event].CreatingFilesLocal)

            End If

            ' Check errors
            If exc IsNot Nothing Then
                Log.WriteError("Error creating file on disk " & file.Name & " - " & Log.SafeException(exc))
                bgwDownloader.ReportProgress(InvokeType.FileDownloadFailedRaiser, exc)
                exc = Nothing
                Exit Try
            End If

            ' Actualizamos el estado descargado (por si hemos parado)
            For Each chunk As DataPart.Chunk In file.GetDataPart.ChunkList
                m_currentFileProgress += chunk.Index
                m_totalProgress += chunk.Index
            Next

            fireEventFromBgw([Event].FilesLocalCreated)
            If bgwDownloader.CancellationPending Then
                Log.WriteWarning("File download stopped - " & file.Name)
                Exit Sub
            End If

            ' Already fully downloaded on disk with valid metadata — still verify MAC before success
            If Not file.GetDataPart.AllFinished Then
                For i As Integer = 1 To Me.NumConnections
                    Log.WriteDebug("Event raised for starting a new connection")
                    bgwDownloader.ReportProgress(InvokeType.StartDownloaderRaiser, Nothing)
                Next
                ' Wait until all chunks finish. NOTE: there is deliberately NO size-based
                ' force-finish here — the .part file is preallocated to the full size, so
                ' "file size == expected size" is ALWAYS true and proves nothing about the
                ' downloaded content. AllFinished is only ever set by real chunk completion
                ' (a 403 storm at the tail previously left holes that were force-finished).
                Dim waitStartTime As Date = Now
                Dim timedOut As Boolean = False
                ' P0-1:无进度超时(stall watchdog)。此前是总时长墙钟:大文件即使一直在推进,
                ' 只要墙钟超 120s 就判失败,叠加自愈删 .part 形成无限循环。此处每次有 chunk
                ' 推进就重置计时,只有真正停滞 120s 才超时。
                Dim lastProgress As Long = file.GetDataPart.GetTotalProgress()
                Do
                    System.Threading.Thread.Sleep(100)

                    ' ④:暂停补偿。trigger.WaitOne() 在暂停期无限阻塞,墙钟会把暂停时长
                    ' 算成停滞,暂停超 120s 恢复即变红。此处 100ms 轮询,暂停期不断顺延计时。
                    While Not trigger.WaitOne(100)
                        waitStartTime = Now
                        If bgwDownloader.CancellationPending Then Exit While
                    End While
                    If bgwDownloader.CancellationPending Then
                        Log.WriteDebug("Aborting connection - stop requested")
                        SyncLock Mutex
                            For Each w As DownloaderWorker In Me.listDownloaders
                                If w.IsBusy Then
                                    w.CancelAsync()
                                End If
                            Next
                        End SyncLock
                        Log.WriteWarning("File download stopped - " & file.Name)
                        Exit Do
                    End If

                    If file.GetDataPart.AllFinished Then
                        Exit Do
                    End If

                    Dim curProgress As Long = file.GetDataPart.GetTotalProgress()
                    If curProgress <> lastProgress Then
                        lastProgress = curProgress
                        waitStartTime = Now
                    End If

                    ' Stall timeout without any progress — report failure but keep the
                    ' chunk state so a retry resumes from the .part file.
                    ' ⑤:配额熔断期 worker 不再重启,零速干等 120s+30s 才变红。
                    ' 熔断期内阈值收紧到 10s,快速转红并标配额,由边沿唤醒恢复。
                    Dim idleLimitSeconds As Integer = If(MegaQuotaManager.IsQuarantined(), 10, 120)
                    If Now.Subtract(waitStartTime).TotalSeconds > idleLimitSeconds Then
                        Log.WriteError("Download stalled with no progress for " & idleLimitSeconds & "s for " & file.Name & "; aborting.")
                        timedOut = True
                        Exit Do
                    End If
                Loop

                ' Wait until chunk workers finish before rename/timeout decision.
                ' B2:看门狗失败判定必须在排空后做——超时瞬间与撞线完成竞态时,
                ' 若已 AllFinished 则走正常重命名/MetaMAC 流程,不得再报 FileDownloadFailed
                ' (否则成功与失败双事件竞态,失败先到会把完好文件钉成 Erroneo)。
                ' ⑤:配额期 worker 已不再重启,排空通常瞬间结束;熔断期只等 5s,避免再叠 30s。
                Dim drainSeconds As Integer = If(timedOut AndAlso MegaQuotaManager.IsQuarantined(), 5, 30)
                Dim waitUntil As Date = Now.AddSeconds(drainSeconds)
                While Now < waitUntil
                    Dim busy As Boolean
                    SyncLock Me.Mutex
                        busy = Me.listDownloaders.Any(Function(w) w.IsBusy)
                    End SyncLock
                    If Not busy Then Exit While
                    System.Threading.Thread.Sleep(50)
                End While

                ' Report the timeout as a download failure. Chunk state is intentionally
                ' preserved for resumption. RC:配额熔断期内超时必须报配额异常(否则 FailedByQuota=False,
                ' 立即重试捞不回);非配额超时为无进度停滞(正常 120s,暂停不计,熔断期 10s;
                ' 有推进会自动顺延,不再是总时长上限)。
                ' B2:排空后已 AllFinished 则不报(交由下述重命名流程成功)。
                If timedOut AndAlso Not bgwDownloader.CancellationPending AndAlso Not file.GetDataPart.AllFinished Then
                    If MegaQuotaManager.IsQuarantined() Then
                        bgwDownloader.ReportProgress(InvokeType.FileDownloadFailedRaiser,
                            New MegaQuotaExceededException("MEGA transfer quota exceeded (EOVERQUOTA / HTTP 509). Download paused during quota quarantine; use Retry now after changing IP/proxy or wait for auto-resume. Progress preserved."))
                    Else
                        bgwDownloader.ReportProgress(InvokeType.FileDownloadFailedRaiser,
                            New ApplicationException("Download stalled with no progress (idle watchdog 120s; pause not counted; progress resets the timer). Progress preserved, retry resumes from the .part file."))
                    End If
                ElseIf timedOut AndAlso file.GetDataPart.AllFinished Then
                    Log.WriteWarning("Download watchdog timed out but all chunks finished during drain for " & file.Name & "; proceeding to finalize instead of failing.")
                End If
            End If
        Catch ex As Exception
            exc = ex
        Finally
            ' If all chunks finished despite an earlier non-fatal error, the download itself succeeded.
            ' Do NOT report FileDownloadFailed — that would wrongly mark a completed file as error.
            If exc IsNot Nothing AndAlso file.GetDataPart.AllFinished AndAlso Not bgwDownloader.CancellationPending Then
                Log.WriteWarning("Non-fatal error during download of " & file.Name & " but all chunks finished. Suppressing failure: " & Log.SafeException(exc))
                exc = Nothing
            End If
            If exc IsNot Nothing Then
                Log.WriteError("Error trying to download file " & file.Name & " - " & Log.SafeException(exc))
                bgwDownloader.ReportProgress(InvokeType.FileDownloadFailedRaiser, exc)
            End If
        End Try

        Try
            ' file.Size=0(远端探活失败)时 GetDataPart 会抛 "Must specify size",
            ' 掩盖上一步已通过 FileDownloadFailed 报告的真实错误(连接失败/404 等),必须前置短路
            If file.Size > 0 AndAlso file.GetDataPart.AllFinished AndAlso Not bgwDownloader.CancellationPending Then
                If Not System.IO.File.Exists(FicheroPART) Then
                    Throw New ApplicationException("Download reported finished but partial file is missing.")
                End If
                Dim partInfo As New System.IO.FileInfo(FicheroPART)
                If partInfo.Length <> file.Size Then
                    Throw New ApplicationException("Download size mismatch before rename.")
                End If

                ' Integrity: MEGA MetaMAC. The schedule is computed exactly as MEGA
                ' clients do (ChunkedHash: 128 KiB * i for i = 1..8, then a fixed 1 MiB),
                ' and the final MAC is compared once over the whole file, exactly as the
                ' SDK does (generateMetaMac + macsmac). The .part file is preallocated
                ' so the size check above proves nothing about content — a mismatch
                ' means the bytes on disk genuinely differ from the uploaded file and
                ' MUST fail hard instead of delivering a damaged file as a success.
                ' 4-word public link keys have no embedded MetaMAC and skip
                ' verification inside VerifyMegaMetaMac.
                Dim keyForMac As String = file.FileKey
                If Not String.IsNullOrEmpty(keyForMac) AndAlso keyForMac.Contains("=###n=") Then
                    keyForMac = keyForMac.Substring(0, keyForMac.IndexOf("=###n="))
                End If
                If Not String.IsNullOrEmpty(keyForMac) Then
                    Log.WriteInfo("Verifying MEGA MetaMAC for " & file.Name)
                    If Not Criptografia.VerifyMegaMetaMac(FicheroPART, keyForMac) Then
                        Throw New ApplicationException("MEGA MetaMAC verification failed: the downloaded data does not match the uploaded file (size was exact: " & partInfo.Length & " bytes). The download will restart from the last valid chunk state.")
                    End If
                End If

                SyncLock MutexFile
                    ' 重命名前关闭复用流：Windows 下打开中的文件无法 Rename，
                    ' 且关闭前已由每次 FlushToDisk 的 Flush(True) 保证落盘，语义不变。
                    ClosePartStream()
                    RenamePartToReal(FicheroPART, FicheroReal)
                End SyncLock
                Log.WriteWarning("File downloaded successfully")
                fireEventFromBgw([Event].FileDownloadSucceeded)
            End If
        Catch ex As Exception
            Log.WriteError("Error finalizing download " & file.Name & " - " & Log.SafeException(ex))
            bgwDownloader.ReportProgress(InvokeType.FileDownloadFailedRaiser, ex)

            ' 修复：验证/重命名失败后必须重置 AllFinished 和 chunk 状态，
            ' 否则下次启动会因 AllFinished=True 跳过下载直接进入验证，导致
            ' "重设也无法下载" 的死循环。
            Try
                file.GetDataPart.AllFinished = False
                For Each c As DataPart.Chunk In file.GetDataPart.ChunkList
                    c.Index = 0
                    c.SyncedIndex = 0
                    c.Available = True
                Next
                Log.WriteWarning("Reset chunk state after finalization failure for " & file.Name)
            Catch cleanupEx As Exception
                Log.WriteWarning("Error during cleanup after finalization failure: " & Log.SafeException(cleanupEx))
            End Try
        End Try
    End Sub

    ''' <summary>
    ''' B1-③:把 FicheroPART 重命名为成品(含 "file (i)" 冲突消解),供正常终结与空文件短路共用。
    ''' 调用方必须已持有 Me.MutexFile(与原内联代码的加锁位置一致)。
    ''' </summary>
    Private Sub RenamePartToReal(ByVal FicheroPART As String, ByVal FicheroReal As String)
        If System.IO.File.Exists(FicheroPART) Then

            If Not System.IO.File.Exists(FicheroReal) Then
                Log.WriteInfo("Rename from " & FicheroPART & " to " & FicheroReal)
                FileSystem.Rename(FicheroPART, FicheroReal)
            Else

                ' File exists, create someone like "file (2).txt"

                Dim extension As String = If(FicheroReal.LastIndexOf("."c) > 0, FicheroReal.Substring(FicheroReal.LastIndexOf("."c) + 1), "")
                Dim fileWithoutExtension As String = If(FicheroReal.LastIndexOf("."c) > 0, FicheroReal.Substring(0, FicheroReal.LastIndexOf("."c)), "")

                Dim i As Integer = 1
                Dim renamed As Boolean = False
                Do
                    i += 1
                    Dim FicheroReal2 As String = fileWithoutExtension & " (" & i & ")" & If(String.IsNullOrEmpty(extension), "", "." & extension)
                    PathGuard.EnsurePathUnderRoot(Me.LocalDirectory, FicheroReal2, allowRoot:=False)
                    If Not System.IO.File.Exists(FicheroReal2) Then
                        Log.WriteInfo("Rename from " & FicheroPART & " to " & FicheroReal2)
                        FileSystem.Rename(FicheroPART, FicheroReal2)
                        renamed = True
                        Exit Do
                    End If

                    If i > 9999 Then
                        Dim uniqueName As String = fileWithoutExtension & " (" & Guid.NewGuid().ToString("N").Substring(0, 8) & ")" & If(String.IsNullOrEmpty(extension), "", "." & extension)
                        PathGuard.EnsurePathUnderRoot(Me.LocalDirectory, uniqueName, allowRoot:=False)
                        Log.WriteInfo("Rename from " & FicheroPART & " to " & uniqueName)
                        FileSystem.Rename(FicheroPART, uniqueName)
                        renamed = True
                        Exit Do
                    End If
                Loop
                If Not renamed Then
                    Throw New ApplicationException("Could not rename partial file: name conflict resolution failed.")
                End If

            End If

        End If
    End Sub

    Private Sub bwgDownloader_ProgressChanged(ByVal sender As Object, ByVal e As ProgressChangedEventArgs) Handles bgwDownloader.ProgressChanged
        Select Case CType(e.ProgressPercentage, InvokeType)
            Case InvokeType.EventRaiser
                Select Case CType(e.UserState, [Event])
                    Case [Event].CalculationFileSizesStarted
                        RaiseEvent CalculationFileSizesStarted(Me, New EventArgs)
                    Case [Event].FileSizesCalculationComplete
                        RaiseEvent FileSizesCalculationComplete(Me, New EventArgs)
                    Case [Event].FilesLocalCreated
                        RaiseEvent FileLocalCreated(Me, New EventArgs)
                    Case [Event].DeletingFilesAfterCancel
                        RaiseEvent DeletingFilesAfterCancel(Me, New EventArgs)

                    Case [Event].FileDownloadAttempting
                        RaiseEvent FileDownloadAttempting(Me, New EventArgs)
                    Case [Event].FileDownloadStarted
                        RaiseEvent FileDownloadStarted(Me, New EventArgs)
                    Case [Event].FileDownloadStopped
                        RaiseEvent FileDownloadStopped(Me, New EventArgs)
                    Case [Event].FileDownloadSucceeded
                        RaiseEvent FileDownloadSucceeded(Me, New EventArgs)
                    Case [Event].ProgressChanged
                        RaiseEvent ProgressChanged(Me, New EventArgs)
                End Select
            Case InvokeType.FileDownloadFailedRaiser
                RaiseEvent FileDownloadFailed(Me, CType(e.UserState, Exception))
            Case InvokeType.CalculatingFileNrRaiser
                RaiseEvent CalculatingFileSize(Me)
            Case InvokeType.StartDownloaderRaiser
                NewDownloaderWorker()
        End Select
    End Sub

    Private Sub bgwDownloader_RunWorkerCompleted(ByVal sender As Object, ByVal e As System.ComponentModel.RunWorkerCompletedEventArgs) Handles bgwDownloader.RunWorkerCompleted
        Me.IsPaused = False
        m_busy = False

        If Me.HasBeenCanceled Then
            RaiseEvent Canceled(Me, New EventArgs)
        Else
            RaiseEvent Completed(Me, New EventArgs)
        End If

        RaiseEvent Stopped(Me, New EventArgs)
        RaiseEvent IsBusyChanged(Me, New EventArgs)
        RaiseEvent StateChanged(Me, New EventArgs)
    End Sub

    Private Sub ChunkDownloader_ProgressChanged(ByVal sender As Object, ByVal e As ProgressChangedEventArgs)
        Select Case CType(e.ProgressPercentage, InvokeType)
            Case InvokeType.EventRaiser
                Select Case CType(e.UserState, [Event])
                    Case [Event].CalculationFileSizesStarted
                        RaiseEvent CalculationFileSizesStarted(Me, New EventArgs)
                    Case [Event].FileSizesCalculationComplete
                        RaiseEvent FileSizesCalculationComplete(Me, New EventArgs)
                    Case [Event].DeletingFilesAfterCancel
                        RaiseEvent DeletingFilesAfterCancel(Me, New EventArgs)
                    Case [Event].FileDownloadAttempting
                        RaiseEvent FileDownloadAttempting(Me, New EventArgs)
                    Case [Event].FileDownloadStarted
                        RaiseEvent FileDownloadStarted(Me, New EventArgs)
                    Case [Event].FileDownloadStopped
                        RaiseEvent FileDownloadStopped(Me, New EventArgs)
                    Case [Event].FileDownloadSucceeded
                        RaiseEvent FileDownloadSucceeded(Me, New EventArgs)
                    Case [Event].ProgressChanged
                        RaiseEvent ProgressChanged(Me, New EventArgs)
                End Select
            Case InvokeType.ChunkDownloadFailedRaiser
                RaiseEvent ChunkDownloadFailed(Me, CType(e.UserState, Exception))
            Case InvokeType.CalculatingFileNrRaiser
                RaiseEvent CalculatingFileSize(Me)
        End Select
    End Sub

    Private Sub ChunkDownloader_DoWork(sender As Object, e As DoWorkEventArgs)
        Try

            Dim worker As DownloaderWorker = CType(sender, DownloaderWorker)

            Dim file As FileInfo = Me.File


            Dim FicheroPART As String = PathGuard.GetSafeFilePathUnderRoot(Me.LocalDirectory, file.Name & ".part")

            Dim FileKey As String = file.FileKey

            Dim webReq As HttpWebRequest = Nothing
            Dim webResp As HttpWebResponse = Nothing

            Dim Chunk As DataPart.Chunk = file.GetDataPart.ChunkList(worker.ChunkIndex)

            Log.WriteDebug("Starting chunk " & FicheroPART & " position " & Chunk.StartIndex + Chunk.Index)

            Dim exc As Exception = Nothing

            Dim readings As Long = 0
            Dim currentBuffersize As Integer = 0
            Dim currentPackageSize As Int32 = -1
            Dim BufferMem(Me.PackageSize - 1) As Byte
            Dim BufferDisk(Me.BufferSize - 1) As Byte
            Dim bytesDownloadedSinceLastTimer As Integer = 0
            Dim speedTimer As New Stopwatch
            Dim arraySpeed(Me.m_stopWatchCycles - 1) As KeyValuePair(Of Double, Double)
            Dim lastSpeedRefresh As Date = Date.MinValue

            Dim ChunkStart As Long = Chunk.StartIndex + Chunk.Index
            Dim ChunkStop As Long = Chunk.StartIndex + Chunk.Size - 1
            Try
                If ChunkStart >= ChunkStop + 1 Then ' Si el indice del chunk es mayor al esperado, presuponemos que se ha descargado del todo y está todo correcto...
                    file.GetDataPart.SetProgress(worker.ChunkIndex, Chunk.Size)
                    Exit Try
                End If

                If String.IsNullOrEmpty(FileKey) Then
                    exc = New ApplicationException("FileKey not defined")
                    worker.ChunkDownloadFailed = True
                    Log.WriteError("Error: FileKey not defined")
                    worker.ReportProgress(InvokeType.ChunkDownloadFailedRaiser, exc)
                    Exit Try
                End If


                Try
                    webReq = Conexion.CreateHttpWebRequest(file.Path)
                    'webReq.UserAgent = "Mozilla/5.0 (Windows; U; Windows NT 5.1; en-US; rv:1.8.0.4) Gecko/20060508 Firefox/1.5.0.4" ' http://stackoverflow.com/questions/6305292/cant-get-html-code-through-httpwebrequest


                    Log.WriteInfo("Starting connection - " & file.Name & " from byte " & ChunkStart & " to " & ChunkStop)

                    ' Resume-offset alignment guard: the CTR keystream is seeked in whole
                    ' 16-byte blocks, so a resume from a non-aligned offset would decrypt
                    ' every following block with the wrong keystream (silent corruption).
                    ' Fail the chunk instead; the aligned progress is kept for the retry.
                    ' (Reaching the tail of the chunk is exempt: no data follows.)
                    If (ChunkStart And 15) <> 0 AndAlso ChunkStart < Chunk.StartIndex + Chunk.Size Then
                        Throw New ApplicationException(String.Format(
                            "Resume offset {0} for {1} is not block-aligned; aborting chunk to avoid keystream misalignment.", ChunkStart, file.Name))
                    End If

                    ' Pequeño hack para soportar ficheros de más de 2 GB:
                    ' http://forums.codeguru.com/showthread.php?467570-WebRequest.AddRange-what-about-files-gt-2gb&p=1794639
                    ' http://www.freesoft.org/CIE/RFC/2068/198.htm
                    Dim key As String = "Range"
                    Dim val As String = String.Format("bytes={0}-{1}", ChunkStart, ChunkStop)

                    Dim method As MethodInfo = GetType(WebHeaderCollection).GetMethod("AddWithoutValidate", BindingFlags.Instance Or BindingFlags.NonPublic)
                    method.Invoke(webReq.Headers, New Object() {key, val})

                    webResp = CType(webReq.GetResponse, HttpWebResponse)

                    ' Require Partial Content for ranged requests
                    If webResp.StatusCode <> HttpStatusCode.PartialContent AndAlso webResp.StatusCode <> HttpStatusCode.OK Then
                        If MegaQuotaManager.IsQuotaStatusCode(webResp.StatusCode) Then
                            MegaQuotaManager.ReportQuota()
                            exc = New MegaQuotaExceededException("MEGA transfer quota exceeded (HTTP 509) during download.")
                        Else
                            exc = New ApplicationException("Unexpected HTTP status for ranged download: " & CInt(webResp.StatusCode).ToString())
                        End If
                    Else
                        Dim contentRangeHeader As String = webResp.Headers.Item("Content-Range")
                        ' Content-Range: bytes 1769472-2147111/2147112
                        If webResp.StatusCode = HttpStatusCode.OK AndAlso String.IsNullOrEmpty(contentRangeHeader) Then
                            ' Server ignored Range and returned full body
                            If ChunkStart <> 0 Then
                                exc = New ApplicationException("Server ignored Range request (HTTP 200 without Content-Range).")
                            End If
                        ElseIf String.IsNullOrEmpty(contentRangeHeader) OrElse Not contentRangeHeader.StartsWith("bytes ") Then
                            If ChunkStart <> 0 Then
                                exc = New ApplicationException("Missing Content-Range for partial download.")
                            End If
                        Else
                            Dim rangeBody As String = contentRangeHeader.Substring(6).Trim()
                            Dim slashIdx As Integer = rangeBody.IndexOf("/"c)
                            Dim rangePart As String = If(slashIdx >= 0, rangeBody.Substring(0, slashIdx), rangeBody)
                            Dim totalPart As String = If(slashIdx >= 0, rangeBody.Substring(slashIdx + 1), "")
                            Dim dashIdx As Integer = rangePart.IndexOf("-"c)
                            If dashIdx < 0 Then
                                exc = New ApplicationException("Invalid Content-Range: " & contentRangeHeader)
                            Else
                                Dim tokenInit As String = rangePart.Substring(0, dashIdx)
                                Dim tokenEnd As String = rangePart.Substring(dashIdx + 1)
                                Dim InitDescarga As Long
                                Dim EndDescarga As Long
                                If Not Long.TryParse(tokenInit, InitDescarga) OrElse Not Long.TryParse(tokenEnd, EndDescarga) Then
                                    exc = New ApplicationException("Invalid Content-Range numbers: " & contentRangeHeader)
                                Else
                                    If Not String.IsNullOrEmpty(totalPart) AndAlso totalPart <> "*" Then
                                        Dim totalLen As Long
                                        If Long.TryParse(totalPart, totalLen) AndAlso file.Size > 0 AndAlso totalLen <> file.Size Then
                                            exc = New ApplicationException(String.Format("Content-Range total length mismatch: {0} vs {1}", totalLen, file.Size))
                                        End If
                                    End If
                                    If exc Is Nothing AndAlso InitDescarga <> ChunkStart Then
                                        If InitDescarga < Chunk.StartIndex OrElse InitDescarga > Chunk.StartIndex + Chunk.Index Then
                                            exc = New ApplicationException(String.Format("Error when downloading chunk {0}-{1}: received start {2}", ChunkStart, ChunkStop, InitDescarga))
                                        ElseIf InitDescarga <> Chunk.StartIndex + Chunk.Index Then
                                            ' Align resume to what the server actually sent (must stay inside chunk)
                                            Chunk.Index = InitDescarga - Chunk.StartIndex
                                            ChunkStart = Chunk.StartIndex + Chunk.Index
                                            ChunkStop = Chunk.StartIndex + Chunk.Size - 1
                                        End If
                                    End If
                                    If exc Is Nothing AndAlso EndDescarga < ChunkStart Then
                                        exc = New ApplicationException("Content-Range end is before requested start.")
                                    End If
                                End If
                            End If
                        End If
                    End If

                Catch ex As WebException
                    ReportQuotaIfMatch(ex)
                    exc = ex
                Catch ex As Exception
                    ReportQuotaIfMatch(ex)
                    exc = ex
                End Try


                If exc IsNot Nothing Then
                    worker.ChunkDownloadFailed = True
                    Log.WriteError("Connection error when downloading file " & file.Name & " - " & Log.SafeException(exc))
                    worker.ReportProgress(InvokeType.ChunkDownloadFailedRaiser, exc)
                    Exit Try
                End If


                Dim FileKeyWithoutN As String = FileKey
                If FileKeyWithoutN.Contains("=###n=") Then
                    FileKeyWithoutN = FileKeyWithoutN.Substring(0, FileKey.IndexOf("=###n="))
                End If

                ' Inicializamos el Cipher (floor seek by absolute file offset)
                Log.WriteDebug("Starting SicBlockCipher seek position " & ChunkStart)
                Dim crono As Date = Now
                Dim oCipher As Criptografia.SicSeekableBlockCipher = Criptografia.GetInstaceCipher(FileKeyWithoutN)
                oCipher.SeekToFileOffset(ChunkStart)
                Log.WriteDebug("Finishing SicBlockCipher seek [" & Now.Subtract(crono).TotalMilliseconds & "ms]")

                If m_currentFileProgress > 0 Then
                    fireEventFromDownloader(worker, [Event].ProgressChanged)
                End If

                Dim Stream As Stream = webResp.GetResponseStream()
                Dim TStream As New ThrottledStream(Stream)
                ThrottledStreamController.GetController.AddStream(TStream, file.FileID)
                speedTimer.Start()
                Try
                    While Chunk.Index < Chunk.Size Or currentBuffersize > 0

                        If worker.CancellationPending Then
                            If currentBuffersize > 0 Then
                                FlushToDisk(worker, FicheroPART, BufferDisk, currentBuffersize, Chunk, True)
                            End If
                            speedTimer.Stop()
                            Exit Sub
                        End If

                        ' Not trigger.WaitOne(0) -> IsPaused? -> Flush to disk
                        If currentBuffersize > 0 And _
                            (Chunk.Index + currentBuffersize >= Chunk.Size Or _
                            Not trigger.WaitOne(0) Or _
                            worker.CancellationPending Or _
                            currentBuffersize + Me.PackageSize > Me.BufferSize) Then

                            Dim [continue] As Boolean = FlushToDisk(worker, FicheroPART, BufferDisk, currentBuffersize, Chunk)
                            If Not [continue] Then
                                Exit While
                            End If

                        End If

                        trigger.WaitOne()
                        If worker.CancellationPending Then
                            If currentBuffersize > 0 Then
                                FlushToDisk(worker, FicheroPART, BufferDisk, currentBuffersize, Chunk, True)
                            End If
                            Log.WriteDebug("Download stopped - " & file.Name)
                            speedTimer.Stop()
                            Exit Sub
                        End If

                        Try

                            ' Fill the packageSize
                            currentPackageSize = 0
                            Dim unexpectedEof As Boolean = False

                            While currentPackageSize < Me.PackageSize
                                Dim bytesDownloaded As Integer = TStream.Read(BufferMem, currentPackageSize, Me.PackageSize - currentPackageSize)
                                currentPackageSize += bytesDownloaded

                                If bytesDownloaded = 0 Then
                                    If Chunk.Index + currentBuffersize + currentPackageSize < Chunk.Size Then
                                        unexpectedEof = True
                                    End If
                                    Exit While
                                End If
                                If Chunk.Index + currentBuffersize + currentPackageSize >= Chunk.Size Then
                                    Exit While
                                End If
                            End While

                            If unexpectedEof Then
                                Throw New ApplicationException("UnexpectedEndOfStream: connection closed before chunk completed.")
                            End If

                            If currentPackageSize = 0 Then
                                If Chunk.Index + currentBuffersize >= Chunk.Size Then
                                    Exit While
                                End If
                                Throw New ApplicationException("UnexpectedEndOfStream: zero-byte read with incomplete chunk.")
                            End If

                            For tempIndex As Integer = 0 To currentPackageSize - 1 Step oCipher.GetBlockSize
                                oCipher.ProcessBlock(BufferMem, tempIndex, BufferDisk, currentBuffersize + tempIndex)
                            Next

                        Catch ex As WebException
                            ReportQuotaIfMatch(ex)
                            exc = ex
                        Catch ex As Exception
                            ReportQuotaIfMatch(ex)
                            exc = ex
                        End Try

                        If exc IsNot Nothing Then
                            If currentBuffersize > 0 Then
                                Try
                                    FlushToDisk(worker, FicheroPART, BufferDisk, currentBuffersize, Chunk, True)
                                Catch flushEx As Exception
                                    Log.WriteError("ChunkDownloader_DoWork: best-effort FlushToDisk before failure reporting failed: " & flushEx.ToString)
                                End Try
                            End If
                            worker.ChunkDownloadFailed = True
                            worker.ReportProgress(InvokeType.ChunkDownloadFailedRaiser, exc)
                            speedTimer.Stop()
                            Exit Sub
                        End If

                        SyncLock Mutex
                            m_currentFileProgress += currentPackageSize
                            m_totalProgress += currentPackageSize
                        End SyncLock

                        bytesDownloadedSinceLastTimer += currentPackageSize

                        ' P0-2:热路径 ProgressChanged 已确认零订阅者（全仓仅 FileDownloader
                        ' 内部 RaiseEvent，无外部 AddHandler/Handles），此处不再经
                        ' BackgroundWorker 编组到 UI 线程。chunk 启动时的低频触发
                        '（:1311 fireEventFromDownloader ProgressChanged）予以保留。
                        currentBuffersize += currentPackageSize

                        If lastSpeedRefresh.AddMilliseconds(175) < Now Then ' Refrescamos cada 175ms
                            lastSpeedRefresh = Now
                            readings += 1

                            speedTimer.Stop()
                            Dim ticks As Long = speedTimer.ElapsedTicks
                            If ticks = 0 Then ticks = 1
                            Dim time As Double = ticks / Stopwatch.Frequency
                            speedTimer.Reset()
                            speedTimer.Start()

                            Dim indexArraySpeed As Integer = CInt(readings Mod Me.StopWatchCyclesAmount)
                            arraySpeed(indexArraySpeed) = New KeyValuePair(Of Double, Double)(bytesDownloadedSinceLastTimer, time)
                            bytesDownloadedSinceLastTimer = 0

                            Dim key As String = worker.ChunkIndex.ToString
                            Dim avgSpeed As Integer = CalculateAvgSpeed(readings, Me.StopWatchCyclesAmount, arraySpeed)
                            SyncLock Mutex
                                m_currentSpeed(key) = avgSpeed
                            End SyncLock

                        End If
                    End While


                Finally
                    speedTimer.Stop()
                    ThrottledStreamController.GetController.RemoveStream(TStream)
                    Stream.Close()
                End Try



                Log.WriteInfo("Finishing connection - " & file.Name & " - chunk downloaded")
            Finally
                If webResp IsNot Nothing Then webResp.Close()
            End Try

            fireEventFromDownloader(worker, [Event].FileDownloadStopped)
        Catch ex As Exception
            Log.WriteError("Error in Downloader.DoWork: " & ex.ToString)
        End Try
    End Sub

    Public Function FlushToDisk(worker As Object, filePath As String, _
                                ByRef BufferDisk As Byte(), ByRef CurrentBufferSize As Integer, _
                                ByRef Chunk As DataPart.Chunk, _
                                Optional ByVal forceSync As Boolean = False) As Boolean

        '''''''''''''''''''''''''
        'Dim BufferDisk2(Me.BufferSize - 1) As Byte
        'For tempIndex As Integer = 0 To CurrentBufferSize - 1 Step oCipher.GetBlockSize
        '    oCipher.ProcessBlock(BufferDisk, tempIndex, BufferDisk2, tempIndex)
        '    'Buffer.BlockCopy(BufferDisk, tempIndex, BufferDisk2, tempIndex, oCipher.GetBlockSize)
        'Next

        '''''''''''''''''''''''


        ' Block alignment guard. The CTR keystream can only be seeked in whole
        ' 16-byte blocks: if a failed/interrupted download leaves Chunk.Index at a
        ' non-aligned offset, the retry would resume decryption with the keystream
        ' of the wrong block and corrupt EVERYTHING written after that point
        ' (verified root cause of "size correct but file damaged" downloads).
        ' Unless this write reaches the end of the chunk (no further data follows),
        ' round the persisted progress down to a 16-byte boundary. The dropped
        ' tail (< 16 bytes of already-decrypted data) is simply re-fetched on retry.
        If Chunk.Index + CurrentBufferSize < Chunk.Size AndAlso (CurrentBufferSize And 15) <> 0 Then
            Dim originalSize As Integer = CurrentBufferSize
            Dim roundedSize As Integer = CurrentBufferSize And Not 15
            Log.WriteWarning("FlushToDisk: rounding buffered bytes down to a 16-byte boundary (" &
                             originalSize & " -> " & roundedSize & ") to keep the resume offset block-aligned")
            Dim discarded As Integer = originalSize - roundedSize
            If discarded > 0 Then
                SyncLock Mutex
                    m_currentFileProgress -= discarded
                    If m_currentFileProgress < 0 Then m_currentFileProgress = 0
                    m_totalProgress -= discarded
                    If m_totalProgress < 0 Then m_totalProgress = 0
                End SyncLock
            End If
            CurrentBufferSize = roundedSize
            If CurrentBufferSize = 0 Then Return Chunk.Index < Chunk.Size
        End If

        Dim IndexProgress As Long = Chunk.Index + CurrentBufferSize
        If IndexProgress >= Chunk.Size Then
            IndexProgress = Chunk.Size
        End If
        Dim isFinal As Boolean = (IndexProgress >= Chunk.Size)
        ' 阈值判断在锁外读 SyncedIndex/m_lastFsyncUtc：仅决定本次是否 fsync，
        ' 误判只造成多/少一次 fsync，不变量由锁内更新 + XML 钳制保证。
        Dim syncNow As Boolean = forceSync OrElse isFinal OrElse _
            (IndexProgress - Chunk.SyncedIndex >= FsyncEveryBytes) OrElse _
            (Date.UtcNow.Subtract(m_lastFsyncUtc).TotalSeconds >= FsyncEverySeconds)

        SyncLock MutexFile
            Dim writer As System.IO.FileStream = GetPartStream(filePath)
            writer.Position = Chunk.StartIndex + Chunk.Index
            writer.Write(BufferDisk, 0, CurrentBufferSize)
            ' 方案B：仅节流点 Flush(True)。Index 照常推进供进程内使用；
            ' 断电安全由 SyncedIndex 记账 + XML 钳制保证（红线）。
            If syncNow Then
                writer.Flush(True)
                m_lastFsyncUtc = Date.UtcNow
            End If
        End SyncLock

        Dim dWorker As DownloaderWorker = CType(worker, DownloaderWorker)

        File.GetDataPart.SetProgress(dWorker.ChunkIndex, IndexProgress)
        Chunk = File.GetDataPart.ChunkList(dWorker.ChunkIndex)
        If syncNow Then Chunk.SyncedIndex = IndexProgress

        CurrentBufferSize = 0

        Return Chunk.Index < Chunk.Size
    End Function

    ''' <summary>
    ''' P0-1 方案A：获取 .part 复用写流。调用方必须已持有 MutexFile。
    ''' 路径切换（新文件/重建后）时自动关闭旧流重开；只 open 不 close。
    ''' </summary>
    Private Function GetPartStream(ByVal filePath As String) As System.IO.FileStream
        If m_partStream IsNot Nothing Then
            If String.Equals(m_partStreamPath, filePath, StringComparison.OrdinalIgnoreCase) Then
                Return m_partStream
            End If
            ClosePartStream()
        End If
        m_partStream = New System.IO.FileStream(filePath, System.IO.FileMode.Open, System.IO.FileAccess.Write, System.IO.FileShare.ReadWrite, 64 * 1024, System.IO.FileOptions.RandomAccess)
        m_partStreamPath = filePath
        Return m_partStream
    End Function

    ''' <summary>
    ''' 关闭复用写流（幂等）。调用方必须已持有 MutexFile；重命名/删除 .part 前必须先调。
    ''' 流的 Flush(True) 由每次 FlushToDisk 保证，此处只做 Flush + Close，不改变持久化语义。
    ''' </summary>
    Private Sub ClosePartStream()
        If m_partStream IsNot Nothing Then
            Try
                m_partStream.Flush(True)
            Catch
            End Try
            Try
                m_partStream.Close()
            Catch
            End Try
            Try
                m_partStream.Dispose()
            Catch
            End Try
            m_partStream = Nothing
            m_partStreamPath = Nothing
        End If
    End Sub

    Private Sub ChunkDownloader_RunWorkerCompleted(ByVal sender As Object, ByVal e As System.ComponentModel.RunWorkerCompletedEventArgs)
        Dim worker As DownloaderWorker = CType(sender, DownloaderWorker)
        Dim ChunkFallido As Boolean
        SyncLock Mutex
            ChunkFallido = worker.ChunkDownloadFailed
            Dim chunk As DataPart.Chunk = worker.File.GetDataPart.ChunkList(worker.ChunkIndex)
            If chunk.Size > chunk.Index Then
                chunk.Available = True
            End If
            Dim key As String = worker.ChunkIndex.ToString
            If m_currentSpeed.ContainsKey(key) Then
                m_currentSpeed.Remove(key)
            End If
            RemoveHandler worker.DoWork, AddressOf ChunkDownloader_DoWork
            RemoveHandler worker.ProgressChanged, AddressOf ChunkDownloader_ProgressChanged
            RemoveHandler worker.RunWorkerCompleted, AddressOf ChunkDownloader_RunWorkerCompleted
            Me.listDownloaders.Remove(worker)
            worker.Dispose()
        End SyncLock
        If Not HasBeenCanceled Then

            If worker.ChunkDownloadFailed Then
                ' v2.5 beta: 配额期内不再空转重试,等待全局熔断解除后由调度器统一唤醒。
                If MegaQuotaManager.IsQuarantined() Then
                    Log.WriteInfo("Chunk failed during MEGA quota quarantine; worker will NOT restart until quota clears.")
                    Return
                End If
                ' Exponential backoff with jitter (cap ~30s).
                ' 该事件处理器被编组回 UI 线程执行(RunWorkerCompleted),在这里 Thread.Sleep
                ' 忙等会冻结界面(最长约 16.5 秒)——把退避和重启移到线程池执行
                System.Threading.Tasks.Task.Run(Sub()
                                                   Dim attempt As Integer = Math.Min(10, Math.Max(0, Me.listDownloaders.Count))
                                                   Dim baseMs As Integer = CInt(Math.Min(30000, 1000 * Math.Pow(2, Math.Min(4, attempt))))
                                                   Dim jitter As Integer = (Now.Millisecond Mod 500)
                                                   Dim TiempoEspera As Double = baseMs + jitter
                                                   Dim Limite As Date = Now.AddMilliseconds(TiempoEspera)
                                                   While Now < Limite
                                                       System.Threading.Thread.Sleep(50)
                                                       If HasBeenCanceled Then
                                                           Return
                                                       End If
                                                   End While
                                                   ' Lanzar nuevo worker
                                                   NewDownloaderWorker()
                                               End Sub)
            Else
                ' Lanzar nuevo worker
                NewDownloaderWorker()
            End If
        End If
    End Sub



    Private Sub fireEventFromBgw(ByVal eventName As [Event])
        bgwDownloader.ReportProgress(InvokeType.EventRaiser, eventName)
    End Sub

    Private Sub fireEventFromDownloader(ByVal d As DownloaderWorker, ByVal eventName As [Event])
        d.ReportProgress(InvokeType.EventRaiser, eventName)
    End Sub


    Private Shared Function CalculateAvgSpeed(readings As Long, StopWatchCyclesAmount As Integer, arraySpeed As KeyValuePair(Of Double, Double)()) As Integer
        Dim TotalDescargado As Double = 0
        Dim TiempoDescarga As Double = 0
        Dim index As Integer = StopWatchCyclesAmount - 1

        ' Al menos 5 ciclos, para evitar picos al inicio
        If readings < 5 And StopWatchCyclesAmount > 5 Then
            index = -1
        ElseIf readings < StopWatchCyclesAmount Then
            index = CInt(readings) ' Solo la media de los elementos que hemos metido (el resto es basura)
        End If

        For i As Integer = 0 To index
            Dim item As KeyValuePair(Of Double, Double) = arraySpeed(i)
            TotalDescargado += item.Key
            TiempoDescarga += item.Value
        Next
        If TiempoDescarga = 0 Then TiempoDescarga = 1
        Return CInt(TotalDescargado / TiempoDescarga)
    End Function

    Private Sub NewDownloaderWorker()
        SyncLock Mutex
            Dim part As Int32? = Me.File.GetDataPart.NextAvailablePartIndex
            If part.HasValue Then
                Dim worker As New DownloaderWorker(part.Value, Me.File)
                worker.WorkerSupportsCancellation = True
                worker.WorkerReportsProgress = True
                AddHandler worker.DoWork, AddressOf ChunkDownloader_DoWork
                AddHandler worker.ProgressChanged, AddressOf ChunkDownloader_ProgressChanged
                AddHandler worker.RunWorkerCompleted, AddressOf ChunkDownloader_RunWorkerCompleted
                Me.listDownloaders.Add(worker)
                worker.RunWorkerAsync()
            End If
        End SyncLock
    End Sub

    Private Sub cleanUpFile()
        If File IsNot Nothing Then
            Dim fullPath As String = PathGuard.GetSafeFilePathUnderRoot(Me.LocalDirectory, Me.File.Name)
            PathGuard.EnsurePathUnderRoot(Me.LocalDirectory, fullPath, allowRoot:=False)
            If IO.File.Exists(fullPath) Then IO.File.Delete(fullPath)
        End If
    End Sub

    Private Sub calculateFilesSize()
        fireEventFromBgw([Event].CalculationFileSizesStarted)


        bgwDownloader.ReportProgress(InvokeType.CalculatingFileNrRaiser, Nothing)
        Try
            Dim size As Long = ProbeRemoteFileSize(Me.File.Path)
            If size < 0 Then Throw New ApplicationException("Could not determine remote file size.")
            m_totalSize = size
        Catch ex As Exception
            ReportQuotaIfMatch(ex)
            Throw New ApplicationException("Connection error: " & ex.Message)
        End Try

        fireEventFromBgw([Event].FileSizesCalculationComplete)
    End Sub

    Protected Overridable Sub Dispose(ByVal disposing As Boolean)
        If Not m_disposed Then
            If disposing Then
                Try
                    If bgwDownloader IsNot Nothing AndAlso bgwDownloader.IsBusy Then
                        bgwDownloader.CancelAsync()
                    End If
                    SyncLock Mutex
                        For Each w As DownloaderWorker In Me.listDownloaders.ToList()
                            If w.IsBusy Then w.CancelAsync()
                        Next
                    End SyncLock
                    Dim waitUntil As Date = Now.AddSeconds(5)
                    While Now < waitUntil
                        Dim busy As Boolean
                        SyncLock Me.Mutex
                            busy = Me.listDownloaders.Any(Function(x) x.IsBusy)
                        End SyncLock
                        If Not busy Then Exit While
                        System.Threading.Thread.Sleep(50)
                    End While
                Catch ex As Exception
                    Log.WriteError("FileDownloader.Dispose: error while cancelling workers: " & ex.ToString)
                End Try
                ' Dispose every worker we spawned, not just the outer bgwDownloader.
                ' Workers that were still running when CancelAsync was called will not
                ' have hit their RunWorkerCompleted handler, so they must be disposed here.
                SyncLock Mutex
                    For Each w As DownloaderWorker In Me.listDownloaders.ToList()
                        Try
                            w.Dispose()
                        Catch ex As Exception
                            Log.WriteError("FileDownloader.Dispose: worker dispose failed: " & ex.ToString)
                        End Try
                    Next
                    Me.listDownloaders.Clear()
                End SyncLock
                If bgwDownloader IsNot Nothing Then
                    ' 等主 worker 退出后再做收尾,避免 DoWork 仍在使用共享状态时被 Dispose 干扰
                    Dim waitMain As Date = Now.AddSeconds(10)
                    While bgwDownloader.IsBusy AndAlso Now < waitMain
                        System.Threading.Thread.Sleep(50)
                    End While
                    bgwDownloader.Dispose()
                End If

                ' 确定性释放本实例的内核对象。SyncLock 锁对象无需释放；
                ' trigger（ManualResetEvent）仍需关闭。复用写流先关，避免 .part 句柄泄漏
                ' 挡住后续删除/重建。
                Try
                    SyncLock MutexFile
                        ClosePartStream()
                    End SyncLock
                Catch ex As Exception
                    Log.WriteWarning("FileDownloader.Dispose: part stream close failed: " & ex.ToString)
                End Try
                Try
                    If trigger IsNot Nothing Then
                        trigger.Set() ' 唤醒可能还在 Pause 等待的路径,让其尽快退出
                        trigger.Close()
                    End If
                Catch ex As Exception
                    Log.WriteWarning("FileDownloader.Dispose: trigger close failed: " & ex.ToString)
                End Try
                ' SyncLock 锁对象无需释放（此前 Mutex 每个下载任务泄漏 3 个内核句柄，
                ' 改轻量锁后泄漏源头消失）。trigger 是 ManualResetEvent，仍需关闭。
                ' 复用写流已在上一步关闭（MutexFile 保护下）。
            End If
            Me.File = Nothing
        End If
        m_disposed = True
    End Sub
#End Region

#Region "Properties"
    ''' <summary>Gets or sets the list of files to download</summary>
    Public Property File() As FileInfo
        Get
            Return m_file
        End Get
        Set(ByVal value As FileInfo)
            If Me.IsBusy Then
                Throw New InvalidOperationException("You can not change the file during the download")
            Else
                If Me.m_file IsNot value Then m_file = value
            End If
        End Set
    End Property

    ''' <summary>Gets or sets the local directory in which files will be stored</summary>
    Public Property LocalDirectory() As String
        Get
            Return m_localDirectory
        End Get
        Set(ByVal value As String)
            Dim value2 As String = value
            For Each c As Char In Path.GetInvalidPathChars()
                value2 = value2.Replace(c, " "c)
            Next

            If value2 <> Me.LocalDirectory Then
                m_localDirectory = value2
            End If
        End Set
    End Property

    ''' <summary>Gets or sets if the FileDownloader should support total progress statistics. Note that when enabled, the FileDownloader will have to get the size of each file before starting to download them, which can delay the operation.</summary>
    Public Property SupportsProgress() As Boolean
        Get
            Return m_supportsProgress
        End Get
        Set(ByVal value As Boolean)
            If Me.IsBusy Then
                Throw New InvalidOperationException("You can not change the SupportsProgress property during the download")
            Else
                m_supportsProgress = value
            End If
        End Set
    End Property

    ''' <summary>Gets or sets if when the download process is cancelled the complete downloads should be deleted</summary>
    Public Property DeleteCompletedFilesAfterCancel() As Boolean
        Get
            Return m_deleteCompletedFiles
        End Get
        Set(ByVal value As Boolean)
            m_deleteCompletedFiles = value
        End Set
    End Property

    ''' <summary>Gets or sets if when the download process is cancelled the downloads should be deleted</summary>
    Public Property DeleteFilesAfterCancel() As Boolean
        Get
            Return m_deleteFiles
        End Get
        Set(ByVal value As Boolean)
            m_deleteFiles = value
        End Set
    End Property

    Public ReadOnly Property OpenConnections As Int32
        Get
            SyncLock Mutex
                Return Me.listDownloaders.Count
            End SyncLock
        End Get
    End Property

    Public Property NumConnections As Int32
        Get
            Return Me.m_num_connections
        End Get
        Set(value As Int32)
            If value > 0 Then
                Me.m_num_connections = value
            Else
                Throw New InvalidOperationException("The NumConnections needs to be greater than 0")
            End If
        End Set
    End Property

    Public Property PartsPerFile As Int32
        Get
            Return Me.m_parts_per_file
        End Get
        Set(value As Int32)
            If value > 0 Then
                Me.m_parts_per_file = value
            Else
                Throw New InvalidOperationException("The PartsPerFile needs to be greater than 0")
            End If
        End Set
    End Property

    Public Property BufferSize As Int32
        Get
            Return m_bufferSize
        End Get
        Set(value As Int32)
            If value < PackageSize Then
                Throw New InvalidOperationException("The BufferSize needs to be greater than the PackageSize")
            ElseIf value > 0 Then
                m_bufferSize = value
            Else
                Throw New InvalidOperationException("The BufferSize needs to be greater than 0")
            End If
        End Set
    End Property

    ''' <summary>Gets or sets the size of the blocks that will be downloaded</summary>
    Public Property PackageSize() As Int32
        Get
            Return m_packageSize
        End Get
        Set(ByVal value As Int32)
            If value > BufferSize Then
                Throw New InvalidOperationException("The BufferSize needs to be greater than the PackageSize")
            ElseIf value > 0 Then
                m_packageSize = value
            Else
                Throw New InvalidOperationException("The PackageSize needs to be greater than 0")
            End If
        End Set
    End Property

    Public Sub setBufferAndPackageSize(bufferSize As Int32, packageSize As Int32)
        Const MaxPackage As Integer = 1024 * 1024 ' 1 MiB
        Const MaxBuffer As Integer = 16 * 1024 * 1024 ' 16 MiB
        If bufferSize <= 0 Then
            bufferSize = 750 * 1024
            Log.WriteWarning("Warning: BufferSize is 0 or less, setting default value 750KB")
        End If
        If packageSize <= 0 Then
            packageSize = 50 * 1024
            Log.WriteWarning("Warning: PackageSize is 0 or less, setting default value 50KB")
        End If
        If packageSize > MaxPackage Then packageSize = MaxPackage
        If bufferSize > MaxBuffer Then bufferSize = MaxBuffer
        If bufferSize < packageSize Then
            Log.WriteWarning("Warning: BufferSize needs to be greater than PackageSize, setting 5x")
            bufferSize = Math.Min(MaxBuffer, 5 * packageSize)
        End If
        m_packageSize = packageSize
        m_bufferSize = bufferSize
    End Sub
   

    ''' <summary>Gets or sets the amount of blocks that need to be downloaded before the progress speed is re-calculated. Note: setting this to a low value might decrease the accuracy</summary>
    Public Property StopWatchCyclesAmount() As Int32
        Get
            Return m_stopWatchCycles
        End Get
        Set(ByVal value As Int32)
            If value > 0 Then
                m_stopWatchCycles = value
            Else
                Throw New InvalidOperationException("The StopWatchCyclesAmount needs to be greather then 0")
            End If
        End Set
    End Property

    ''' <summary>Gets or sets the busy state of the FileDownloader</summary>
    Public Property IsBusy() As Boolean
        Get
            Return m_busy
        End Get
        Set(ByVal value As Boolean)
            If Me.IsBusy <> value Then
                m_busy = value
                m_canceled = Not value
                If Me.IsBusy Then
                    ' ②:复用同一 Downloader 重试时 m_currentFileProgress 残留旧值,
                    ' downloadFile() 入口又 += chunk.Index 会双计(瞬间满格)。与 m_totalProgress 同清零,
                    ' 入口重播 chunk.Index 即得正确值;暂停/恢复不走 IsBusy,不受影响。
                    m_currentFileProgress = 0
                    m_totalProgress = 0
                    bgwDownloader.RunWorkerAsync()
                    RaiseEvent Started(Me, New EventArgs)
                    RaiseEvent IsBusyChanged(Me, New EventArgs)
                    RaiseEvent StateChanged(Me, New EventArgs)
                Else
                    bgwDownloader.CancelAsync()
                    If Me.IsPaused Then
                        trigger.Set()
                    End If
                    m_paused = False

                    RaiseEvent CancelRequested(Me, New EventArgs)
                    RaiseEvent StateChanged(Me, New EventArgs)
                End If
            End If
        End Set
    End Property

    ''' <summary>Gets or sets the pause state of the FileDownloader</summary>
    Public Property IsPaused() As Boolean
        Get
            Return m_paused
        End Get
        Set(ByVal value As Boolean)
            If Me.IsBusy Then
                If value <> Me.IsPaused Then
                    m_paused = value
                    If Me.IsPaused Then
                        trigger.Reset()
                        RaiseEvent Paused(Me, New EventArgs)
                    Else
                        trigger.Set()
                        RaiseEvent Resumed(Me, New EventArgs)
                    End If
                    RaiseEvent IsPausedChanged(Me, New EventArgs)
                    RaiseEvent StateChanged(Me, New EventArgs)
                End If
            End If
        End Set
    End Property

    ''' <summary>Gets if the FileDownloader can start</summary>
    Public ReadOnly Property CanStart() As Boolean
        Get
            Return Not Me.IsBusy
        End Get
    End Property

    ''' <summary>Gets if the FileDownloader can pause</summary>
    Public ReadOnly Property CanPause() As Boolean
        Get
            Return Me.IsBusy And Not Me.IsPaused And Not bgwDownloader.CancellationPending
        End Get
    End Property

    ''' <summary>Gets if the FileDownloader can resume</summary>
    Public ReadOnly Property CanResume() As Boolean
        Get
            Return Me.IsBusy And Me.IsPaused And Not bgwDownloader.CancellationPending
        End Get
    End Property

    ''' <summary>Gets if the FileDownloader can stop</summary>
    Public ReadOnly Property CanStop() As Boolean
        Get
            Return Me.IsBusy And Not bgwDownloader.CancellationPending
        End Get
    End Property

    ''' <summary>Gets the total size of all files together. Only avaible when the FileDownloader suports progress</summary>
    Public ReadOnly Property TotalSize() As Int64
        Get
            If Me.SupportsProgress Then
                Return m_totalSize
            Else
                Throw New InvalidOperationException("This FileDownloader that it doesn't support progress. Modify SupportsProgress to state that it does support progress to get the total size.")
            End If
        End Get
    End Property

    ''' <summary>Gets the total amount of bytes downloaded</summary>
    Public ReadOnly Property TotalProgress() As Int64
        Get
            Return m_totalProgress
        End Get
    End Property

    ''' <summary>Gets the amount of bytes downloaded of the current file</summary>
    Public ReadOnly Property CurrentFileProgress() As Int64
        Get
            Return m_currentFileProgress
        End Get
    End Property

    ''' <summary>Gets the total download percentage. Only avaible when the FileDownloader suports progress</summary>
    Public ReadOnly Property TotalPercentage(Optional ByVal decimals As Int32 = 0) As Double
        Get
            If Me.SupportsProgress Then
                If Me.TotalSize <= 0 Then Return If(Me.TotalProgress > 0, 100.0, 0.0)
                Dim Perc As Double = Me.TotalProgress / Me.TotalSize * 100
                If Perc > 100 Then Perc = 100
                Return Math.Round(Perc, decimals)
            Else
                Throw New InvalidOperationException("This FileDownloader that it doesn't support progress. Modify SupportsProgress to state that it does support progress.")
            End If
        End Get
    End Property

    ''' <summary>Gets the percentage of the current file progress</summary>
    Public ReadOnly Property CurrentFilePercentage(Optional ByVal decimals As Int32 = 0) As Double
        Get
            If Me.CurrentFileSize <= 0 Then Return If(Me.CurrentFileProgress > 0, 100.0, 0.0)
            Dim Perc As Double = Me.CurrentFileProgress / Me.CurrentFileSize * 100
            If Perc > 100 Then Perc = 100
            Return Math.Round(Perc, decimals)
        End Get
    End Property

    ''' <summary>Gets the current download speed in bytes</summary>
    Public ReadOnly Property DownloadSpeed() As Int32
        Get
            SyncLock Mutex
                Dim Speed As Int32 = 0
                For Each value As Int32 In m_currentSpeed.Values
                    Speed += value
                Next
                Return Speed
            End SyncLock

        End Get
    End Property

    ' ''' <summary>Gets the FileInfo object representing the current file</summary>
    'Public ReadOnly Property CurrentFile() As FileInfo
    '    Get
    '        Return Me.File
    '    End Get
    'End Property

    ''' <summary>Gets the size of the current file in bytes</summary>
    Public ReadOnly Property CurrentFileSize() As Int64
        Get
            Return m_currentFileSize
        End Get
    End Property

    ''' <summary>Gets if the last download was canceled by the user</summary>
    Private ReadOnly Property HasBeenCanceled() As Boolean
        Get
            Return m_canceled
        End Get
    End Property
#End Region

End Class
#End Region
