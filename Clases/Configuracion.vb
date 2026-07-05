Imports System.Xml
Imports System.IO
Imports System.Security

Public Class Configuracion
	
	Public Enum ErrorConfigClass
		SinErrores = 0 ' No hay errores
		Fichero_No_Existe = 1 ' El fichero no existe
		Fichero_No_Valido = 2 ' El fichero existe pero no es un XML vallido
		Usuario_Password_Incorrecto = 3 ' El fichero existe pero el usuario y password está mal grabado
		Fichero_No_Creado = 4 ' Ha habido algún error creando el directorio o el fichero (permisos?)
	End Enum
	
	
	#Region "Métodos públicos"
	
	Public Sub New()
		' Cargamos del XML
		ErrorConfig = ErrorConfigClass.SinErrores
		Me.ConfigUI = New ConfiguracionUI
		CargaXML()
		Conexion.SetProxy(Me)
		ThrottledStreamController.GetController.SetMaxGlobalSpeed(Me.LimiteVelocidadKBs)
	End Sub
	
	Public ConfigUI As ConfiguracionUI
	
	Public Idioma As String
	
	Public CondicionesAceptadas As Boolean
	
	Public VersionConfig As String
	
	Public ErrorConfig As ErrorConfigClass
	
	Public RutaDefecto As String
	
	Public ExtraerAutomaticamente As Boolean
	
	Public CrearDirectorioPaquete As Boolean
	
	Public AnalizarPortapapeles As Boolean
	
	'Public PermitirSkins As Boolean
	
	Public TamanoPaqueteKB As Integer
	
	Public TamanoBufferKB As Integer
	
	Public MaxConexionesGuardadas As Integer
	
	Public ConexionesPorFichero As Integer
	
	Public DescargasSimultaneas As Integer
	
	Public ResetearErrores As Boolean
	
	Public UsarProxy As Boolean
	
    Public ApagarPC As Boolean

    Public CheckUpdates As Boolean
	
	Public ComenzarDescargando As Boolean
	
	Public MantenerUltimaConfiguracion As Boolean
	
	Public HideCollaborateButton As Boolean
	
	Public ProxyIP As String
	
	Public ProxyUser As String
	
	Public ProxyPassword As String
	
	Public ProxyPort As Integer
	
	Public ResetearErroresPeriodoMinutos As Integer
	
	Public LimiteVelocidadKBs As Integer
	
	Public IniciarConWindows As Boolean
	
	Public NivelLog As Log.LevelLogType
	
	Public PrioridadDescompresion As SharpCompress.PriorityExtension.Priority.PriorityType
	
	Public ServidorStreamingActivo As Boolean
	
	Public ServidorStreamingPuerto As Integer
    Private Const DEFAULT_STREAMING_PORT As Integer = 54321

    Public ServidorStreamingPassword As String
	
	Public ServidorWebActivo As Boolean
	
	Public ServidorWebNombre As String
	
	Public ServidorWebRutaPlantilla As String
	
	Public ServidorWebPuerto As Integer
	
	Public ServidorWebPassword As String
	
	Public ServidorWebTimeout As Integer

	Public VLCPath As String
	
	Public ListaPreSharedKeys As List(Of SecureString)
	
	Private _Usuario As SecureString
	
	Private _Password As SecureString
	
	Private _ELCAccountList As Generic.List(Of ELCAccountHelper.Account)
	
	Private Const KeyPassword As String = "A9G7dHUprtNmNEBLEDhFneBAcyRTZdd5RuAzYQKc3qJ4BaVH"
	Public Property Usuario As String
		Get
			Return Criptografia.ToInsecureString(_Usuario)
		End Get
		Set(value As String)
			_Usuario = Criptografia.ToSecureString(value)
		End Set
	End Property
	Public Property Password As String
		Get
			Return Criptografia.ToInsecureString(_Password)
		End Get
		Set(value As String)
			_Password = Criptografia.ToSecureString(value)
		End Set
	End Property
	
	Public Property ELCAccounts As Generic.List(Of ELCAccountHelper.Account)
		Get
			Return _ELCAccountList
		End Get
		Set(ByVal value As Generic.List(Of ELCAccountHelper.Account))
			_ELCAccountList = value
		End Set
	End Property
	
	
	Private Shared _LastSavedXML As String = Nothing
	Public Sub GuardarXML(ByVal ForzarGuardado As Boolean)
		Dim Xml As New XmlDocument
		
		Xml.AppendChild(Xml.CreateElement("Configuration"))
		
		Xml.DocumentElement.AppendChild(Xml.CreateElement("Language")).InnerText = Idioma
		Xml.DocumentElement.AppendChild(Xml.CreateElement("RutaDefecto")).InnerText = RutaDefecto
		Xml.DocumentElement.AppendChild(Xml.CreateElement("CondicionesAceptadas")).InnerText = CondicionesAceptadas.ToString
		Xml.DocumentElement.AppendChild(Xml.CreateElement("VersionConfig")).InnerText = VersionConfig
		Xml.DocumentElement.AppendChild(Xml.CreateElement("ExtraerAutomaticamente")).InnerText = ExtraerAutomaticamente.ToString
		Xml.DocumentElement.AppendChild(Xml.CreateElement("CrearDirectorioPaquete")).InnerText = CrearDirectorioPaquete.ToString
		Xml.DocumentElement.AppendChild(Xml.CreateElement("AnalizarPortapapeles")).InnerText = AnalizarPortapapeles.ToString
		'Xml.DocumentElement.AppendChild(Xml.CreateElement("PermitirSkins")).InnerText = PermitirSkins.ToString
        'Xml.DocumentElement.AppendChild(Xml.CreateElement("MaxConexionesGuardadas")).InnerText = MaxConexionesGuardadas.ToString
		
		Xml.DocumentElement.AppendChild(Xml.CreateElement("TamanoPaqueteKB")).InnerText = TamanoPaqueteKB.ToString
		Xml.DocumentElement.AppendChild(Xml.CreateElement("TamanoBufferKB")).InnerText = TamanoBufferKB.ToString
		
		Xml.DocumentElement.AppendChild(Xml.CreateElement("DescargasSimultaneas")).InnerText = DescargasSimultaneas.ToString
		Xml.DocumentElement.AppendChild(Xml.CreateElement("ConexionesPorFichero")).InnerText = ConexionesPorFichero.ToString
		
		Xml.DocumentElement.AppendChild(Xml.CreateElement("ResetearErrores")).InnerText = ResetearErrores.ToString
		
		' Xml.DocumentElement.AppendChild(Xml.CreateElement("ApagarPC")).InnerText = ApagarPC.ToString
		
        Xml.DocumentElement.AppendChild(Xml.CreateElement("ComenzarDescargando")).InnerText = ComenzarDescargando.ToString

        Xml.DocumentElement.AppendChild(Xml.CreateElement("CheckUpdates")).InnerText = CheckUpdates.ToString
		
		Xml.DocumentElement.AppendChild(Xml.CreateElement("MantenerUltimaConfiguracion")).InnerText = MantenerUltimaConfiguracion.ToString
		
		Xml.DocumentElement.AppendChild(Xml.CreateElement("UsarProxy")).InnerText = UsarProxy.ToString
		
		Xml.DocumentElement.AppendChild(Xml.CreateElement("ProxyPort")).InnerText = ProxyPort.ToString
		Xml.DocumentElement.AppendChild(Xml.CreateElement("ProxyIP")).InnerText = ProxyIP
		Xml.DocumentElement.AppendChild(Xml.CreateElement("ProxyUser")).InnerText = ProxyUser
		Xml.DocumentElement.AppendChild(Xml.CreateElement("ProxyPassword")).InnerText = ProxyPassword
		
		Xml.DocumentElement.AppendChild(Xml.CreateElement("VLCPath")).InnerText = VLCPath
		
		Xml.DocumentElement.AppendChild(Xml.CreateElement("ResetearErroresPeriodoMinutos")).InnerText = ResetearErroresPeriodoMinutos.ToString
		
		Xml.DocumentElement.AppendChild(Xml.CreateElement("NivelLog")).InnerText = [Enum].GetName(GetType(Log.LevelLogType), NivelLog)
		
		Xml.DocumentElement.AppendChild(Xml.CreateElement("PrioridadDescompresion")).InnerText = [Enum].GetName(GetType(SharpCompress.PriorityExtension.Priority.PriorityType), PrioridadDescompresion)
		
		Xml.DocumentElement.AppendChild(Xml.CreateElement("LimiteVelocidadKBs")).InnerText = LimiteVelocidadKBs.ToString
		
		Xml.DocumentElement.AppendChild(Xml.CreateElement("IniciarConWindows")).InnerText = IniciarConWindows.ToString
		
		Xml.DocumentElement.AppendChild(Xml.CreateElement("ServidorStreamingActivo")).InnerText = ServidorStreamingActivo.ToString
        Xml.DocumentElement.AppendChild(Xml.CreateElement("ServidorStreamingPuerto")).InnerText = ServidorStreamingPuerto.ToString
        Xml.DocumentElement.AppendChild(Xml.CreateElement("ServidorStreamingPassword")).InnerText = ServidorStreamingPassword
		
		Xml.DocumentElement.AppendChild(Xml.CreateElement("ServidorWebActivo")).InnerText = ServidorWebActivo.ToString
		Xml.DocumentElement.AppendChild(Xml.CreateElement("ServidorWebNombre")).InnerText = ServidorWebNombre
		Xml.DocumentElement.AppendChild(Xml.CreateElement("ServidorWebRutaPlantilla")).InnerText = ServidorWebRutaPlantilla
		Xml.DocumentElement.AppendChild(Xml.CreateElement("ServidorWebPassword")).InnerText = Criptografia.AES_EncryptString(ServidorWebPassword, KeyPassword)
		Xml.DocumentElement.AppendChild(Xml.CreateElement("ServidorWebTimeout")).InnerText = ServidorWebTimeout.ToString
		Xml.DocumentElement.AppendChild(Xml.CreateElement("ServidorWebPuerto")).InnerText = ServidorWebPuerto.ToString
		
		If HideCollaborateButton Then Xml.DocumentElement.AppendChild(Xml.CreateElement("HideCollaborateButton")).InnerText = HideCollaborateButton.ToString
		
		
		ConfigUI.GuardarXML(Xml)
		
		Dim Fichero As String
		
		Fichero = ObtenerRutaFicheroConfiguracion()
		
		If _LastSavedXML Is Nothing OrElse _LastSavedXML <> Xml.DocumentElement.OuterXml.GetHashCode.ToString Or ForzarGuardado Then
			
			_LastSavedXML = Xml.DocumentElement.OuterXml.GetHashCode.ToString
			
			' Como el usuario y password se guarda cifrado con entropia, cada vez tendrá un valor distinto, no podemos compararlos...
			Xml.DocumentElement.AppendChild(Xml.CreateElement("Usuario")).InnerText = Criptografia.EncryptString_DPAPI(_Usuario)
			Xml.DocumentElement.AppendChild(Xml.CreateElement("Password")).InnerText = Criptografia.EncryptString_DPAPI(_Password)
			Xml.DocumentElement.AppendChild(SavePreSharedKeys(Xml))
			
			
			Dim AccountsNode As XmlNode = Xml.DocumentElement.AppendChild(Xml.CreateElement("ELCAccounts"))
			For Each Account As ELCAccountHelper.Account In Me.ELCAccounts
				Dim AccountNode As XmlNode = AccountsNode.AppendChild(Xml.CreateElement("Account"))
				AccountNode.AppendChild(Xml.CreateElement("Name")).InnerText = Account.Alias
				AccountNode.AppendChild(Xml.CreateElement("URL")).InnerText = Account.URL
				AccountNode.AppendChild(Xml.CreateElement("User")).InnerText = Criptografia.EncryptString_DPAPI(Account.User)
				AccountNode.AppendChild(Xml.CreateElement("Key")).InnerText = Criptografia.EncryptString_DPAPI(Account.Key)
				AccountNode.AppendChild(Xml.CreateElement("Default")).InnerText = If(Account.DefaultAccount, "1", "0")
			Next
			
			Log.WriteDebug("Saving configuration")
			
            Mutex.GuardarConfig.WaitOne()

            ' Save backup
            Try
                If System.IO.File.Exists(Fichero & ".bak") Then
                    System.IO.File.Delete(Fichero & ".bak")
                End If
                System.IO.File.Move(Fichero, Fichero & ".bak")
            Catch ex As Exception
                ' Error?
            End Try


			Try
				Xml.Save(Fichero)
			Catch ex As Exception
				Log.WriteError("Error saving configuration XML: " & ex.ToString)
				ErrorConfig = ErrorConfigClass.Fichero_No_Creado
			Finally
				Mutex.GuardarConfig.ReleaseMutex()
			End Try
			
			
			
		End If
	End Sub
	
	Public Function SavePreSharedKeys(ByRef XmlDoc As XmlDocument) As XmlElement
		Dim list As XmlElement = XmlDoc.CreateElement("PreSharedKeys")
		For Each key As SecureString In Me.ListaPreSharedKeys
			list.AppendChild(XmlDoc.CreateElement("Key")).InnerText = Criptografia.EncryptString_DPAPI(key)
		Next
		Return list
	End Function
	
	Public Sub CargaXML()
		
		Dim Fichero As String
		
		Fichero = ObtenerRutaFicheroConfiguracion()
		
		If Not System.IO.File.Exists(Fichero) Then
			Log.WriteWarning("Configuration file does not exist")
			ErrorConfig = ErrorConfigClass.Fichero_No_Existe
			ConfiguracionDefectoVacia()
			Exit Sub
		End If
		
		Log.WriteDebug("Loading configuration XML")
		
		' Cargamos el XML
		Dim Xml As New XmlDocument
		Mutex.GuardarConfig.WaitOne()
		Try
			Xml.Load(Fichero)
		Catch ex As Exception
			ErrorConfig = ErrorConfigClass.Fichero_No_Valido
			ConfiguracionDefectoVacia()
			Log.WriteError("Configuration file could not be loaded: " & ex.ToString)
			Exit Sub
		Finally
			Mutex.GuardarConfig.ReleaseMutex()
		End Try
		
		Idioma = LeerNodo(Xml, "Language", "")
		Try
			Idioma = System.Globalization.CultureInfo.GetCultureInfo(Idioma).Name
			If String.IsNullOrEmpty(Idioma) Then Throw New ApplicationException("Empty culture")
		Catch ex As Exception
			Me.Idioma = System.Threading.Thread.CurrentThread.CurrentUICulture.Name
		End Try
		
		
		VersionConfig = LeerNodo(Xml, "VersionConfig", "0")
		RutaDefecto = LeerNodo(Xml, "RutaDefecto", "")
		ProxyIP = LeerNodo(Xml, "ProxyIP", "")
		ProxyUser = LeerNodo(Xml, "ProxyUser", "")
		ProxyPassword = LeerNodo(Xml, "ProxyPassword", "")
		VLCPath = LeerNodo(Xml, "VLCPath", "")
		_Usuario = Criptografia.DecryptString_DPAPI(LeerNodo(Xml, "Usuario", ""))
		_Password = Criptografia.DecryptString_DPAPI(LeerNodo(Xml, "Password", ""))
		ExtraerAutomaticamente = False
		CrearDirectorioPaquete = False
		AnalizarPortapapeles = False
		'PermitirSkins = True
		ResetearErrores = False
		UsarProxy = False
		ApagarPC = False
        ComenzarDescargando = True
		IniciarConWindows = False
		MantenerUltimaConfiguracion = True
        HideCollaborateButton = False
        CheckUpdates = True
        Boolean.TryParse(LeerNodo(Xml, "CheckUpdates", "true"), CheckUpdates)
		Boolean.TryParse(LeerNodo(Xml, "HideCollaborateButton", "false"), HideCollaborateButton)
		Boolean.TryParse(LeerNodo(Xml, "ExtraerAutomaticamente", "false"), ExtraerAutomaticamente)
		Boolean.TryParse(LeerNodo(Xml, "CondicionesAceptadas", "false"), CondicionesAceptadas)
		Boolean.TryParse(LeerNodo(Xml, "CrearDirectorioPaquete", "false"), CrearDirectorioPaquete)
		Boolean.TryParse(LeerNodo(Xml, "AnalizarPortapapeles", "false"), AnalizarPortapapeles)
		'Boolean.TryParse(LeerNodo(Xml, "PermitirSkins", "true"), PermitirSkins)
		Boolean.TryParse(LeerNodo(Xml, "ResetearErrores", "false"), ResetearErrores)
		Boolean.TryParse(LeerNodo(Xml, "UsarProxy", "false"), UsarProxy)
		Boolean.TryParse(LeerNodo(Xml, "IniciarConWindows", "false"), IniciarConWindows)
		Boolean.TryParse(LeerNodo(Xml, "MantenerUltimaConfiguracion", "true"), MantenerUltimaConfiguracion)
        Boolean.TryParse(LeerNodo(Xml, "ComenzarDescargando", "true"), ComenzarDescargando)
		'Boolean.TryParse(LeerNodo(Xml, "ApagarPC", "false"), ApagarPC)
		TamanoPaqueteKB = 50
		TamanoBufferKB = 750
        MaxConexionesGuardadas = 100
		ResetearErroresPeriodoMinutos = 15
		ProxyPort = 0
		LimiteVelocidadKBs = 0
		Integer.TryParse(LeerNodo(Xml, "TamanoPaqueteKB", "50"), TamanoPaqueteKB)
		Integer.TryParse(LeerNodo(Xml, "TamanoBufferKB", "750"), TamanoBufferKB)
		Integer.TryParse(LeerNodo(Xml, "ProxyPort", "0"), ProxyPort)
		Integer.TryParse(LeerNodo(Xml, "LimiteVelocidadKBs", "0"), LimiteVelocidadKBs)
        'Integer.TryParse(LeerNodo(Xml, "MaxConexionesGuardadas", "100"), MaxConexionesGuardadas)
		Integer.TryParse(LeerNodo(Xml, "ResetearErroresPeriodoMinutos", "15"), ResetearErroresPeriodoMinutos)
        If ResetearErroresPeriodoMinutos < 1 Or ResetearErroresPeriodoMinutos > 999 Then
            ResetearErroresPeriodoMinutos = 15
        End If
		If LimiteVelocidadKBs < 0 Then
			LimiteVelocidadKBs = 0
		End If
		If TamanoPaqueteKB < 1 Then
			TamanoPaqueteKB = 50
		End If
		If TamanoBufferKB < 1 Then
			TamanoBufferKB = 750
		End If
		
		NivelLog = Log.LevelLogType.Normal
		If [Enum].IsDefined(GetType(Log.LevelLogType), LeerNodo(Xml, "NivelLog", "")) Then
			NivelLog = CType([Enum].Parse(GetType(Log.LevelLogType), LeerNodo(Xml, "NivelLog", "")), Log.LevelLogType)
		End If
		
		PrioridadDescompresion = SharpCompress.PriorityExtension.Priority.PriorityType.Normal
		If [Enum].IsDefined(GetType(SharpCompress.PriorityExtension.Priority.PriorityType), LeerNodo(Xml, "PrioridadDescompresion", "")) Then
			PrioridadDescompresion = CType([Enum].Parse(GetType(SharpCompress.PriorityExtension.Priority.PriorityType), LeerNodo(Xml, "PrioridadDescompresion", "")), SharpCompress.PriorityExtension.Priority.PriorityType)
		End If
		
		DescargasSimultaneas = 3
		ConexionesPorFichero = 3
		Integer.TryParse(LeerNodo(Xml, "DescargasSimultaneas", "3"), DescargasSimultaneas)
		Integer.TryParse(LeerNodo(Xml, "ConexionesPorFichero", "3"), ConexionesPorFichero)
		If DescargasSimultaneas < 1 Then
			DescargasSimultaneas = 3
		End If
		If ConexionesPorFichero < 1 Then
			ConexionesPorFichero = 3
		End If
		
		' Servidor streaming
		ServidorStreamingActivo = False
		Boolean.TryParse(LeerNodo(Xml, "ServidorStreamingActivo", "false"), ServidorStreamingActivo)
		ServidorStreamingPuerto = DEFAULT_STREAMING_PORT
        Integer.TryParse(LeerNodo(Xml, "ServidorStreamingPuerto", DEFAULT_STREAMING_PORT.ToString), ServidorStreamingPuerto)
        ServidorStreamingPassword = LeerNodo(Xml, "ServidorStreamingPassword", "")

		' Servidor web
		ServidorWebActivo = False
		Boolean.TryParse(LeerNodo(Xml, "ServidorWebActivo", "false"), ServidorWebActivo)
		ServidorWebNombre = LeerNodo(Xml, "ServidorWebNombre", "")
		ServidorWebRutaPlantilla = LeerNodo(Xml, "ServidorWebRutaPlantilla", "")
		ServidorWebPassword = LeerNodo(Xml, "ServidorWebPassword", "")
		Try
			If Not String.IsNullOrEmpty(ServidorWebPassword) Then
				ServidorWebPassword = Criptografia.AES_DecryptString(ServidorWebPassword, KeyPassword)
			End If
		Catch ex As Exception
		End Try
		ServidorWebTimeout = 5
		Integer.TryParse(LeerNodo(Xml, "ServidorWebTimeout", "5"), ServidorWebTimeout)
		ServidorWebPuerto = 0
		Integer.TryParse(LeerNodo(Xml, "ServidorWebPuerto", "0"), ServidorWebPuerto)
		
		' Pre shared keys
		Me.ListaPreSharedKeys = New List(Of SecureString)
		For Each keynode As XmlNode In Xml.DocumentElement.SelectNodes("PreSharedKeys/Key")
			Me.ListaPreSharedKeys.Add(Criptografia.DecryptString_DPAPI(keynode.InnerText))
		Next
		
		If _Usuario.Length = 0 Or _Password.Length = 0 Then
			'ErrorConfig = ErrorConfigClass.Usuario_Password_Incorrecto
		End If
		If ConexionesPorFichero = 0 Then
			ErrorConfig = ErrorConfigClass.Fichero_No_Valido
		End If
		
		
		Me._ELCAccountList = New Generic.List(Of ELCAccountHelper.Account)
		For Each AccountNode As XmlNode In Xml.DocumentElement.SelectNodes("ELCAccounts/Account")
			Dim newAccount As New ELCAccountHelper.Account
			newAccount.User = Criptografia.DecryptString_DPAPI(LeerNodo(AccountNode, "User", ""))
			newAccount.Key = Criptografia.DecryptString_DPAPI(LeerNodo(AccountNode, "Key", ""))
			newAccount.URL = LeerNodo(AccountNode, "URL", "")
			newAccount.Alias = LeerNodo(AccountNode, "Name", "")
			If String.IsNullOrEmpty(newAccount.Alias) Then
				newAccount.Alias = newAccount.URL
			End If
			newAccount.DefaultAccount = LeerNodo(AccountNode, "Default", "") = "1"
			Me._ELCAccountList.Add(newAccount)
		Next
		
		' UI
		Me.ConfigUI.CargarXML(Xml)
		
	End Sub
	
	Public Sub ConfiguracionDefectoVacia()
		Me.RutaDefecto = ""
		Me.Idioma = System.Threading.Thread.CurrentThread.CurrentUICulture.Name
		Me.ExtraerAutomaticamente = False
		Me.CondicionesAceptadas = False
		Me.CrearDirectorioPaquete = True
		Me.AnalizarPortapapeles = True
		Me.HideCollaborateButton = False
		Me.ApagarPC = False
		Me.UsarProxy = False
        Me.ComenzarDescargando = True
        Me.CheckUpdates = True
		Me.ProxyPort = 0
		Me.VersionConfig = "0"
		Me.DescargasSimultaneas = 3
		Me.ConexionesPorFichero = 3
		Me.ProxyIP = ""
		Me.ServidorWebTimeout = 5
        Me.MaxConexionesGuardadas = 100
		Me.NivelLog = Log.LevelLogType.Normal
		Me.ServidorStreamingActivo = False
		Me.ServidorWebActivo = False
		Me.ServidorStreamingPuerto = DEFAULT_STREAMING_PORT
		Me.PrioridadDescompresion = SharpCompress.PriorityExtension.Priority.PriorityType.Normal
		Me._Usuario = Criptografia.ToSecureString("")
		Me._Password = Criptografia.ToSecureString("")
		Me.ListaPreSharedKeys = New List(Of SecureString)
		Me.TamanoPaqueteKB = 50
		Me.TamanoBufferKB = 750
		Me._ELCAccountList = New Generic.List(Of ELCAccountHelper.Account)
		Me.ConfigUI.ConfiguracionDefectoVacia()
	End Sub
	
	Public Shared Sub RegisterInStartup(isChecked As Boolean)
		Try 

			Dim registryKey As Microsoft.Win32.RegistryKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\Microsoft\Windows\CurrentVersion\Run", True)
			If registryKey Is Nothing and isChecked Then
	    		registryKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey("SOFTWARE\Microsoft\Windows\CurrentVersion\Run")
	    	End If
			If isChecked Then
				registryKey.SetValue("MegaDownloader", """" & Application.ExecutablePath & """ -silent")
			Else
				registryKey.DeleteValue("MegaDownloader", False)
			End If
			
		Catch ex As UnauthorizedAccessException
            Log.WriteError("SECURITY ERROR: Not enough privileges to access the registry (CU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run). Execute the application with administrator privileges (at least one time) in order to access the registry. Please note that if you move the application, you will have to execute it again with administrator privileges.")
          
        Catch ex As Security.SecurityException
            Log.WriteError("SECURITY ERROR: Not enough privileges to access the registry (CU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run). Execute the application with administrator privileges (at least one time) in order to access the registry. Please note that if you move the application, you will have to execute it again with administrator privileges.")
          
        Catch ex As Exception
            Log.WriteError("Error accessing the registry (CU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run). Error: " & ex.ToString)
     
        End Try
	End Sub
	
	#End Region
	
	
	#Region "Métodos privados"
	
	
	Private Function LeerNodo(ByRef DocumentoXML As XmlDocument, ByRef Path As String, ByVal ValorDefecto As String) As String
		Dim nodo As XmlNode = DocumentoXML.DocumentElement.SelectSingleNode(Path)
		If nodo Is Nothing Then
			Return ValorDefecto
		Else
			Return nodo.InnerText
		End If
	End Function
	Private Function LeerNodo(ByRef NodoXML As XmlNode, ByRef Path As String, ByVal ValorDefecto As String) As String
        Dim nodo As XmlNode = NodoXML.SelectSingleNode(Path)
        If nodo Is Nothing Then
            Return ValorDefecto
        Else
            Return nodo.InnerText
        End If
    End Function
	
	
	
	Private Shared Function ObtenerRutaFicheroConfiguracion() As String
		
		Dim PathLog As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MegaDownloader/Config")
		
		If Not System.IO.Directory.Exists(PathLog) Then
			System.IO.Directory.CreateDirectory(PathLog)
		End If
		PathLog = Path.Combine(PathLog, "Configuration.xml")
		Return PathLog
	End Function
	
	#End Region
	
End Class
