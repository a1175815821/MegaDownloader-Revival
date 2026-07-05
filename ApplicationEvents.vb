Imports System.Reflection

Namespace My

    ' The following events are available for MyApplication:
    ' 
    ' Startup: Raised when the application starts, before the startup form is created.
    ' Shutdown: Raised after all application forms are closed.  This event is not raised if the application terminates abnormally.
    ' UnhandledException: Raised if the application encounters an unhandled exception.
    ' StartupNextInstance: Raised when launching a single-instance application and the application is already active. 
    ' NetworkAvailabilityChanged: Raised when the network connection is connected or disconnected.
    Partial Friend Class MyApplication


        Private Shared _mainFrm As Main
        Friend Shared Property Main_Form As Main
            Set(value As Main)
                _mainFrm = value
            End Set
            Get
                Return _mainFrm
            End Get
        End Property

        Private Sub MyApplication_Startup(sender As Object, e As Microsoft.VisualBasic.ApplicationServices.StartupEventArgs) Handles Me.Startup

            If Not ApplicationInstanceManager.CreateSingleInstance("MegaDownloader", AddressOf SingleInstanceCallback) Then
                Return
            End If


            My.Application.MinimumSplashScreenDisplayTime = 600
            AddHandler AppDomain.CurrentDomain.AssemblyResolve, AddressOf LoadDLLFromStream
        End Sub

        Private Shared Sub SingleInstanceCallback(sender As Object, args As InstanceCallbackEventArgs)
            ' Not used anymore
            'If args Is Nothing OrElse _mainFrm Is Nothing Then
            '    Return
            'End If

            'Dim d As Action(Of Boolean) = Sub(x As Boolean)
            '                                  _mainFrm.ProcessArgs(args.CommandLineArgs)
            '                                  _mainFrm.Activate()
            '                              End Sub
            '_mainFrm.Invoke(d, True)
        End Sub

        Private Function LoadDLLFromStream( _
            ByVal sender As Object, _
            ByVal args As System.ResolveEventArgs) As System.Reflection.Assembly

            Dim resourceName As String = New AssemblyName(args.Name).Name & ".dll"


            resourceName = ResourceHelper.GetResourceName(resourceName)

            If Not String.IsNullOrEmpty(resourceName) Then
                Using stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)

                    Dim assemblyData(CInt(stream.Length - 1)) As Byte
                    stream.Read(assemblyData, 0, assemblyData.Length)
                    Return System.Reflection.Assembly.Load(assemblyData)

                End Using
            Else
                Throw New ApplicationException("DLL could not be loaded: " & args.Name)
            End If


        End Function


    End Class


End Namespace

