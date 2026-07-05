Imports System.IO
Imports System.Net
Imports System.ComponentModel
Imports System.Reflection


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

            ' Ex: file of 10000 bytes, 4 chunks, the fourth chunk has 60% completed:
            ' * StartIndex: 7500
            ' * Size: 2500
            ' * Index: 1500

            Public Sub New()
                Me.StartIndex = 0
                Me.Size = 0
                Me.Index = 0
                Me.Available = True
            End Sub
        End Class


        Public ChunkList As Generic.List(Of Chunk)
        Public AllFinished As Boolean
        Private _Mutex As System.Threading.Mutex

        Public Sub New()
            Me._Mutex = New System.Threading.Mutex
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
            Me._Mutex.WaitOne()
            For Each c1 As Chunk In Me.ChunkList
                If c1.Index <> c1.Size Then
                    c1.Available = True
                End If
            Next
            Me._Mutex.ReleaseMutex()
        End Sub


        Public ReadOnly Property NextAvailablePartIndex As Int32?
            Get
                Me._Mutex.WaitOne()
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
                Me._Mutex.ReleaseMutex()
                Return ret
            End Get
        End Property

        Public Sub SetProgress(chunkIndex As Int32, index As Long)
            Me._Mutex.WaitOne()
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
            Me._Mutex.ReleaseMutex()
        End Sub

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
        Private _Mutex As System.Threading.Mutex
        ''' <summary>The name of the file</summary>
        Private _Name As String

        Public Property Name As String
            Get
                Return _Name
            End Get
            Set(value As String)
                Me._Name = value & ""
                For Each c As Char In System.IO.Path.GetInvalidFileNameChars
                    Me._Name = Me.Name.Replace(c, " "c)
                Next
            End Set
        End Property
        ''' <summary>Create a new instance of FileInfo</summary>
        ''' <param name="path">The complete path of the file (directory + filename)</param>
        Public Sub New(ByVal path As String)
            Me._Mutex = New System.Threading.Mutex
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
                Me._Mutex.WaitOne()
                If _dataPart Is Nothing Then
                    _dataPart = New DataPart(Me.Size, Me.NumParts)
                End If
                Me._Mutex.ReleaseMutex()
                Return _dataPart
            End Get
        End Property

        Public Sub SetDataPart(ByVal d As DataPart)
            If d IsNot Nothing Then
                Me._dataPart = d
                Me._dataPart.ResetAvailableParts()
            End If
        End Sub

        Public Property Size As Long
            Get
                Return Me._Size
            End Get
            Set(value As Long)
                Me._Size = value
                If DataPartInitialized Then
                    Me._Mutex.WaitOne()
                    Dim Tamano As Long = 0

                    For Each c As DataPart.Chunk In _dataPart.ChunkList
                        'Tamano += c.Size
                        ' Now the chunks can be resized depending on MEGA response... so we will take the biggest chunk
                        If c.StartIndex + c.Size > Tamano Then
                            Tamano = c.StartIndex + c.Size
                        End If
                    Next

                    Me._Mutex.ReleaseMutex()
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
    Private Mutex As New System.Threading.Mutex() ' Sync workers for object manipulation
    Private MutexFile As New System.Threading.Mutex() ' Sync workers for disk write
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
        Dim formattedSize As Double = size
        Dim sizeIndex As Int32 = 0
        While formattedSize >= 1024 And sizeIndex < sizes.Length
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
        Dim formattedSize As Double = size
        Dim sizeIndex As Int32 = 0
        While formattedSize >= 1000 And sizeIndex < sizes.Length
            formattedSize /= 1000
            sizeIndex += 1
        End While
        Return Math.Round(formattedSize, decimals).ToString & sizes(sizeIndex)
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
                        cleanUpFile()
                    End If
                    e.Cancel = True
                End If
            End If
        Catch ex As Exception
            Log.WriteError("Error in bgwDownloader.DoWork: " & ex.ToString)
            MessageBox.Show("Error: " & ex.ToString, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub downloadFile()

        Dim file As FileInfo = Me.m_file

        Log.WriteWarning("Starting file download " & file.Name)

        Dim FicheroPART As String = Path.Combine(Me.LocalDirectory, file.Name & ".part")
        Dim FicheroReal As String = Path.Combine(Me.LocalDirectory, file.Name)

        Dim exc As Exception = Nothing

        Try

            ' Get size
            If file.Size = 0 Then
                Try
                    Dim webReq As HttpWebRequest = Nothing
                    Dim webResp As HttpWebResponse = Nothing
                    ' Obtenemos el tamaño total
                    webReq = Conexion.CreateHttpWebRequest(Me.File.Path)
                    webReq.Method = "HEAD"
                    webResp = CType(webReq.GetResponse, HttpWebResponse)

                    Dim TotalSize As Long = webResp.ContentLength
                    webResp.Close()

                    file.Size = TotalSize
                    m_currentFileSize = TotalSize
                    Log.WriteInfo("File size " & file.Name & ": " & TotalSize)
                Catch ex As WebException
                    exc = ex
                Catch ex As Exception
                    exc = ex
                End Try
            End If

            ' Check errors
            If exc IsNot Nothing Then
                Log.WriteError("Error downloading file " & file.Name & " - " & exc.ToString)
                bgwDownloader.ReportProgress(InvokeType.FileDownloadFailedRaiser, exc)
                exc = Nothing
                Exit Try
            End If

            ' Check size
            If Not System.IO.File.Exists(FicheroPART) Then

                fireEventFromBgw([Event].CreatingFilesLocal)
                ' Creamos un fichero vacío con ese tamaño
                Me.MutexFile.WaitOne()
                Try
                    Using Stream As System.IO.FileStream = System.IO.File.Create(FicheroPART, 64 * 1024, FileOptions.RandomAccess)
                        Stream.SetLength(file.Size)
                        Stream.Flush()
                    End Using
                Catch ex As Exception
                    exc = ex
                Finally
                    Me.MutexFile.ReleaseMutex()
                End Try
            Else
                Me.MutexFile.WaitOne()
                Try
                    Dim inf As New System.IO.FileInfo(FicheroPART)
                    If inf.Length <> file.Size Then
                        exc = New ApplicationException("The file exists and does not have the expected size [" & inf.Length & " - " & file.Size & "]")
                    End If
                Finally
                    Me.MutexFile.ReleaseMutex()
                End Try

            End If

            ' Check errors
            If exc IsNot Nothing Then
                Log.WriteError("Error creating file on disk " & file.Name & " - " & exc.ToString)
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

            For i As Integer = 1 To Me.NumConnections
                Log.WriteDebug("Event raised for starting a new connection")
                bgwDownloader.ReportProgress(InvokeType.StartDownloaderRaiser, Nothing)
            Next
            Do
                System.Threading.Thread.Sleep(100)

                trigger.WaitOne()
                If bgwDownloader.CancellationPending Then
                    Log.WriteDebug("Aborting connection - stop requested")
                    Me.Mutex.WaitOne()
                    Try
                        For Each w As DownloaderWorker In Me.listDownloaders
                            If w.IsBusy Then
                                w.CancelAsync()
                            End If
                        Next
                    Finally
                        Me.Mutex.ReleaseMutex()
                    End Try
                    Log.WriteWarning("File download stopped - " & file.Name)
                    Exit Do
                End If

                If file.GetDataPart.AllFinished Then
                    Exit Do
                End If
            Loop
        Catch ex As Exception
            exc = ex
        Finally
            If exc IsNot Nothing Then
                Log.WriteError("Error trying to download file " & file.Name & " - " & exc.ToString)
                bgwDownloader.ReportProgress(InvokeType.FileDownloadFailedRaiser, exc)
            End If
        End Try

        Try
            If file.GetDataPart.AllFinished Then
                Me.MutexFile.WaitOne()
                Try
                    If System.IO.File.Exists(FicheroPART) Then

                        If Not System.IO.File.Exists(FicheroReal) Then
                            Log.WriteInfo("Rename from " & FicheroPART & " to " & FicheroReal)
                            FileSystem.Rename(FicheroPART, FicheroReal)
                        Else

                            ' File exists, create someone like "file (2).txt"

                            Dim extension As String = If(FicheroReal.LastIndexOf("."c) > 0, FicheroReal.Substring(FicheroReal.LastIndexOf("."c) + 1), "")
                            Dim fileWithoutExtension As String = If(FicheroReal.LastIndexOf("."c) > 0, FicheroReal.Substring(0, FicheroReal.LastIndexOf("."c)), "")

                            Dim i As Integer = 1
                            Do
                                i += 1
                                Dim FicheroReal2 As String = fileWithoutExtension & " (" & i & ")" & If(String.IsNullOrEmpty(extension), "", "." & extension)
                                If Not System.IO.File.Exists(FicheroReal2) Then
                                    Log.WriteInfo("Rename from " & FicheroPART & " to " & FicheroReal2)
                                    FileSystem.Rename(FicheroPART, FicheroReal2)
                                    Exit Do
                                End If

                                If i > 9999 Then Exit Do
                            Loop

                        End If

                    End If
                Finally
                    Me.MutexFile.ReleaseMutex()
                End Try
                Log.WriteWarning("File downloaded successfully")
                fireEventFromBgw([Event].FileDownloadSucceeded)
            End If
        Catch ex As Exception
            Log.WriteError("Error checking if the download has stopped " & file.Name & " - " & exc.ToString)
            bgwDownloader.ReportProgress(InvokeType.FileDownloadFailedRaiser, exc)
        End Try
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


            Dim FicheroPART As String = Me.LocalDirectory & "\" & file.Name & ".part"

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

                    ' Pequeño hack para soportar ficheros de más de 2 GB:
                    ' http://forums.codeguru.com/showthread.php?467570-WebRequest.AddRange-what-about-files-gt-2gb&p=1794639
                    ' http://www.freesoft.org/CIE/RFC/2068/198.htm
                    Dim key As String = "Range"
                    Dim val As String = String.Format("bytes={0}-{1}", ChunkStart, ChunkStop)

                    Dim method As MethodInfo = GetType(WebHeaderCollection).GetMethod("AddWithoutValidate", BindingFlags.Instance Or BindingFlags.NonPublic)
                    method.Invoke(webReq.Headers, New Object() {key, val})

                    webResp = CType(webReq.GetResponse, HttpWebResponse)

                    Dim contentRangeHeader As String = webResp.Headers.Item("Content-Range")
                    ' Content-Range: bytes 1769472-2147111/2147112
                    If Not String.IsNullOrEmpty(contentRangeHeader) _
                        AndAlso contentRangeHeader.StartsWith("bytes ") _
                        AndAlso contentRangeHeader.Contains("-") Then

                        Dim tokenInit As String = contentRangeHeader.Substring(6).Split("-"c)(0)
                        If IsNumeric(tokenInit) Then
                            Dim InitDescarga As Long = CLng(tokenInit)
                            If ChunkStart <> InitDescarga Then

                                If InitDescarga < Chunk.StartIndex Then
                                    ' Before StartIndex (before chunk starts) -> make chunk bigger

                                    Chunk.Size += Chunk.StartIndex - InitDescarga
                                    Chunk.StartIndex = InitDescarga
                                    Chunk.Index = 0

                                    ChunkStart = Chunk.StartIndex + Chunk.Index ' Update chunkstart & chunkstop
                                    ChunkStop = Chunk.StartIndex + Chunk.Size - 1

                                ElseIf InitDescarga < Chunk.StartIndex + Chunk.Index Then
                                    ' After StartIndex and before current index -> start index from the point Mega sends the file

                                    Chunk.Index -= Chunk.StartIndex + Chunk.Index - InitDescarga
                                    ChunkStart = Chunk.StartIndex + Chunk.Index ' Update chunkstart & chunkstop
                                    ChunkStop = Chunk.StartIndex + Chunk.Size - 1

                                Else
                                    exc = New ApplicationException(String.Format("Error when downloading chunk {0}-{1}: received {2}", _
                                                                                 ChunkStart, _
                                                                                 ChunkStop, _
                                                                                 InitDescarga))
                                End If


                            End If
                        End If


                    End If

                Catch ex As WebException
                    exc = ex
                Catch ex As Exception
                    exc = ex
                End Try


                If exc IsNot Nothing Then
                    worker.ChunkDownloadFailed = True
                    Log.WriteError("Connection error when downloading file " & file.Name & " - " & exc.ToString)
                    worker.ReportProgress(InvokeType.ChunkDownloadFailedRaiser, exc)
                    Exit Try
                End If


                Dim FileKeyWithoutN As String = FileKey
                If FileKeyWithoutN.Contains("=###n=") Then
                    FileKeyWithoutN = FileKeyWithoutN.Substring(0, FileKey.IndexOf("=###n="))
                End If

                ' Inicializamos el Cipher
                ' Como no tenemos acceso al contador interno, tenemos que recorrerlo secuencialmente (chapu, pero bueno)
                Log.WriteDebug("Starting SicBlockCipher seek position " & ChunkStart)
                Dim crono As Date = Now
                Dim oCipher As Criptografia.SicSeekableBlockCipher = Criptografia.GetInstaceCipher(FileKeyWithoutN)
                oCipher.IncrementCounter(CInt(Math.Ceiling(ChunkStart / oCipher.GetBlockSize)))
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
                            speedTimer.Stop()
                            Exit Sub
                        End If

                        ' Not trigger.WaitOne(0) -> IsPaused? -> Flush to disk
                        If currentBuffersize > 0 And _
                            (Chunk.Index + currentBuffersize >= Chunk.Size Or _
                            Not trigger.WaitOne(0) Or _
                            worker.CancellationPending Or _
                            currentBuffersize + Me.PackageSize > Me.BufferSize) Then

                            'Dim [continue] As Boolean = FlushToDisk(worker, FicheroPART, oCipher, BufferDisk, currentBuffersize, Chunk)
                            Dim [continue] As Boolean = FlushToDisk(worker, FicheroPART, BufferDisk, currentBuffersize, Chunk)
                            If Not [continue] Then
                                Exit While
                            End If

                        End If

                        trigger.WaitOne()
                        If worker.CancellationPending Then
                            Log.WriteDebug("Download stopped - " & file.Name)
                            speedTimer.Stop()
                            Exit Sub
                        End If

                        Try

                            ' Fill the packageSize
                            currentPackageSize = 0

                            While currentPackageSize < Me.PackageSize
                                Dim bytesDownloaded As Integer = TStream.Read(BufferMem, currentPackageSize, Me.PackageSize - currentPackageSize)
                                currentPackageSize += bytesDownloaded

                                ' bytesDownloaded = 0 -> end of stream 
                                ' https://msdn.microsoft.com/en-us/library/29tb55d8%28v=VS.100%29.aspx
                                If bytesDownloaded = 0 Or _
                                    Chunk.Index + currentBuffersize + currentPackageSize >= Chunk.Size Then
                                    Exit While
                                End If
                            End While


                            'currentPackageSize = TStream.Read(BufferMem, 0, Me.PackageSize)

                            '' Forzamos a que haya bloques de 16 bytes
                            'Dim diff As Integer = oCipher.GetBlockSize - currentPackageSize Mod oCipher.GetBlockSize
                            'If diff <> oCipher.GetBlockSize Then

                            '    If Chunk.Index + currentBuffersize + currentPackageSize < Chunk.Size Then
                            '        currentPackageSize += TStream.Read(BufferMem, currentPackageSize, diff)
                            '    End If

                            '    '' New code... it works... although currentBuffersize is not used, why? :S
                            '    ' Dont release yet...
                            '    'If Chunk.Index + currentPackageSize < Chunk.Size Then
                            '    '    currentPackageSize += TStream.Read(readBytes, currentPackageSize, diff)
                            '    'Else
                            '    '    currentPackageSize += diff
                            '    'End If

                            'End If

                            For tempIndex As Integer = 0 To currentPackageSize - 1 Step oCipher.GetBlockSize
                                oCipher.ProcessBlock(BufferMem, tempIndex, BufferDisk, currentBuffersize + tempIndex)
                            Next

                            'If Now.Millisecond < 200 Then
                            '    Throw New ApplicationException("TEST PRUEBA STOP, SIMULAMOS PROBLEMA CONEXION, ABORTAMOS CONEXION")
                            'End If

                        Catch ex As WebException
                            exc = ex
                        Catch ex As Exception
                            exc = ex
                        End Try

                        If exc IsNot Nothing Then
                            worker.ChunkDownloadFailed = True
                            worker.ReportProgress(InvokeType.ChunkDownloadFailedRaiser, exc)
                            speedTimer.Stop()
                            Exit Sub
                        End If

                        Me.Mutex.WaitOne()
                        m_currentFileProgress += currentPackageSize
                        m_totalProgress += currentPackageSize
                        Me.Mutex.ReleaseMutex()

                        bytesDownloadedSinceLastTimer += currentPackageSize

                        fireEventFromDownloader(worker, [Event].ProgressChanged)

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
                            Me.Mutex.WaitOne()
                            Try
                                m_currentSpeed(key) = avgSpeed
                            Finally
                                Me.Mutex.ReleaseMutex()
                            End Try

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
                                ByRef Chunk As DataPart.Chunk) As Boolean

        '''''''''''''''''''''''''
        'Dim BufferDisk2(Me.BufferSize - 1) As Byte
        'For tempIndex As Integer = 0 To CurrentBufferSize - 1 Step oCipher.GetBlockSize
        '    oCipher.ProcessBlock(BufferDisk, tempIndex, BufferDisk2, tempIndex)
        '    'Buffer.BlockCopy(BufferDisk, tempIndex, BufferDisk2, tempIndex, oCipher.GetBlockSize)
        'Next

        '''''''''''''''''''''''


        'Me.MutexFile.WaitOne()
        Try
            Dim writer As New FileStream(filePath, IO.FileMode.Open, FileAccess.Write, FileShare.ReadWrite, CurrentBufferSize, FileOptions.RandomAccess)
            writer.Position = Chunk.StartIndex + Chunk.Index
            writer.Write(BufferDisk, 0, CurrentBufferSize)
            writer.Close()
        Finally
            'Me.MutexFile.ReleaseMutex()
        End Try

        Dim IndexProgress As Long = Chunk.Index + CurrentBufferSize
        If IndexProgress >= Chunk.Size Then
            IndexProgress = Chunk.Size
        End If

        Dim dWorker As DownloaderWorker = CType(worker, DownloaderWorker)

        File.GetDataPart.SetProgress(dWorker.ChunkIndex, IndexProgress)
        Chunk = File.GetDataPart.ChunkList(dWorker.ChunkIndex)

        CurrentBufferSize = 0

        Return Chunk.Index < Chunk.Size
    End Function

    Private Sub ChunkDownloader_RunWorkerCompleted(ByVal sender As Object, ByVal e As System.ComponentModel.RunWorkerCompletedEventArgs)
        Dim worker As DownloaderWorker = CType(sender, DownloaderWorker)
        Dim ChunkFallido As Boolean
        Me.Mutex.WaitOne()
        Try
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
        Finally
            Me.Mutex.ReleaseMutex()
        End Try
        If Not HasBeenCanceled Then

            If worker.ChunkDownloadFailed Then
                ' Ha fallado, antes de reconectar esperamos unos segundos, entre 1 y 4
                Dim TiempoEspera As Double = 1000 + (Now.Millisecond * 4)
                Dim Limite As Date = Now.AddMilliseconds(TiempoEspera)
                While Now < Limite
                    System.Threading.Thread.Sleep(50)
                    If HasBeenCanceled Then
                        Exit Sub
                    End If
                End While
            End If

            ' Lanzar nuevo worker
            NewDownloaderWorker()
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
        Me.Mutex.WaitOne()
        Try
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
        Finally
            Me.Mutex.ReleaseMutex()
        End Try
    End Sub

    Private Sub cleanUpFile()
        If File IsNot Nothing Then
            Dim fullPath As String = Path.Combine(Me.LocalDirectory, Me.File.Name)
            If IO.File.Exists(fullPath) Then IO.File.Delete(fullPath)
        End If
    End Sub

    Private Sub calculateFilesSize()
        fireEventFromBgw([Event].CalculationFileSizesStarted)


        bgwDownloader.ReportProgress(InvokeType.CalculatingFileNrRaiser, Nothing)
        Try
            Dim webReq As HttpWebRequest = Conexion.CreateHttpWebRequest(Me.File.Path)
            webReq.Method = "HEAD"
            webReq.Timeout = 15000
            Dim webResp As HttpWebResponse = CType(webReq.GetResponse, HttpWebResponse)
            m_totalSize = webResp.ContentLength
            webResp.Close()
        Catch ex As Exception
            Throw New ApplicationException("Connection error: " & ex.Message)
        End Try

        fireEventFromBgw([Event].FileSizesCalculationComplete)
    End Sub

    Protected Overridable Sub Dispose(ByVal disposing As Boolean)
        If Not m_disposed Then
            If disposing Then
                ' Free other state (managed objects)
                bgwDownloader.Dispose()
            End If
            ' Free your own state (unmanaged objects)
            ' Set large fields to null
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
            Me.Mutex.WaitOne()
            Try
                Return Me.listDownloaders.Count
            Finally
                Me.Mutex.ReleaseMutex()
            End Try
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
        If bufferSize <= 0 Then
            ' The BufferSize needs to be greater than 0
            bufferSize = 750 * 1024
            Log.WriteWarning("Warning: BufferSize is 0 or less, setting default value 750KB")
        End If
        If packageSize <= 0 Then
            ' The PackageSize needs to be greater than 0
            packageSize = 50 * 1024
            Log.WriteWarning("Warning: PackageSize is 0 or less, setting default value 750KB")
        End If
        If bufferSize < packageSize Then
            Log.WriteWarning("Warning: BufferSize needs to be greater than PackageSize, setting 5x")
            bufferSize = 5 * packageSize
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
            Return Math.Round(Me.CurrentFileProgress / Me.CurrentFileSize * 100, decimals)
        End Get
    End Property

    ''' <summary>Gets the current download speed in bytes</summary>
    Public ReadOnly Property DownloadSpeed() As Int32
        Get
            Me.Mutex.WaitOne()
            Try
                Dim Speed As Int32 = 0
                For Each value As Int32 In m_currentSpeed.Values
                    Speed += value
                Next
                Return Speed
            Finally
                Me.Mutex.ReleaseMutex()
            End Try

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
