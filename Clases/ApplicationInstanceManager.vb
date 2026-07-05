Imports System.Runtime.Remoting
Imports System.Runtime.Remoting.Channels
Imports System.Threading
Imports System.Security.Permissions
Imports System.Runtime.Remoting.Channels.Ipc
Imports System.IO



' http://madebits.com/netz/help2.php#rem

''' <summary>
''' shared object for processes
''' </summary>
<Serializable()> _
<PermissionSet(SecurityAction.Demand, Name:="FullTrust")> _
Public Class InstanceProxy
    Inherits MarshalByRefObject
    ''' <summary>
    ''' Gets a value indicating whether this instance is first instance.
    ''' </summary>
    ''' <value>
    ''' 	<c>true</c> if this instance is first instance; otherwise, <c>false</c>.
    ''' </value>
    Public Shared Property IsFirstInstance() As Boolean
        Get
            Return m_IsFirstInstance
        End Get
        Set(value As Boolean)
            m_IsFirstInstance = value
        End Set
    End Property
    Private Shared m_IsFirstInstance As Boolean

    ''' <summary>
    ''' Gets the command line args.
    ''' </summary>
    ''' <value>The command line args.</value>
    Public Shared Property CommandLineArgs() As String()
        Get
            Return m_CommandLineArgs
        End Get
        Set(value As String())
            m_CommandLineArgs = value
        End Set
    End Property
    Private Shared m_CommandLineArgs As String()

    ''' <summary>
    ''' Sets the command line args.
    ''' </summary>
    ''' <param name="isFirstInstance__1">if set to <c>true</c> [is first instance].</param>
    ''' <param name="commandLineArgs__2">The command line args.</param>
    Public Sub SetCommandLineArgs(isFirstInstance__1 As Boolean, commandLineArgs__2 As String())
        IsFirstInstance = isFirstInstance__1
        CommandLineArgs = commandLineArgs__2
    End Sub
End Class

''' <summary>
''' 
''' </summary>
Public Class InstanceCallbackEventArgs
    Inherits EventArgs
    ''' <summary>
    ''' Initializes a new instance of the <see cref="InstanceCallbackEventArgs"/> class.
    ''' </summary>
    ''' <param name="isFirstInstance__1">if set to <c>true</c> [is first instance].</param>
    ''' <param name="commandLineArgs__2">The command line args.</param>
    Public Sub New(isFirstInstance__1 As Boolean, commandLineArgs__2 As String())
        IsFirstInstance = isFirstInstance__1
        CommandLineArgs = commandLineArgs__2
    End Sub

    ''' <summary>
    ''' Gets a value indicating whether this instance is first instance.
    ''' </summary>
    ''' <value>
    ''' 	<c>true</c> if this instance is first instance; otherwise, <c>false</c>.
    ''' </value>
    Public Property IsFirstInstance() As Boolean
        Get
            Return m_IsFirstInstance
        End Get
        Private Set(value As Boolean)
            m_IsFirstInstance = value
        End Set
    End Property
    Private m_IsFirstInstance As Boolean

    ''' <summary>
    ''' Gets or sets the command line args.
    ''' </summary>
    ''' <value>The command line args.</value>
    Public Property CommandLineArgs() As String()
        Get
            Return m_CommandLineArgs
        End Get
        Private Set(value As String())
            m_CommandLineArgs = value
        End Set
    End Property
    Private m_CommandLineArgs As String()
End Class

