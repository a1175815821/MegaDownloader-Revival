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
		MegaQuotaManager.SetBreakthroughMode(Me.QuotaBreakthroughMode)
		' ③:MaximumBytesPerSecond 按字节限速,此处须 KB→B(×1024),否则 1MB/s 实得 1KB/s。
		ThrottledStreamController.GetController.SetMaxGlobalSpeed(CLng(Me.LimiteVelocidadKBs) * 1024L)
	End Sub
	
	Public ConfigUI As ConfiguracionUI
	
	Public Idioma As String
	
	Public CondicionesAceptadas As Boolean
	
	Public VersionConfig As String
	
	Public ErrorConfig As ErrorConfigClass
	
	''' <summary>ErrorConfig mixes two different things: a failure to LOAD the file
	''' (Fichero_No_Existe / Fichero_No_Valido) and a failure to SAVE it (Fichero_No_Creado).
	''' P0-2 clears ErrorConfig after a successful save, which also erased the LOAD error —
	''' and the V26 column migration (Main.vb:420-424) saves successfully BEFORE the check at
	''' Main.vb:450, so a fresh install (or a corrupt XML) silently lost the mandatory
	''' first-run configuration dialog. This field reflects the load result only and is never
	''' cleared; Main.vb:450 reads this one, Configuration.vb keeps reading ErrorConfig.</summary>
	Public LoadErrorConfig As ErrorConfigClass

	''' <summary>P0-3:加载失败是 IOException(文件被瞬时占用)而非损坏时为 True。
	''' 此时磁盘上的配置可能完好,内存里只能跑默认值——所有写盘必须跳过并保留
	''' 旧文件,否则 GuardarXML 会把默认值落盘并把完好文件挤进 .bak 直至彻底覆盖。</summary>
	Public LoadTransientError As Boolean
	
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

	''' <summary>配额突破模式(旧版行为):命中 509/-17 不熔断,走普通瞬时错误链持续重试。默认关。</summary>
	Public QuotaBreakthroughMode As Boolean

	''' <summary>v2.5 beta 一次性迁移:老配置的 ResetearErrores=False 翻为 True(15min)。True=已迁移,不再重复覆盖用户后续手动选择。</summary>
	Public ResetearErroresMigratedV25 As Boolean
	
	''' <summary>P0-1 UI 一次性列默认集:开 Progreso%、藏 Descargado、Estado 加宽。True=已迁移,不再覆盖用户后续手动调整。
	''' 注:V26 是 UI 修订号,不是 App 版本(当前发布线 v2.5),为兼容已迁移用户保留原名,RC 起双写 V25 别名。</summary>
	Public ColumnUIDefaultsMigratedV26 As Boolean
	
	Public UsarProxy As Boolean
	
    Public ApagarPC As Boolean

    Public CheckUpdates As Boolean

    ''' <summary>
    ''' Versión que el usuario descartó con "no volver a avisar".
    ''' Vacío (por defecto) = ninguna versión descartada.
    ''' </summary>
    Public UpdateSkipVersion As String
	
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

	''' <summary>P1-1:用户选择"不再提示"后,mega:// 注册失败不再弹框(仍写日志)。</summary>
	Public NoMostrarAvisoURI As Boolean
	
	Public NivelLog As Log.LevelLogType
	
	Public PrioridadDescompresion As DescompresionPriority
	
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

	''' <summary>
	''' Allow LAN devices to reach the web remote-control interface.
	''' False (default) binds 127.0.0.1 only — local machine access.
	''' True binds all interfaces so phones/other PCs can push downloads;
	''' requires ServidorWebPassword to be set (enforced at server start).
	''' </summary>
	Public ServidorWebPermitirLAN As Boolean

	''' <summary>
	''' Specific local IP address to bind when ServidorWebPermitirLAN is enabled.
	''' Empty (default) binds all interfaces (0.0.0.0). Useful on multi-NIC
	''' machines to expose the listener on the LAN adapter only, keeping it
	''' off VPN/virtual adapters.
	''' </summary>
	Public ServidorWebBindIP As String

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
	' B2-⑦:ServidorWebPassword 明文快照,用于去重比对。密文每次随机 IV 必变,
	' 不得参与 OuterXml 比对(否则有 Web 密码时配置每 5s 必重写)。
	Private Shared _LastSavedWebPasswordPlain As String = Nothing
	' P0-4:代理/流媒体口令 DPAPI 密文同样非确定性(Protect 每次随机)，同样不得参与
	' OuterXml 比对；此处快照其明文用于去重（Web 口令模式的复用）。
	Private Shared _LastSavedProxyPasswordPlain As String = Nothing
	Private Shared _LastSavedStreamingPasswordPlain As String = Nothing
	' R-P0-1:ELCAccounts 的内容指纹(见 ELCAccountsHash)。账号节点是在去重快照 _LastSavedXML
	' 之后才追加到 XML 的,所以账号增删改对周期性 GuardarXML(False) 完全不可见 —— P0-1 只能在
	' 两处导入调用点补 GuardarXML(True) 兜底,陷阱本身还在。此字段让周期保存也能感知账号变更。
	Private Shared _LastSavedELCAccountsHash As String = Nothing
	' P0-3 补充:GuardarXML 每 5s 被调用一次,瞬时锁状态下若每次都写 Warning,一小时 700+ 条。
	' 只在首次真正拦截时记一条,状态本身仍由 LoadTransientError 决定(不影响行为)。
	Private Shared _TransientSkipLogged As Boolean = False
	' DPAPI 密文存储前缀：有前缀=DPAPI blob（解密失败=需重填）；无前缀=历史明文（直接沿用，
	' 下次保存时转加密迁移）。前缀法避免 base64 启发式误判（如 8 位字母数字口令恰为合法 base64）。
	' 注意：降级到旧版本会读到 "DPAPI:..." 原字串当密码用——升级前备份配置（release notes 已要求）。
	Private Const DPAPI_PREFIX As String = "DPAPI:"
	' P0-4：DPAPI 解密失败（换机器/换账户恢复配置）的运行时标记，不落盘。
	' 由设置界面读取并提示用户重填；用户保存新值后清零。
	Public ProxyPasswordNeedsReentry As Boolean
	Public ServidorStreamingPasswordNeedsReentry As Boolean

	''' <summary>R-P0-1:ELCAccounts 的内容指纹(SHA-256/base64)。
	''' Account.User/Key 是随机 IV 的 DPAPI 密文,不能参与 OuterXml 去重比对(否则每次调用都
	''' 不同、后台每 5s 必落盘 —— 与 ServidorWebPassword 后移到比对之后同理),所以这里按
	''' 明文内容取哈希:既能感知账号的增/删/改/顺序变化,又只留哈希不留明文副本。</summary>
	Private Function ELCAccountsHash() As String
		Try
			Dim sb As New System.Text.StringBuilder()
			' P1-5:持统一锁遍历(列表可能正被 ELCAccountHelper.SaveToConfig 换引用/释放)
			SyncLock ELCAccountHelper.ELCAccountsLock
				For Each Account As ELCAccountHelper.Account In Me.ELCAccounts
					sb.Append(Account.Alias).Append(vbTab).Append(Account.URL).Append(vbTab).
						Append(Criptografia.ToInsecureString(Account.User)).Append(vbTab).
						Append(Criptografia.ToInsecureString(Account.Key)).Append(vbTab).
						Append(If(Account.DefaultAccount, "1", "0")).Append(vbLf)
				Next
			End SyncLock
			Using sha As System.Security.Cryptography.SHA256 = System.Security.Cryptography.SHA256.Create()
				Return System.Convert.ToBase64String(sha.ComputeHash(System.Text.Encoding.Unicode.GetBytes(sb.ToString())))
			End Using
		Catch ex As Exception
			' ELCAccountHelper.SaveToConfig 会先 Dispose 旧 SecureString、Clear 再重建整个列表,
			' 全程不持锁;而本方法可能正跑在 bgwActualizadorDatosDisco 的周期保存上
			' (Main.vb:2765 在 DoWork 内),撞上那个窗口会抛 ObjectDisposedException /
			' InvalidOperationException。返回 Nothing 只会让下个周期多存一次盘并重试,
			' 绝不能让异常冒出去把整次保存打掉(此处不在 GuardarXML 的 Try 覆盖范围内)。
			Log.WriteDebug("ELCAccountsHash skipped (account list busy): " & Log.SafeException(ex))
			Return Nothing
		End Try
	End Function

	Public Sub GuardarXML(ByVal ForzarGuardado As Boolean)
		ApplyConfigLimits()
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
        Xml.DocumentElement.AppendChild(Xml.CreateElement("MaxConexionesGuardadas")).InnerText = MaxConexionesGuardadas.ToString
		
		Xml.DocumentElement.AppendChild(Xml.CreateElement("TamanoPaqueteKB")).InnerText = TamanoPaqueteKB.ToString
		Xml.DocumentElement.AppendChild(Xml.CreateElement("TamanoBufferKB")).InnerText = TamanoBufferKB.ToString
		
		Xml.DocumentElement.AppendChild(Xml.CreateElement("DescargasSimultaneas")).InnerText = DescargasSimultaneas.ToString
		Xml.DocumentElement.AppendChild(Xml.CreateElement("ConexionesPorFichero")).InnerText = ConexionesPorFichero.ToString
		
		Xml.DocumentElement.AppendChild(Xml.CreateElement("ResetearErrores")).InnerText = ResetearErrores.ToString
		Xml.DocumentElement.AppendChild(Xml.CreateElement("QuotaBreakthroughMode")).InnerText = QuotaBreakthroughMode.ToString
		Xml.DocumentElement.AppendChild(Xml.CreateElement("ResetearErroresMigratedV25")).InnerText = ResetearErroresMigratedV25.ToString
		Xml.DocumentElement.AppendChild(Xml.CreateElement("ColumnUIDefaultsMigratedV26")).InnerText = ColumnUIDefaultsMigratedV26.ToString
		Xml.DocumentElement.AppendChild(Xml.CreateElement("ColumnUIDefaultsMigratedV25")).InnerText = ColumnUIDefaultsMigratedV26.ToString

        Xml.DocumentElement.AppendChild(Xml.CreateElement("ApagarPC")).InnerText = ApagarPC.ToString
		
        Xml.DocumentElement.AppendChild(Xml.CreateElement("ComenzarDescargando")).InnerText = ComenzarDescargando.ToString

        Xml.DocumentElement.AppendChild(Xml.CreateElement("CheckUpdates")).InnerText = CheckUpdates.ToString
        Xml.DocumentElement.AppendChild(Xml.CreateElement("UpdateSkipVersion")).InnerText = UpdateSkipVersion
		
		Xml.DocumentElement.AppendChild(Xml.CreateElement("MantenerUltimaConfiguracion")).InnerText = MantenerUltimaConfiguracion.ToString
		
		Xml.DocumentElement.AppendChild(Xml.CreateElement("UsarProxy")).InnerText = UsarProxy.ToString
		
		Xml.DocumentElement.AppendChild(Xml.CreateElement("ProxyPort")).InnerText = ProxyPort.ToString
		Xml.DocumentElement.AppendChild(Xml.CreateElement("ProxyIP")).InnerText = ProxyIP
		Xml.DocumentElement.AppendChild(Xml.CreateElement("ProxyUser")).InnerText = ProxyUser
		' P0-4:口令密文写盘后移（见比对后 DPAPI 块）。此处不写明文节点，
		' 旧明文值在首次保存后即被密文替换（向前兼容读侧保留明文回退）。

		Xml.DocumentElement.AppendChild(Xml.CreateElement("VLCPath")).InnerText = VLCPath
		
		Xml.DocumentElement.AppendChild(Xml.CreateElement("ResetearErroresPeriodoMinutos")).InnerText = ResetearErroresPeriodoMinutos.ToString
		
		Xml.DocumentElement.AppendChild(Xml.CreateElement("NivelLog")).InnerText = [Enum].GetName(GetType(Log.LevelLogType), NivelLog)
		
		Xml.DocumentElement.AppendChild(Xml.CreateElement("PrioridadDescompresion")).InnerText = [Enum].GetName(GetType(DescompresionPriority), PrioridadDescompresion)
		
		Xml.DocumentElement.AppendChild(Xml.CreateElement("LimiteVelocidadKBs")).InnerText = LimiteVelocidadKBs.ToString
		
		Xml.DocumentElement.AppendChild(Xml.CreateElement("IniciarConWindows")).InnerText = IniciarConWindows.ToString
		If NoMostrarAvisoURI Then Xml.DocumentElement.AppendChild(Xml.CreateElement("NoMostrarAvisoURI")).InnerText = NoMostrarAvisoURI.ToString
		
		Xml.DocumentElement.AppendChild(Xml.CreateElement("ServidorStreamingActivo")).InnerText = ServidorStreamingActivo.ToString
        Xml.DocumentElement.AppendChild(Xml.CreateElement("ServidorStreamingPuerto")).InnerText = ServidorStreamingPuerto.ToString
		' P0-4:同 ProxyPassword，密文节点后移，见下。

		Xml.DocumentElement.AppendChild(Xml.CreateElement("ServidorWebActivo")).InnerText = ServidorWebActivo.ToString
		Xml.DocumentElement.AppendChild(Xml.CreateElement("ServidorWebNombre")).InnerText = ServidorWebNombre
		Xml.DocumentElement.AppendChild(Xml.CreateElement("ServidorWebRutaPlantilla")).InnerText = ServidorWebRutaPlantilla
		' B2-⑦:ServidorWebPassword 密文(随机 IV,每次不同)不得参与去重比对,
		' 否则 OuterXml 恒变、后台每 5s 必落盘。此处先不写该节点,比对通过后再补(见下)。
		Xml.DocumentElement.AppendChild(Xml.CreateElement("ServidorWebTimeout")).InnerText = ServidorWebTimeout.ToString
        Xml.DocumentElement.AppendChild(Xml.CreateElement("ServidorWebPermitirLAN")).InnerText = ServidorWebPermitirLAN.ToString
        Xml.DocumentElement.AppendChild(Xml.CreateElement("ServidorWebBindIP")).InnerText = ServidorWebBindIP
        Xml.DocumentElement.AppendChild(Xml.CreateElement("ServidorWebPuerto")).InnerText = ServidorWebPuerto.ToString
		
		If HideCollaborateButton Then Xml.DocumentElement.AppendChild(Xml.CreateElement("HideCollaborateButton")).InnerText = HideCollaborateButton.ToString
		
		
		ConfigUI.GuardarXML(Xml)
		
		Dim Fichero As String
		
		Fichero = ObtenerRutaFicheroConfiguracion()

		' P0-3:加载时文件被瞬时占用、内存是默认值——拒绝写盘保住磁盘上的完好配置。
		' 必须挡在去重比对之前,否则 _LastSaved* 被更新后,占用解除时反而跳过真正的补写。
		If LoadTransientError AndAlso System.IO.File.Exists(Fichero) Then
			If Not _TransientSkipLogged Then
				_TransientSkipLogged = True
				Log.WriteWarning("Skipping configuration save: the on-disk file could not be loaded earlier (locked). It is preserved untouched.")
			End If
			ErrorConfig = ErrorConfigClass.Fichero_No_Creado
			Exit Sub
		End If

		Dim curWebPasswordPlain As String = If(ServidorWebPassword, "")
		Dim curProxyPasswordPlain As String = If(ProxyPassword, "")
		Dim curStreamingPasswordPlain As String = If(ServidorStreamingPassword, "")
		Dim curELCAccountsHash As String = ELCAccountsHash()
		If _LastSavedXML Is Nothing OrElse _LastSavedXML <> Xml.DocumentElement.OuterXml OrElse _LastSavedWebPasswordPlain Is Nothing OrElse _LastSavedWebPasswordPlain <> curWebPasswordPlain OrElse _LastSavedProxyPasswordPlain Is Nothing OrElse _LastSavedProxyPasswordPlain <> curProxyPasswordPlain OrElse _LastSavedStreamingPasswordPlain Is Nothing OrElse _LastSavedStreamingPasswordPlain <> curStreamingPasswordPlain OrElse _LastSavedELCAccountsHash Is Nothing OrElse _LastSavedELCAccountsHash <> curELCAccountsHash Or ForzarGuardado Then

			_LastSavedXML = Xml.DocumentElement.OuterXml
			_LastSavedWebPasswordPlain = curWebPasswordPlain
			_LastSavedProxyPasswordPlain = curProxyPasswordPlain
			_LastSavedStreamingPasswordPlain = curStreamingPasswordPlain
			' R-P0-1:Nothing(账号列表正被 SaveToConfig 重建)也照存,下个周期会因
			' "_LastSavedELCAccountsHash Is Nothing" 再存一次盘 —— 方向安全,只会多写不会漏写。
			_LastSavedELCAccountsHash = curELCAccountsHash

			' B2-⑦:随机 IV 密文只写盘、不比对。加密失败返回 Nothing 时跳过该节点,
			' 保留磁盘旧密文而非存入空值(与旧逻辑一致,只是位置后移)。
			Dim encryptedWebPassword As String = Criptografia.AES_EncryptString(ServidorWebPassword, KeyPassword)
			If encryptedWebPassword IsNot Nothing Then
				Xml.DocumentElement.AppendChild(Xml.CreateElement("ServidorWebPassword")).InnerText = encryptedWebPassword
			End If

		' Como el usuario y password se guarda cifrado con entropia, cada vez tendrá un valor distinto, no podemos compararlos...
		Xml.DocumentElement.AppendChild(Xml.CreateElement("Usuario")).InnerText = Criptografia.EncryptString_DPAPI(_Usuario)
		Xml.DocumentElement.AppendChild(Xml.CreateElement("Password")).InnerText = Criptografia.EncryptString_DPAPI(_Password)
		' P0-4:代理/流媒体口令 DPAPI 密文（非确定性，只写盘不比对）。
		' 空值照写空节点（读侧缺省空字串，语义不变）；仅加密失败返回 Nothing 时跳过
		' 该节点，保留磁盘旧值而非存入空值覆盖。
		Dim encryptedProxyPassword As String = EncryptPasswordField(ProxyPassword)
		If encryptedProxyPassword IsNot Nothing Then
			Xml.DocumentElement.AppendChild(Xml.CreateElement("ProxyPassword")).InnerText = encryptedProxyPassword
		End If
		Dim encryptedStreamingPassword As String = EncryptPasswordField(ServidorStreamingPassword)
		If encryptedStreamingPassword IsNot Nothing Then
			Xml.DocumentElement.AppendChild(Xml.CreateElement("ServidorStreamingPassword")).InnerText = encryptedStreamingPassword
		End If
			Xml.DocumentElement.AppendChild(SavePreSharedKeys(Xml))
			
			
			Dim AccountsNode As XmlNode = Xml.DocumentElement.AppendChild(Xml.CreateElement("ELCAccounts"))
			' P1-5:持统一锁遍历 —— 本循环在 GuardarXML 的 Try 之外,此前
			' ELCAccountHelper.SaveToConfig 就地重建列表会抛 "Collection was modified"
			' / ObjectDisposedException 直接炸死磁盘 worker(自动存盘全停)。
			SyncLock ELCAccountHelper.ELCAccountsLock
				For Each Account As ELCAccountHelper.Account In Me.ELCAccounts
					Dim AccountNode As XmlNode = AccountsNode.AppendChild(Xml.CreateElement("Account"))
					AccountNode.AppendChild(Xml.CreateElement("Name")).InnerText = Account.Alias
					AccountNode.AppendChild(Xml.CreateElement("URL")).InnerText = Account.URL
					AccountNode.AppendChild(Xml.CreateElement("User")).InnerText = Criptografia.EncryptString_DPAPI(Account.User)
					AccountNode.AppendChild(Xml.CreateElement("Key")).InnerText = Criptografia.EncryptString_DPAPI(Account.Key)
					AccountNode.AppendChild(Xml.CreateElement("Default")).InnerText = If(Account.DefaultAccount, "1", "0")
				Next
			End SyncLock
			
			Log.WriteDebug("Saving configuration")
			
            SyncLock Mutex.GuardarConfig
			Try
				AtomicFile.SaveXml(Xml, Fichero)
				' P0-2: clear the sticky error flag on success. Without this, one transient
				' failure (e.g. disk full) keeps ErrorConfig non-zero forever, so every later
				' save shows a bogus "Configuration could not be saved" and Configuration.vb
				' then Exit-Subs away the rest of the settings application.
				' Clearing unconditionally is safe ONLY because the LOAD result now lives in
				' LoadErrorConfig (see its declaration) - do not merge the two back together.
				ErrorConfig = ErrorConfigClass.SinErrores
			Catch ex As Exception
				Log.WriteError("Error saving configuration XML: " & Log.SafeException(ex))
				ErrorConfig = ErrorConfigClass.Fichero_No_Creado
				_LastSavedXML = Nothing
				_LastSavedWebPasswordPlain = Nothing
				_LastSavedProxyPasswordPlain = Nothing
				_LastSavedStreamingPasswordPlain = Nothing
				_LastSavedELCAccountsHash = Nothing
			End Try
			End SyncLock
			
			
			
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
		
		Log.WriteDebug("Loading configuration XML")
		
		Dim Xml As XmlDocument = Nothing
		Dim recoveredFromBackup As Boolean = False
		Dim transientFailure As Boolean = False
		SyncLock Mutex.GuardarConfig
			If Not AtomicFile.TryLoadXml(Fichero, Xml, recoveredFromBackup, transientFailure) Then
				If Not System.IO.File.Exists(Fichero) AndAlso Not System.IO.File.Exists(Fichero & ".bak") Then
					Log.WriteWarning("Configuration file does not exist")
					ErrorConfig = ErrorConfigClass.Fichero_No_Existe
					LoadErrorConfig = ErrorConfigClass.Fichero_No_Existe
				Else
					ErrorConfig = ErrorConfigClass.Fichero_No_Valido
					LoadErrorConfig = ErrorConfigClass.Fichero_No_Valido
					' P0-3:被占用(杀软/备份/同步盘)≠损坏。内存照跑默认值,但磁盘文件
					' 可能完好,置标记让 GuardarXML 一律拒写,保住旧文件。
					LoadTransientError = transientFailure
					If transientFailure Then
						Log.WriteError("Configuration file is locked by another process (transient). Running with defaults; writes are suspended to preserve the on-disk file.")
					Else
						Log.WriteError("Configuration file could not be loaded (primary and backup failed).")
					End If
				End If
				ConfiguracionDefectoVacia()
				Exit Sub
			End If
			If recoveredFromBackup Then
				Log.WriteWarning("Configuration restored from backup file.")
			End If
		End SyncLock
		
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
		ProxyPassword = DecryptPasswordField(LeerNodo(Xml, "ProxyPassword", ""), "ProxyPassword", ProxyPasswordNeedsReentry)
		VLCPath = LeerNodo(Xml, "VLCPath", "")
		_Usuario = Criptografia.DecryptString_DPAPI(LeerNodo(Xml, "Usuario", ""))
		_Password = Criptografia.DecryptString_DPAPI(LeerNodo(Xml, "Password", ""))
		ExtraerAutomaticamente = False
		CrearDirectorioPaquete = False
		AnalizarPortapapeles = False
		'PermitirSkins = True
		ResetearErrores = False
		QuotaBreakthroughMode = False
		UsarProxy = False
		ApagarPC = False
        ComenzarDescargando = True
		IniciarConWindows = False
		MantenerUltimaConfiguracion = True
        HideCollaborateButton = False
        CheckUpdates = True
        Boolean.TryParse(LeerNodo(Xml, "CheckUpdates", "true"), CheckUpdates)
        UpdateSkipVersion = LeerNodo(Xml, "UpdateSkipVersion", "")
		Boolean.TryParse(LeerNodo(Xml, "HideCollaborateButton", "false"), HideCollaborateButton)
		Boolean.TryParse(LeerNodo(Xml, "ExtraerAutomaticamente", "false"), ExtraerAutomaticamente)
		Boolean.TryParse(LeerNodo(Xml, "CondicionesAceptadas", "false"), CondicionesAceptadas)
		Boolean.TryParse(LeerNodo(Xml, "CrearDirectorioPaquete", "false"), CrearDirectorioPaquete)
		Boolean.TryParse(LeerNodo(Xml, "AnalizarPortapapeles", "false"), AnalizarPortapapeles)
		'Boolean.TryParse(LeerNodo(Xml, "PermitirSkins", "true"), PermitirSkins)
		Boolean.TryParse(LeerNodo(Xml, "ResetearErrores", "true"), ResetearErrores)
		Boolean.TryParse(LeerNodo(Xml, "QuotaBreakthroughMode", "false"), QuotaBreakthroughMode)
		Boolean.TryParse(LeerNodo(Xml, "ResetearErroresMigratedV25", "false"), ResetearErroresMigratedV25)
		Boolean.TryParse(LeerNodo(Xml, "ColumnUIDefaultsMigratedV26", "false"), ColumnUIDefaultsMigratedV26)
		Dim migratedV25Alias As Boolean = False
		Boolean.TryParse(LeerNodo(Xml, "ColumnUIDefaultsMigratedV25", "false"), migratedV25Alias)
		If migratedV25Alias Then ColumnUIDefaultsMigratedV26 = True
		Dim needV25Migration As Boolean = Not ResetearErroresMigratedV25
		Boolean.TryParse(LeerNodo(Xml, "UsarProxy", "false"), UsarProxy)
		Boolean.TryParse(LeerNodo(Xml, "IniciarConWindows", "false"), IniciarConWindows)
		Boolean.TryParse(LeerNodo(Xml, "NoMostrarAvisoURI", "false"), NoMostrarAvisoURI)
		Boolean.TryParse(LeerNodo(Xml, "MantenerUltimaConfiguracion", "true"), MantenerUltimaConfiguracion)
        Boolean.TryParse(LeerNodo(Xml, "ComenzarDescargando", "true"), ComenzarDescargando)
		Boolean.TryParse(LeerNodo(Xml, "ApagarPC", "false"), ApagarPC)
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
        Integer.TryParse(LeerNodo(Xml, "MaxConexionesGuardadas", "100"), MaxConexionesGuardadas)
		Integer.TryParse(LeerNodo(Xml, "ResetearErroresPeriodoMinutos", "15"), ResetearErroresPeriodoMinutos)
        If ResetearErroresPeriodoMinutos < 1 Or ResetearErroresPeriodoMinutos > 999 Then
            ResetearErroresPeriodoMinutos = 15
        End If
		If needV25Migration Then
			' v2.5 beta:存量配置一次性迁移到默认开(15min)。新装直接缺省 True,不走这里。
			ResetearErrores = True
			ResetearErroresMigratedV25 = True
			Log.WriteWarning("Migrated ResetearErrores to True (v2.5 default-on self-heal).")
		End If
		ApplyConfigLimits()
		
		NivelLog = Log.LevelLogType.Normal
		If [Enum].IsDefined(GetType(Log.LevelLogType), LeerNodo(Xml, "NivelLog", "")) Then
			NivelLog = CType([Enum].Parse(GetType(Log.LevelLogType), LeerNodo(Xml, "NivelLog", "")), Log.LevelLogType)
		End If
		
		PrioridadDescompresion = DescompresionPriority.Normal
		If [Enum].IsDefined(GetType(DescompresionPriority), LeerNodo(Xml, "PrioridadDescompresion", "")) Then
			PrioridadDescompresion = CType([Enum].Parse(GetType(DescompresionPriority), LeerNodo(Xml, "PrioridadDescompresion", "")), DescompresionPriority)
		End If
		
		DescargasSimultaneas = 3
		ConexionesPorFichero = 3
		Integer.TryParse(LeerNodo(Xml, "DescargasSimultaneas", "3"), DescargasSimultaneas)
		Integer.TryParse(LeerNodo(Xml, "ConexionesPorFichero", "3"), ConexionesPorFichero)
		ApplyConfigLimits()
		
		' Servidor streaming
		ServidorStreamingActivo = False
		Boolean.TryParse(LeerNodo(Xml, "ServidorStreamingActivo", "false"), ServidorStreamingActivo)
		ServidorStreamingPuerto = DEFAULT_STREAMING_PORT
        Integer.TryParse(LeerNodo(Xml, "ServidorStreamingPuerto", DEFAULT_STREAMING_PORT.ToString), ServidorStreamingPuerto)
        ServidorStreamingPassword = DecryptPasswordField(LeerNodo(Xml, "ServidorStreamingPassword", ""), "ServidorStreamingPassword", ServidorStreamingPasswordNeedsReentry)

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
			' 解密失败必须记录并回退:保留密文原值当密码用会导致登录永远失败且日志无线索
			Log.WriteError("ServidorWebPassword decrypt failed; resetting to empty: " & ex.Message)
			ServidorWebPassword = ""
		End Try
		ServidorWebTimeout = 5
		Integer.TryParse(LeerNodo(Xml, "ServidorWebTimeout", "5"), ServidorWebTimeout)
		ServidorWebPermitirLAN = False
		Boolean.TryParse(LeerNodo(Xml, "ServidorWebPermitirLAN", "false"), ServidorWebPermitirLAN)
		ServidorWebBindIP = LeerNodo(Xml, "ServidorWebBindIP", "")
		ServidorWebPuerto = 0
		Integer.TryParse(LeerNodo(Xml, "ServidorWebPuerto", "0"), ServidorWebPuerto)
		
		' Pre shared keys
		Me.ListaPreSharedKeys = New List(Of SecureString)
		For Each keynode As XmlNode In Xml.DocumentElement.SelectNodes("PreSharedKeys/Key")
			Me.ListaPreSharedKeys.Add(Criptografia.DecryptString_DPAPI(keynode.InnerText))
		Next
		
		If _Usuario Is Nothing OrElse _Password Is Nothing OrElse _Usuario.Length = 0 Or _Password.Length = 0 Then
			'ErrorConfig = ErrorConfigClass.Usuario_Password_Incorrecto
		End If
		If ConexionesPorFichero = 0 Then
			ErrorConfig = ErrorConfigClass.Fichero_No_Valido
			' R1: this is a LOAD-path failure too (see LoadErrorConfig) - without this line
			' Main.vb:450 would no longer open the configuration form for this case.
			LoadErrorConfig = ErrorConfigClass.Fichero_No_Valido
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
		Me.ResetearErrores = True
		Me.ResetearErroresMigratedV25 = True
		Me.ResetearErroresPeriodoMinutos = 15
		Me.Idioma = System.Threading.Thread.CurrentThread.CurrentUICulture.Name
		Me.ExtraerAutomaticamente = False
		Me.CondicionesAceptadas = False
		Me.CrearDirectorioPaquete = True
		Me.AnalizarPortapapeles = True
		Me.HideCollaborateButton = False
		Me.ApagarPC = False
        Me.UsarProxy = False
        ' P3-5(b): align the empty-config default with CargaXML (:407 pre-read
        ' assignment and :428 node default "true", Forms/Configuration.vb:552 write
        ' path). The field default was False here, so a fresh install behaved
        ' differently from every loaded/reset configuration (inverted default).
        Me.MantenerUltimaConfiguracion = True
        Me.ComenzarDescargando = True
        Me.CheckUpdates = True
        Me.UpdateSkipVersion = ""
		Me.ProxyPort = 0
		Me.VersionConfig = "0"
		Me.DescargasSimultaneas = 3
		Me.ConexionesPorFichero = 3
		Me.ProxyIP = ""
		Me.ServidorWebTimeout = 5
		Me.ServidorWebPermitirLAN = False
		Me.ServidorWebBindIP = ""
        Me.MaxConexionesGuardadas = 100
		Me.NivelLog = Log.LevelLogType.Normal
		Me.ServidorStreamingActivo = False
		Me.ServidorWebActivo = False
		Me.ServidorStreamingPuerto = DEFAULT_STREAMING_PORT
		Me.PrioridadDescompresion = DescompresionPriority.Normal
		Me._Usuario = Criptografia.ToSecureString("")
		Me._Password = Criptografia.ToSecureString("")
		Me.ListaPreSharedKeys = New List(Of SecureString)
		Me.TamanoPaqueteKB = 50
		Me.TamanoBufferKB = 750
		Me._ELCAccountList = New Generic.List(Of ELCAccountHelper.Account)
		Me.ConfigUI.ConfiguracionDefectoVacia()
		ApplyConfigLimits()
	End Sub

	''' <summary>
	''' Clamps configuration values so hand-edited XML cannot bypass UI safety limits.
	''' </summary>
	Public Sub ApplyConfigLimits()
		Const MaxPackageKB As Integer = 1024
		Const MaxBufferKB As Integer = 16 * 1024
		Const MaxConnections As Integer = 32
		Const MaxSimultaneous As Integer = 20
		Const MaxSpeedKBs As Integer = 100 * 1024 * 1024 ' 100 GB/s absurd upper bound

		If LimiteVelocidadKBs < 0 Then LimiteVelocidadKBs = 0
		If LimiteVelocidadKBs > MaxSpeedKBs Then LimiteVelocidadKBs = MaxSpeedKBs
		If TamanoPaqueteKB < 1 Then TamanoPaqueteKB = 50
		If TamanoPaqueteKB > MaxPackageKB Then TamanoPaqueteKB = MaxPackageKB
		If TamanoBufferKB < 1 Then TamanoBufferKB = 750
		If TamanoBufferKB > MaxBufferKB Then TamanoBufferKB = MaxBufferKB
		If TamanoBufferKB < TamanoPaqueteKB Then TamanoBufferKB = Math.Min(MaxBufferKB, TamanoPaqueteKB * 5)
		If DescargasSimultaneas < 1 Then DescargasSimultaneas = 3
		If DescargasSimultaneas > MaxSimultaneous Then DescargasSimultaneas = MaxSimultaneous
		If ConexionesPorFichero < 1 Then ConexionesPorFichero = 3
		If ConexionesPorFichero > MaxConnections Then ConexionesPorFichero = MaxConnections
		If MaxConexionesGuardadas < 1 Then MaxConexionesGuardadas = 100
		If MaxConexionesGuardadas > 10000 Then MaxConexionesGuardadas = 10000

		' P3-5(a): the UI validates the ports (Forms/Configuration.vb:439/509), but a
		' hand-edited Configuration.xml could inject out-of-range values that reached
		' New WebProxy(host, port) (Conexion.vb:100, hot request path) and the HTTP
		' server binders unvalidated. Clamp here — this method's stated purpose is
		' "hand-edited XML cannot bypass UI safety limits". ProxyPort is only touched
		' while the proxy is enabled: 0 with UsarProxy=False is the documented default
		' ("no port configured") and must survive a fresh/reset config untouched.
		If UsarProxy AndAlso (ProxyPort < 1 OrElse ProxyPort > 65535) Then
			Dim fixedProxyPort As Integer = If(ProxyPort > 65535, 65535, 8080)
			Log.WriteWarning("Proxy port " & ProxyPort & " is out of range; clamped to " & fixedProxyPort & ".")
			ProxyPort = fixedProxyPort
		End If
		If ServidorStreamingPuerto < 0 Then
			' 0 keeps its documented "not configured" meaning (empty textbox in the UI);
			' only clearly invalid negatives are normalized.
			Log.WriteWarning("Streaming port " & ServidorStreamingPuerto & " is negative; reset to 0 (not configured).")
			ServidorStreamingPuerto = 0
		ElseIf ServidorStreamingPuerto > 65535 Then
			Log.WriteWarning("Streaming port " & ServidorStreamingPuerto & " is out of range; clamped to 65535.")
			ServidorStreamingPuerto = 65535
		End If
		If ServidorWebPuerto < 0 Then
			Log.WriteWarning("Web server port " & ServidorWebPuerto & " is negative; reset to 0 (not configured).")
			ServidorWebPuerto = 0
		ElseIf ServidorWebPuerto > 65535 Then
			Log.WriteWarning("Web server port " & ServidorWebPuerto & " is out of range; clamped to 65535.")
			ServidorWebPuerto = 65535
		End If

		If Not String.IsNullOrWhiteSpace(VLCPath) Then
			Try
				If Directory.Exists(VLCPath) Then
					' OK: directory containing vlc.exe
				ElseIf File.Exists(VLCPath) AndAlso VLCPath.ToLowerInvariant().EndsWith(".exe") Then
					VLCPath = Path.GetDirectoryName(VLCPath)
				Else
					Log.WriteWarning("VLC path is invalid and was cleared: " & VLCPath)
					VLCPath = ""
				End If
			Catch ex As Exception
				Log.WriteWarning("VLC path validation failed, clearing VLCPath (" & If(VLCPath, "") & "): " & Log.SafeException(ex))
				VLCPath = ""
			End Try
		End If
	End Sub
	
	Public Shared Sub RegisterInStartup(isChecked As Boolean)
		Try

			Dim registryKey As Microsoft.Win32.RegistryKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\Microsoft\Windows\CurrentVersion\Run", True)
			If registryKey Is Nothing And isChecked Then
	    		registryKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey("SOFTWARE\Microsoft\Windows\CurrentVersion\Run")
	    	End If
			If registryKey Is Nothing Then
				' Could not open/create the registry key; nothing else to do.
				Return
			End If
			Try
				If isChecked Then
					registryKey.SetValue("MegaDownloader", """" & Application.ExecutablePath & """ -silent")
				Else
					registryKey.DeleteValue("MegaDownloader", False)
				End If
			Finally
				registryKey.Close()
			End Try

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

	''' <summary>
	''' P0-4：口令字段写盘加密。空值返回 ""（节点照写空字串，保持旧行为）；
	''' 非空返回 "DPAPI:" + DPAPI 密文。加密失败返回 Nothing，调用方跳过该节点
	''' （保留磁盘旧值，而非存入空值覆盖——与 ServidorWebPassword 模式一致）。
	''' </summary>
	Private Shared Function EncryptPasswordField(ByVal plainValue As String) As String
		If String.IsNullOrEmpty(plainValue) Then Return ""
		Try
			Return DPAPI_PREFIX & Criptografia.EncryptString_DPAPI(Criptografia.ToSecureString(plainValue))
		Catch ex As Exception
			Log.WriteError("EncryptPasswordField: DPAPI encrypt failed, keeping on-disk value: " & Log.SafeException(ex))
			Return Nothing
		End Try
	End Function

	''' <summary>
	''' P0-4：口令字段双读。无前缀=历史明文（直接沿用，迁移由下次保存完成）；
	''' 有前缀=DPAPI blob，解密成功返回明文，失败则 needsReentry=True 并返回 ""，
	''' 由设置界面提示用户重填（绝不静默置空当正常值用）。
	''' </summary>
	Private Shared Function DecryptPasswordField(ByVal rawValue As String, ByVal fieldName As String, ByRef needsReentry As Boolean) As String
		needsReentry = False
		If String.IsNullOrEmpty(rawValue) Then Return ""
		If Not rawValue.StartsWith(DPAPI_PREFIX, StringComparison.Ordinal) Then
			Return rawValue
		End If
		Dim blob As String = rawValue.Substring(DPAPI_PREFIX.Length)
		Dim plain As String = ""
		Try
			plain = Criptografia.ToInsecureString(Criptografia.DecryptString_DPAPI(blob))
		Catch ex As Exception
			plain = ""
			Log.WriteWarning(fieldName & ": DPAPI decrypt threw, treating as needs-reentry: " & Log.SafeException(ex))
		End Try
		If String.IsNullOrEmpty(plain) Then
			needsReentry = True
			Log.WriteWarning(fieldName & ": stored password cannot be decrypted on this machine/user (config restored from backup or DPAPI key lost); please re-enter the password in Settings. 口令无法在本机/账户解密（如从备份恢复配置），请在设置中重新输入。")
			Return ""
		End If
		Return plain
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
