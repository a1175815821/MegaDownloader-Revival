Imports System.ComponentModel

Public Class StreamingForm

    Public Config As Configuracion
    Public MainForm As Main


    Private WithEvents bckActualizador As BackgroundWorker

    Private Sub StreamingForm_Load(sender As Object, e As System.EventArgs) Handles Me.Load
        Translate()

        If Not Config.ServidorStreamingActivo Then
            txtUrlMEGA.Enabled = False
            txtUrlStreaming.Enabled = False
            lblInfo.Text = Language.GetText("Streaming server not activated")
            lblInfo.ForeColor = Color.Red
        End If


        bckActualizador = New BackgroundWorker
        bckActualizador.WorkerSupportsCancellation = True
        bckActualizador.RunWorkerAsync()
        
   		' Centramos la pantalla
        ' http://stackoverflow.com/questions/7892090/how-to-set-winform-start-position-at-top-right
        Dim scr = Screen.FromPoint(Me.Location)
        Me.Location = New Point(CInt((scr.WorkingArea.Right - Me.Width) / 2), CInt((scr.WorkingArea.Bottom - Me.Height) / 2))
        
    End Sub

 
    Private Sub Translate()
        Me.Label1.Text = Language.GetText("MEGA Url") & ":"
        Me.Label2.Text = Language.GetText("Streaming Url") & ":"
        Me.GroupBox2.Text = Language.GetText("Streaming")
        Me.GroupBox3.Text = Language.GetText("Information")
        Me.lblInfo.Text = Language.GetText("Streaming Info")
        Me.btnCerrar.Text = Language.GetText("Close")
        Me.btnLanzarVLC.Text = Language.GetText("Start VLC")
        Me.Text = Language.GetText("Streaming")
    End Sub


    Public Sub bckActualizador_DoWork(sender As Object, e As DoWorkEventArgs) Handles bckActualizador.DoWork
        Dim worker As BackgroundWorker = CType(sender, BackgroundWorker)

        While Not worker.CancellationPending

            System.Threading.Thread.Sleep(150)

            If worker.CancellationPending Then Exit While

            ActualizarDatos()
        End While

        bckActualizador = Nothing
    End Sub

    Private Sub Cerrando() Handles Me.FormClosed
        bckActualizador.CancelAsync()
    End Sub

    Private PreviousURLMega As String = String.Empty
    Private ValidURLMega As Boolean = False

    Delegate Sub ActualizarDatosCallback()
    Private Sub ActualizarDatos()
        If Me.txtUrlMEGA.InvokeRequired Then
            Try
                Dim d As New ActualizarDatosCallback(AddressOf ActualizarDatos)
                Me.Invoke(d, New Object() {})
            Catch ex As Exception
                ' A veces da error al cerrar la ventana
            End Try

        Else

            If Not Config.ServidorStreamingActivo Then
                ValidURLMega = False
                Exit Sub
            End If

            Dim URLMega As String = Me.txtUrlMEGA.Text
            If URLMega <> PreviousURLMega Then
                PreviousURLMega = URLMega
                ValidURLMega = False

                Dim StreamingLink As String = StreamingHelper.CreateStreamingLink(URLMega, Config.ServidorStreamingPuerto, Config)

                If String.IsNullOrEmpty(StreamingLink) Then Exit Sub

                txtUrlStreaming.Text = StreamingLink

                ValidURLMega = True

            End If

        End If
    End Sub

    Private Sub btnLanzarVLC_Click(sender As System.Object, e As System.EventArgs) Handles btnLanzarVLC.Click

        If Not ValidURLMega Then Exit Sub
        StreamingHelper.WatchOnline(Config.VLCPath, txtUrlStreaming.Text)

    End Sub

    Private Sub btnCerrar_Click(sender As System.Object, e As System.EventArgs) Handles btnCerrar.Click
        Me.Close()
    End Sub

    Private Sub HelpButtonPressed() Handles Me.HelpButtonClicked
        Main.FAQ_Click(Nothing, Nothing)
    End Sub
End Class