''' <summary>
''' Application Instance Manager
''' </summary>
Public NotInheritable Class ApplicationInstanceManager
    Private Sub New()
    End Sub
    ''' <summary>
    ''' Creates the single instance.
    ''' </summary>
    ''' <param name="name">The name.</param>
    ''' <param name="callback">The callback.</param>
    ''' <returns></returns>
    Public Shared Function CreateSingleInstance(name As String, callback As EventHandler(Of InstanceCallbackEventArgs)) As Boolean
        Dim eventWaitHandle__1 As EventWaitHandle = Nothing
        Dim eventName As String = String.Format("{0}-{1}", Environment.MachineName, name)

        InstanceProxy.IsFirstInstance = False
        InstanceProxy.CommandLineArgs = Environment.GetCommandLineArgs()

        Try
            ' try opening existing wait handle
            eventWaitHandle__1 = EventWaitHandle.OpenExisting(eventName)
        Catch
            ' got exception = handle wasn't created yet
            InstanceProxy.IsFirstInstance = True
        End Try

        If InstanceProxy.IsFirstInstance Then
            ' init handle
            eventWaitHandle__1 = New EventWaitHandle(False, EventResetMode.AutoReset, eventName)

            ' register wait handle for this instance (process)
            ThreadPool.RegisterWaitForSingleObject(eventWaitHandle__1, AddressOf WaitOrTimerCallback, callback, Timeout.Infinite, False)
            eventWaitHandle__1.Close()

            ' register shared type (used to pass data between processes)
            RegisterRemoteType(name)
        Else
            ' pass console arguments to shared object
            UpdateRemoteObject(name)

            ' invoke (signal) wait handle on other process
            If eventWaitHandle__1 IsNot Nothing Then
                eventWaitHandle__1.[Set]()
            End If


            ' kill current process
            Environment.[Exit](0)
        End If

        Return InstanceProxy.IsFirstInstance
    End Function

    ''' <summary>
    ''' Updates the remote object.
    ''' </summary>
    ''' <param name="uri">The remote URI.</param>
    Private Shared Sub UpdateRemoteObject(uri As String)

        ' Cannot remote with mpress, arrrgh!! :(
        Dim PathFile As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MegaDownloader/Internal")

        Mutex.MEGAUriParameters.WaitOne()
        Try
            If Not System.IO.Directory.Exists(PathFile) Then
                System.IO.Directory.CreateDirectory(PathFile)
            End If
            PathFile = Path.Combine(PathFile, "Buffer.dat")
            Using t As New StreamWriter(PathFile, True)
                t.Write(String.Join("|", InstanceProxy.CommandLineArgs))
            End Using
        Finally
            Mutex.MEGAUriParameters.ReleaseMutex()
        End Try

        '' register net-pipe channel
        'Dim clientChannel = New IpcClientChannel()
        'ChannelServices.RegisterChannel(clientChannel, True)

        '' get shared object from other process
        'Dim proxy As InstanceProxy = TryCast(Activator.GetObject(GetType(InstanceProxy), String.Format("ipc://{0}_{1}/{1}", Environment.MachineName, uri)), InstanceProxy)

        '' pass current command line args to proxy
        'If proxy IsNot Nothing Then

        '   proxy.SetCommandLineArgs(InstanceProxy.IsFirstInstance, InstanceProxy.CommandLineArgs)

        'End If

        '' close current client channel
        'ChannelServices.UnregisterChannel(clientChannel)
    End Sub



    ' Dirty trick... but works without using remote marshalling :(
    Private Shared _getParametersLastCheck As Date = Date.MinValue
    Friend Shared Function GetParameters() As Boolean
        Dim PathLog As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), _
                                             "MegaDownloader/Internal/Buffer.dat")
        If File.Exists(PathLog) Then
            If _getParametersLastCheck = Date.MinValue OrElse File.GetLastWriteTimeUtc(PathLog) > _getParametersLastCheck.ToUniversalTime Then
                Mutex.MEGAUriParameters.WaitOne()
                Try
                    Dim args As New Generic.List(Of String)
                    Using t As New StreamReader(PathLog)
                        While Not t.EndOfStream
                            Dim l = t.ReadLine.Split("|"c)
                            If args.Count > 0 Then l = l.Skip(1).ToArray ' Each line has the program name as first parameter, we delete it
                            args.AddRange(l)
                        End While
                    End Using
                    Using t As New StreamWriter(PathLog, False)
                        t.Write("")
                    End Using
                    _getParametersLastCheck = Now

                    If args.Count > 0 Then
                        Dim d As Action(Of Boolean) = Sub(x As Boolean)
                                                          My.MyApplication.Main_Form.ProcessArgs(args.ToArray)
                                                          My.MyApplication.Main_Form.Activate()
                                                      End Sub
                        My.MyApplication.Main_Form.Invoke(d, True)

                        Return True
                    End If

                Finally
                    Mutex.MEGAUriParameters.ReleaseMutex()
                End Try
            End If
        End If
        Return False

    End Function

    ''' <summary>
    ''' Registers the remote type.
    ''' </summary>
    ''' <param name="uri">The URI.</param>
    Private Shared Sub RegisterRemoteType(uri As String)
        ' register remote channel (net-pipes)
        'Dim serverChannel = New IpcServerChannel(Environment.MachineName & "_" & uri)

        'ChannelServices.RegisterChannel(serverChannel, True)

        ' register shared type
        'RemotingConfiguration.RegisterWellKnownServiceType(GetType(InstanceProxy), uri, WellKnownObjectMode.Singleton)

        ' close channel, on process exit
        'Dim process As Process = process.GetCurrentProcess()

        'AddHandler process.Exited, Sub()
        '                               ChannelServices.UnregisterChannel(serverChannel)
        '                           End Sub
    End Sub


    ''' <summary>
    ''' Wait Or Timer Callback Handler
    ''' </summary>
    ''' <param name="state">The state.</param>
    ''' <param name="timedOut">if set to <c>true</c> [timed out].</param>
    Private Shared Sub WaitOrTimerCallback(state As Object, timedOut As Boolean)
        ' cast to event handler
        Dim callback = TryCast(state, EventHandler(Of InstanceCallbackEventArgs))
        If callback Is Nothing Then
            Return
        End If

        ' invoke event handler on other process
        callback(state, New InstanceCallbackEventArgs(InstanceProxy.IsFirstInstance, InstanceProxy.CommandLineArgs))
    End Sub
End Class
