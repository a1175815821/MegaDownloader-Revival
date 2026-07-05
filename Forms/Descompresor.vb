Imports System.ComponentModel

Public Class Descompresor



    Private WithEvents bckActualizador As BackgroundWorker

    Private Sub Descompresor_Load(sender As Object, e As System.EventArgs) Handles Me.Load
        translate()
    End Sub


    Private Sub Translate()
        Me.gbEstado.Text = Language.GetText("Status")
        Me.Label6.Text = Language.GetText("Size") & ":"
        Me.Label4.Text = Language.GetText("Current") & ":"
        Me.Label2.Text = Language.GetText("File") & ":"
        Me.GroupBox2.Text = Language.GetText("Extraction queue")
        Me.Label1.Text = Language.GetText("This screen lets you see the status of the files pending of extraction") & _
" " & Global.Microsoft.VisualBasic.ChrW(13) & Global.Microsoft.VisualBasic.ChrW(10) & "(" & Language.GetText("if automatic extraction option is enabled") & ")."
        Me.Text = Language.GetText("File decompressor")

    End Sub

    Private Sub Descompresor_Shown(sender As Object, e As System.EventArgs) Handles Me.Shown
        ActualizarDatos()

        bckActualizador = New BackgroundWorker
        bckActualizador.WorkerSupportsCancellation = True
        bckActualizador.RunWorkerAsync()
    End Sub

    Delegate Sub ActualizarDatosCallback()
    Private Sub ActualizarDatos()
        If Me.txtCola.InvokeRequired Then
            Try
                Dim d As New ActualizarDatosCallback(AddressOf ActualizarDatos)
                Me.Invoke(d, New Object() {})
            Catch ex As Exception
                ' A veces da error al cerrar la ventana
            End Try

        Else

            Dim desc As DescompresorController = DescompresorController.GetController

            Dim Elementos As Generic.List(Of String) = desc.GetCola
            lblFichero.Text = desc.EleActual_Ruta
            lblActual.Text = desc.EleActual_FicActNombre
            lblTamano.Text = "  -"
            Dim Total As Long? = desc.EleActual_TamanoTotal
            Dim Extraido As Long? = desc.EleActual_FicActExtraido
            Dim Extraido2 As Long? = desc.EleActual_TamanoTotalExtraido
            If Total.HasValue Then
                If Not Extraido.HasValue Then Extraido = 0
                If Not Extraido2.HasValue Then Extraido2 = 0
                Extraido += Extraido2
                lblTamano.Text = PintarTamano(Extraido.Value) & " / " & PintarTamano(Total.Value)
            End If
            If String.IsNullOrEmpty(lblFichero.Text) Then
                lblFichero.Text = "-"
            End If
            If String.IsNullOrEmpty(lblActual.Text) Then
                lblActual.Text = "-"
            End If

            ' Si el nombre del fichero es muy largo, evitamos que se corte
            While lblFichero.Width + 65 >= gbEstado.Width And lblFichero.Text.Length > 10
                Dim Longi As Integer = lblFichero.Text.Length
                Dim MitadLongi As Integer = CInt(Longi / 2)
                lblFichero.Text = lblFichero.Text.Substring(0, MitadLongi - 3) & " ... " & lblFichero.Text.Substring(MitadLongi + 3, Longi - (MitadLongi + 3))
                If lblFichero.Text.Length >= Longi Then Exit While
            End While

            While lblActual.Width + 220 >= gbEstado.Width And lblActual.Text.Length > 15
                Dim Longi As Integer = lblActual.Text.Length
                Dim MitadLongi As Integer = CInt(Longi / 2)
                lblActual.Text = lblActual.Text.Substring(0, MitadLongi - 4) & " ... " & lblActual.Text.Substring(MitadLongi + 3, Longi - (MitadLongi + 4))
                If lblActual.Text.Length >= Longi Then Exit While
            End While



            Dim str As String = ""
            For Each ele As String In Elementos
                str &= ele & vbNewLine
            Next

            If txtCola.Text <> str Then
                txtCola.Text = str
            End If

        End If
    End Sub

    Private Function PintarTamano(numBytes As Decimal) As String
        Dim Dato As String = "B"
        If numBytes > 1024 Then
            Dato = "KB"
            numBytes = numBytes / 1024
        End If
        If numBytes > 1024 Then
            Dato = "MB"
            numBytes = numBytes / 1024
        End If
        If numBytes > 1024 Then
            Dato = "GB"
            numBytes = numBytes / 1024
        End If
        If numBytes > 1024 Then
            Dato = "TB"
            numBytes = numBytes / 1024
        End If
        Return numBytes.ToString("F1") & " " & Dato

    End Function

    Public Sub bckActualizador_DoWork(sender As Object, e As DoWorkEventArgs) Handles bckActualizador.DoWork
        Dim worker As BackgroundWorker = CType(sender, BackgroundWorker)

        While Not worker.CancellationPending

            System.Threading.Thread.Sleep(300)

            If worker.CancellationPending Then Exit While

            ActualizarDatos()
        End While

        bckActualizador = Nothing
    End Sub

    Private Sub Cerrando() Handles Me.FormClosed
        bckActualizador.CancelAsync()
    End Sub



  
End Class