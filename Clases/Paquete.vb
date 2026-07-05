Imports System.Xml
Imports System.IO

Public Class Paquete
    Implements IDescarga

    Public Sub New()
    End Sub

    Public Nombre As String

    Public RutaLocal As String

	Public CrearSubdirectorio As Boolean

	Public PendienteNombrePaquete As Boolean

    Public ListaFicheros As Generic.List(Of Fichero)

    Public ExtraccionFicheroAutomatica As Boolean

    Public ExtraccionFicheroPassword As String

    Public Prioridad As Integer

    Private EstadoDescarga As Estado

    Public Porcentaje As Decimal

    Public TamanoBytes As Long

    Private VelocidadKBs As Decimal

    Private TiempoEstimadoDescarga As String

    Private Function HayFicheros() As Boolean
        Mutex.ListaDescargas.WaitOne()
        Dim hay As Boolean = ListaFicheros IsNot Nothing AndAlso ListaFicheros.Count > 0
        Mutex.ListaDescargas.ReleaseMutex()
        Return hay
    End Function

    Public Sub AgregarFichero(ByVal Fichero As Fichero)
        Mutex.ListaDescargas.WaitOne()
        If ListaFicheros Is Nothing Then
            ListaFicheros = New Generic.List(Of Fichero)
        End If
        ListaFicheros.Add(Fichero)
        Mutex.ListaDescargas.ReleaseMutex()
    End Sub

    Public Sub ActualizarDatosDescarga()

        If Not HayFicheros() Then
            EstadoDescarga = Estado.EnCola
            Porcentaje = 0
            TamanoBytes = 0
            VelocidadKBs = 0
        Else

            Dim TotalVelocidad As Decimal = 0
            Dim HayDescargando As Boolean = False
            Dim HayCompletado As Boolean = False
            Dim HayEncola As Boolean = False
            Dim HayErroneos As Boolean = False
            Dim HayGenerandoLocal As Boolean = False
            Dim HayPausados As Boolean = False
            Dim HayDescomprimiendo As Boolean = False
            Dim HayComprobandoMD5 As Boolean = False
            Dim HayVerificando As Boolean = False
            Dim Total As Long = 0
            Dim TotalDesc As Long = 0

            Mutex.ListaDescargas.WaitOne()
            For Each fichero As Fichero In ListaFicheros
                Total += fichero.DescargaTamanoBytes
                TotalDesc += CLng(fichero.DescargaTamanoBytes * (fichero.DescargaPorcentaje / 100))
                Select Case fichero.DescargaEstado
                    Case Estado.Descargando
                        TotalVelocidad += fichero.DescargaVelocidadKBs
                        HayDescargando = True
                    Case Estado.Completado
                        HayCompletado = True
                    Case Estado.EnCola
                        HayEncola = True
                    Case Estado.Pausado
                        HayPausados = True
                    Case Estado.ComprobandoMD5
                        HayComprobandoMD5 = True
                    Case Estado.Descomprimiendo
                        HayDescomprimiendo = True
                    Case Estado.Verificando
                        HayVerificando = True
                    Case Estado.CreandoLocal
                        HayGenerandoLocal = True
                    Case Else
                        HayErroneos = True
                End Select
            Next
            Mutex.ListaDescargas.ReleaseMutex()
            If HayDescargando Then
                EstadoDescarga = Estado.Descargando
            ElseIf HayPausados Then
                EstadoDescarga = Estado.Pausado
            ElseIf HayErroneos Then
                EstadoDescarga = Estado.Erroneo
            ElseIf HayGenerandoLocal Then
                EstadoDescarga = Estado.CreandoLocal
            ElseIf HayEncola Then
                EstadoDescarga = Estado.EnCola
            ElseIf HayComprobandoMD5 Then
                EstadoDescarga = Estado.ComprobandoMD5
            ElseIf HayDescomprimiendo Then
                EstadoDescarga = Estado.Descomprimiendo
            ElseIf HayVerificando Then
                EstadoDescarga = Estado.Verificando
            Else
                EstadoDescarga = Estado.Completado
            End If

            TamanoBytes = Total
            VelocidadKBs = Math.Round(TotalVelocidad, 2)

            If TamanoBytes = 0 Then
                Porcentaje = 0
            Else
                Porcentaje = CDec(100 * TotalDesc / Total)
            End If
        End If

        If VelocidadKBs = 0 Then
            TiempoEstimadoDescarga = " --- "
        Else
            Dim BytesRestantes As Long = TamanoBytes - CLng(Porcentaje * TamanoBytes / 100)
            Dim SegundosRestantes As Double = BytesRestantes / (1024 * VelocidadKBs)
            Dim t As TimeSpan = TimeSpan.FromSeconds(SegundosRestantes)

            If SegundosRestantes > 3600 Then
                TiempoEstimadoDescarga = String.Format("{0:D2}h:{1:D2}m:{2:D2}s",
                                            (t.Days * 24) + t.Hours,
                                            t.Minutes,
                                            t.Seconds)
            Else
                TiempoEstimadoDescarga = String.Format("{0:D2}m:{1:D2}s",
                                           t.Minutes,
                                           t.Seconds)
            End If
        End If

    End Sub

    Public Function DescargaPrioridad() As Integer Implements IDescarga.DescargaPrioridad
        Return Me.Prioridad
    End Function

    Public WriteOnly Property SetDescargaPrioridad As Integer
        Set(value As Integer)
            Me.Prioridad = value
        End Set
    End Property

    Public WriteOnly Property SetDescargaExtraccionAutomatica(Password As String) As Boolean
        Set(value As Boolean)
            Me.ExtraccionFicheroAutomatica = value
            Me.ExtraccionFicheroPassword = Password
        End Set
    End Property

    Public Function DescargaNombre() As String Implements IDescarga.DescargaNombre
        Return Nombre
    End Function

    Public Function DescargaEstado() As Estado Implements IDescarga.DescargaEstado
        Return EstadoDescarga
    End Function

    Public Function DescargaExtraccionAutomatica() As Boolean Implements IDescarga.DescargaExtraccionAutomatica
        Return Me.ExtraccionFicheroAutomatica
    End Function

    Public Function DescargaExtraccionPassword() As String Implements IDescarga.DescargaExtraccionPassword
        Return Me.ExtraccionFicheroPassword
    End Function

    Public Function DescargaTiempoEstimadoDescarga() As String Implements IDescarga.DescargaTiempoEstimadoDescarga
        Return TiempoEstimadoDescarga
    End Function

    Public Function DescargaPorcentaje() As Decimal Implements IDescarga.DescargaPorcentaje
        Return Porcentaje
    End Function

    Public Function DescargaTamanoBytes() As Long Implements IDescarga.DescargaTamanoBytes
        Return TamanoBytes
    End Function

    Public Function DescargaVelocidadKBs() As Decimal Implements IDescarga.DescargaVelocidadKBs
        Return VelocidadKBs
    End Function


    Public Shared Function CargarDesdeFichero() As Generic.List(Of Paquete)
        Dim Fichero As String = ObtenerRutaFicheroDescargas()
        If Not System.IO.File.Exists(Fichero) Then
            Return New Generic.List(Of Paquete)
        End If

        Log.WriteDebug("Loading download list XML")

        Dim XML As New XmlDocument
        Mutex.GuardarDownloadList.WaitOne()
        Try
            XML.Load(Fichero)
        Catch ex As Exception

            ' Error manager - try to load bak file if it exists
            Log.WriteError("Error loading download list XML: " & ex.ToString)

            If System.IO.File.Exists(Fichero & ".bak") Then

                Log.WriteError("Let's try to load the backup file: " & Fichero & ".bak")
                Try
                    XML.Load(Fichero & ".bak")
                Catch ex2 As Exception
                    Log.WriteError("Error loading download backup list XML: " & ex2.ToString)
                    Return New Generic.List(Of Paquete)
                End Try

            Else
                Return New Generic.List(Of Paquete)
            End If

        Finally
            Mutex.GuardarDownloadList.ReleaseMutex()
        End Try

        Dim l As New Generic.List(Of Paquete)

        For Each NodoPaq As XmlNode In XML.DocumentElement.SelectNodes("Paquete")
            Dim p As New Paquete
            p.CargarXML(NodoPaq)
            l.Add(p)
        Next

        MarcarFicherosComoParados(l)
        Return l
    End Function


    Private Shared Sub MarcarFicherosComoParados(ByRef ListaPaquetes As Generic.List(Of Paquete))
        Mutex.ListaDescargas.WaitOne()
        For Each Paquete As Paquete In ListaPaquetes
            For Each Fichero As Fichero In Paquete.ListaFicheros
                If Fichero.DescargaEstado = Estado.Descargando Or _
                   Fichero.DescargaEstado = Estado.Pausado Then
                    Fichero.SetDescargaEstado = Estado.EnCola
                ElseIf Fichero.DescargaEstado = Estado.Descomprimiendo Or _
                       Fichero.DescargaEstado = Estado.Verificando Then
                    Fichero.SetDescargaEstado = Estado.Completado
                End If
                ThrottledStreamController.GetController.SetMaxSpeed(Fichero.FileID, Fichero.LimiteVelocidad)
            Next
        Next
        Mutex.ListaDescargas.ReleaseMutex()
    End Sub

    Private Shared _LastSavedXML As String = Nothing
    Public Shared Sub GuardarEnFichero(ByVal ListaPaquetes As Generic.List(Of Paquete))

        Dim Fichero As String = ObtenerRutaFicheroDescargas()

        Dim XML As XmlDocument = GuardarXML(ListaPaquetes, False)

        If _LastSavedXML Is Nothing OrElse _LastSavedXML <> XML.DocumentElement.OuterXml.GetHashCode.ToString Then

            _LastSavedXML = XML.DocumentElement.OuterXml.GetHashCode.ToString

            ' Como el usuario y password se guarda cifrado con entropia, cada vez tendrá un valor distinto, no podemos compararlos...
            XML = GuardarXML(ListaPaquetes, True)
            Log.WriteDebug("Saving download list")


            Mutex.GuardarDownloadList.WaitOne()

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
                XML.Save(Fichero)
                'Serializer.SerializarFichero(ListaPaquetes, Fichero)
            Finally
                Mutex.GuardarDownloadList.ReleaseMutex()
            End Try

        End If

    End Sub

    Private Shared Function GuardarXML(ByVal ListaPaquetes As Generic.List(Of Paquete), IncluirDatosCifrados As Boolean) As XmlDocument
        Dim XML As New XmlDocument
        XML.AppendChild(XML.CreateElement("ListaPaquetes"))

        If ListaPaquetes IsNot Nothing Then
            For Each Paquete As Paquete In ListaPaquetes
                XML.DocumentElement.AppendChild(Paquete.GuardarXML(XML, IncluirDatosCifrados))
            Next
        End If
        Return XML
    End Function


    Private Shared Function ObtenerRutaFicheroDescargas() As String

        Dim PathLog As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MegaDownloader/Config")

        If Not System.IO.Directory.Exists(PathLog) Then
            System.IO.Directory.CreateDirectory(PathLog)
        End If
        PathLog = Path.Combine(PathLog, "DownloadList.xml")
        Return PathLog
    End Function




    Public Sub CargarXML(ByVal XML As XmlNode)

        Me.Nombre = LeerNodo(XML, "Nombre", "")
        Me.RutaLocal = LeerNodo(XML, "RutaLocal", "")
        Boolean.TryParse(LeerNodo(XML, "CrearSubdirectorio", "false"), CrearSubdirectorio)
        Boolean.TryParse(LeerNodo(XML, "PendienteNombrePaquete", "false"), PendienteNombrePaquete)
        Boolean.TryParse(LeerNodo(XML, "ExtraccionFicheroAutomatica", "false"), ExtraccionFicheroAutomatica)
        Dim tempPass As String = LeerNodo(XML, "ExtraccionFicheroPassword", "")
        If Not String.IsNullOrEmpty(tempPass) Then
            Me.ExtraccionFicheroPassword = Criptografia.AES_DecryptString(tempPass, "passZIP")
        End If


        Integer.TryParse(LeerNodo(XML, "Prioridad", "0"), Prioridad)
        Decimal.TryParse(LeerNodo(XML, "Porcentaje", "0"), Porcentaje)
        Long.TryParse(LeerNodo(XML, "TamanoBytes", "0"), TamanoBytes)

        Me.ListaFicheros = New Generic.List(Of Fichero)
        For Each NodoFichero As XmlNode In XML.SelectNodes("ListaFicheros/Fichero")
            Dim fic As New Fichero
            fic.cargarxml(NodoFichero)
            Me.ListaFicheros.Add(fic)
        Next


    End Sub

    Public Function GuardarXML(ByVal XML As XmlDocument, IncluirDatosCifrados As Boolean) As XmlNode
        Dim NodoPaq As XmlNode = XML.CreateElement("Paquete")

        NodoPaq.AppendChild(XML.CreateElement("Nombre")).InnerText = Nombre
        NodoPaq.AppendChild(XML.CreateElement("RutaLocal")).InnerText = RutaLocal
        NodoPaq.AppendChild(XML.CreateElement("CrearSubdirectorio")).InnerText = CrearSubdirectorio.ToString
        NodoPaq.AppendChild(XML.CreateElement("ExtraccionFicheroAutomatica")).InnerText = ExtraccionFicheroAutomatica.ToString
        If Not String.IsNullOrEmpty(ExtraccionFicheroPassword) Then
            NodoPaq.AppendChild(XML.CreateElement("ExtraccionFicheroPassword")).InnerText = Criptografia.AES_EncryptString(ExtraccionFicheroPassword, "passZIP")
        End If
        NodoPaq.AppendChild(XML.CreateElement("Prioridad")).InnerText = Prioridad.ToString
        NodoPaq.AppendChild(XML.CreateElement("Porcentaje")).InnerText = Porcentaje.ToString
        NodoPaq.AppendChild(XML.CreateElement("TamanoBytes")).InnerText = TamanoBytes.ToString
        If PendienteNombrePaquete Then NodoPaq.AppendChild(XML.CreateElement("PendienteNombrePaquete")).InnerText = PendienteNombrePaquete.ToString


        If Me.ListaFicheros IsNot Nothing Then
            Dim nodoListaFic As XmlNode = NodoPaq.AppendChild(XML.CreateElement("ListaFicheros"))
            For Each Fichero As Fichero In Me.ListaFicheros
                nodoListaFic.AppendChild(Fichero.GuardarXML(XML, IncluirDatosCifrados))
            Next
        End If

        Return NodoPaq

    End Function

    Private Shared Function LeerNodo(ByRef NodoXML As XmlNode, ByRef Path As String, ByVal ValorDefecto As String) As String
        Dim nodo As XmlNode = NodoXML.SelectSingleNode(Path)
        If nodo Is Nothing Then
            Return ValorDefecto
        Else
            Return nodo.InnerText
        End If
    End Function

End Class
