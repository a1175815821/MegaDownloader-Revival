Imports System.ComponentModel

Public Class EncodeLinksForm
	
	Private ClosingForm As Boolean = False
	Private WithEvents bckActualizador As BackgroundWorker
	Public MainForm As Main
	
	
	Private Sub EncodeLinksForm_Load(sender As Object, e As System.EventArgs) Handles Me.Load
		txtEncodedLinks.Multiline = True
		txtEncodedLinks.Text = ""
		
		Translate()
		
		bckActualizador = New BackgroundWorker
		bckActualizador.WorkerSupportsCancellation = True
		bckActualizador.RunWorkerAsync()
		
		' Centramos la pantalla
		' http://stackoverflow.com/questions/7892090/how-to-set-winform-start-position-at-top-right
		Dim scr = Screen.FromPoint(Me.Location)
		Me.Location = New Point(CInt((scr.WorkingArea.Right - Me.Width) / 2), CInt((scr.WorkingArea.Bottom - Me.Height) / 2))
	End Sub
	
	Private Sub Translate()
		Me.Text = Language.GetText("Encode Url")
		Me.Button1.Text = Language.GetText("Close")
		Me.GroupBox1.Text = Language.GetText("MEGA Url")
		Me.GroupBox2.Text = Language.GetText("Encoded Url")
		Me.lblExplanation.Text = Language.GetText("Paste your MEGA Url(s) and the encoded Url(s) will appear under the Encoded Url box.")
	End Sub
	
	Private Sub Button1_Click(sender As System.Object, e As System.EventArgs) Handles Button1.Click
		Me.Close()
	End Sub
	
	
	Private Sub Cerrando() Handles Me.FormClosed
		ClosingForm = True
		bckActualizador.CancelAsync()
	End Sub
	
	
	
	Private TextHash As Integer = 0
	Public Sub bckActualizador_DoWork(sender As Object, e As DoWorkEventArgs) Handles bckActualizador.DoWork
		Dim worker As BackgroundWorker = CType(sender, BackgroundWorker)
		
		While Not worker.CancellationPending
			
			System.Threading.Thread.Sleep(300)
			
			If worker.CancellationPending Then Exit While
			
			ActualizarDatos()
			
		End While
		
		bckActualizador = Nothing
	End Sub
	
	
	
	Delegate Sub ActualizarDatosCallback()
	Private Sub ActualizarDatos()
		If ClosingForm Then Exit Sub
		If Me.txtOriginalLinks.InvokeRequired Then
			Try
				Dim d As New ActualizarDatosCallback(AddressOf ActualizarDatos)
				Me.Invoke(d, New Object() {})
			Catch ex As Exception
				' A veces da error al cerrar la ventana
			End Try
			
		Else
			
			If ClosingForm Then Exit Sub
			Dim text As String = txtOriginalLinks.Text
			Dim NewTextHash As Integer = text.GetHashCode
			
			If NewTextHash <> TextHash Then
				TextHash = NewTextHash
				
				Dim l As Generic.List(Of String) = URLExtractor.ExtraerSoloURLsOficiales(text)
				For Each Url As String In l
					Dim FileID As String = URLExtractor.ExtraerFileID(Url)
					Dim FileKey As String = URLExtractor.ExtraerFileKey(Url)
					If Not String.IsNullOrEmpty(FileID) Then
                        Dim NewUrl As String = URLExtractor.GenerateEncodedURILink(FileID, FileKey, URLExtractor.IsMegaFolder(Url), False)
						text = text.Replace(Url, NewUrl)
					End If
				Next
				
				Me.txtEncodedLinks.Text = text
				
			End If
			
		End If
	End Sub
	
	
End Class