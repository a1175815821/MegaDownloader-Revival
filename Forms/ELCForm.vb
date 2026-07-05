
Imports System.ComponentModel

Public Partial Class ELCForm
	Public Sub New()
		' The Me.InitializeComponent call is required for Windows Forms designer support.
		Me.InitializeComponent()
		
	End Sub
	
	
	Private ClosingForm As Boolean = False
	Public MainForm As Main
	Public WriteOnly Property MegaURLs As String
		Set(v As String)
			Me.txtMegaURLs.Text = v
		End Set
	End Property
	
	Private Sub ELCForm_Load(sender As Object, e As System.EventArgs) Handles Me.Load
		If MainForm IsNot Nothing then
			Translate()
			
			Me.btnSaveFile.Enabled = False 
			Me.btnExaminar.Enabled = False
			
			
			dim AccountH As New ELCAccountHelper(MainForm.Config)
			comboELCAccounts.DataSource = AccountH.GetAccounts.OrderBy(Function(c) c.Alias _
				).Select(Function(c As ELCAccountHelper.Account) New With { _
				.Url = c.URL, _
				.AccountName = c.Alias} _
				).ToList()
			
			comboELCAccounts.DisplayMember = "AccountName"
			comboELCAccounts.ValueMember = "Url"
			
			Dim DefAccount As ELCAccountHelper.Account = AccountH.GetDefaultAccount
			If DefAccount IsNot Nothing Then
				comboELCAccounts.SelectedValue = DefAccount.Url
			End If
			
			AccountH.Dispose()
			
			If comboELCAccounts.Items.Count = 0 Then
				MessageBox.Show(Language.GetText("No ELC accounts configured. Go to Configuration in order to add an ELC account"), _
					Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error) 
			End If
			
			bckELCGenerator = New BackgroundWorker
			bckELCGenerator.WorkerSupportsCancellation = True
			bckELCGenerator.RunWorkerAsync()
			
			
			' Centramos la pantalla
			' http://stackoverflow.com/questions/7892090/how-to-set-winform-start-position-at-top-right
			Dim scr = Screen.FromPoint(Me.Location)
			Me.Location = New Point(CInt((scr.WorkingArea.Right - Me.Width) / 2), CInt((scr.WorkingArea.Bottom - Me.Height) / 2))
		End if
	End Sub	
	
	Private Sub Cerrando() Handles Me.FormClosed
		ClosingForm = True
		bckELCGenerator.CancelAsync()
	End Sub
	
	Private Sub PantallaMsg_Shown(sender As Object, e As EventArgs) Handles Me.Shown	
		PonerFoco()
	End Sub
	
	Public Sub PonerFoco()
		' http://stackoverflow.com/questions/278237/keep-window-on-top-and-steal-focus-in-winforms
		Me.TopMost = True
		Me.TopMost = False
		Me.TopMost = True ' Para que funcione tenemos que quitar Topmost y volverlo a activar (??)
		Me.Activate()
	End Sub
	
	Private Sub Translate()
		Me.Text = Language.GetText("ELC generator") 
		Me.btnCerrar.Text = Language.GetText("Close")
		Me.gbMEGAUrl.Text = Language.GetText("MEGA Url") 
		Me.btnExaminar.Text = Language.GetText("Browse")
		Me.gbELCLink.Text = Language.GetText("ELC") 
		Me.lblExplanation.Text = Language.GetText("Paste your MEGA Url(s), select your ELC account, and click on Generate ELC")
		Me.lblExplanation2.Text = Language.GetText("This is your ELC link. You can also create an ELC file clicking on Save file") 
		Me.btnCrearELC.Text = Language.GetText("Generate ELC") 
        Me.btnSaveFile.Text = Language.GetText("Save file")
        Me.chkMultipleELCs.Text = Language.GetText("Multiple ELC")
	End Sub
	
	Private Sub btnCerrar_Click(sender As System.Object, e As System.EventArgs) Handles btnCerrar.Click
		Me.Close()
	End Sub
	
	Private Sub btnCrearELC_Click(sender As System.Object, e As System.EventArgs) Handles btnCrearELC.Click
        Try

            If comboELCAccounts.SelectedValue Is Nothing OrElse String.IsNullOrEmpty(CStr(comboELCAccounts.SelectedValue)) Then
                MessageBox.Show(Language.GetText("Please select a valid ELC account"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
                Exit Sub
            End If

            If BackgroundWorkerBusy Then
                MessageBox.Show(Language.GetText("There is an ELC being processed, please wait"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
                Exit Sub
            End If



            If chkMultipleELCs.Checked Then
                ' 1 ELC for each link
                Dim Mlist As New Generic.List(Of ServerEncoderLinkHelper.MegaLink)
                Dim linkIndex As Integer = 0
                Dim originalText As String = Me.txtMegaURLs.Text
                Dim modifiedText As New System.Text.StringBuilder
                For Each line In originalText.Split(New String() {Environment.NewLine}, StringSplitOptions.None)
                    Dim linkList As Generic.List(Of String) = URLExtractor.ExtraerURLs(line)
                    If linkList.Count > 0 Then
                        For Each Link As String In linkList
                            Dim l As New ServerEncoderLinkHelper.MegaLink
                            l.FileID = URLExtractor.ExtraerFileID(Link)
                            l.FileKey = URLExtractor.ExtraerFileKey(Link)
                            l.MegaFolder = URLExtractor.IsMegaFolder(Link)
                            Mlist.Add(l)
                            modifiedText.AppendLine("{" & linkIndex & "}")
                            linkIndex += 1
                        Next
                    Else
                        modifiedText.AppendLine(line)
                    End If

                Next

                Me.txtELCUrl.Text = ""
                Me.BackgroundWorkerBusy = True
                Me.btnCrearELC.Text = Language.GetText("Loading...")
                Me.btnCrearELC.Enabled = False
                Me.ELC_URL = CStr(comboELCAccounts.SelectedValue)

                Me.MegaLinkList = Mlist
                Me.TextTemplateToPrint = modifiedText.ToString
                Me.Action = "GenerateELC_Multiple"
            Else
                ' 1 ELC for all links

                ' Check data
                Dim linkList As Generic.List(Of String) = URLExtractor.ExtraerURLs(Me.txtMegaURLs.Text)
                If linkList.Count = 0 Then
                    MessageBox.Show(Language.GetText("Links not valid"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
                    Exit Sub
                End If

                Me.txtELCUrl.Text = ""
                Me.BackgroundWorkerBusy = True
                Me.btnCrearELC.Text = Language.GetText("Loading...")
                Me.btnCrearELC.Enabled = False
                Me.ELC_URL = CStr(comboELCAccounts.SelectedValue)

                ' Perform ELC generation
                Dim Mlist As New Generic.List(Of ServerEncoderLinkHelper.MegaLink)
                For Each Link As String In linkList
                    Dim l As New ServerEncoderLinkHelper.MegaLink
                    l.FileID = URLExtractor.ExtraerFileID(Link)
                    l.FileKey = URLExtractor.ExtraerFileKey(Link)
                    l.MegaFolder = URLExtractor.IsMegaFolder(Link)
                    Mlist.Add(l)
                Next
                Me.MegaLinkList = Mlist
                Me.TextTemplateToPrint = String.Empty
                Me.Action = "GenerateELC"
            End If

        


        Catch ex As Exception
            Log.WriteError("Error generating ELC: " & ex.ToString)
            MessageBox.Show(ex.Message, Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try

	End Sub
	
	Private Sub btnSaveFile_Click(sender As System.Object, e As System.EventArgs) Handles btnSaveFile.Click
		Try
			
			' Check data
			If String.IsNullOrEmpty(Me.ELCResult) Then
				MessageBox.Show(Language.GetText("Links not valid"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
				Exit sub
			End If
			
			If String.IsNullOrEmpty (Me.txtExaminar.Text) Then
				MessageBox.Show(Language.GetText("The ELC path is not valid"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
				Exit sub
			End If
			
			If BackgroundWorkerBusy Then
				MessageBox.Show(Language.GetText("There is an ELC being processed, please wait"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error) 
				Exit sub
			End If
			
			Me.ELCFilePath = Me.txtExaminar.Text 
			Me.BackgroundWorkerBusy = True
			Me.Action = "SaveELC"			
			
		Catch ex As Exception
			Log.WriteError("Error generating ELC: " & ex.ToString)
			MessageBox.Show(ex.Message, Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
		End Try
		
	End Sub
	
	Private Sub btnExaminar_Click(sender As System.Object, e As System.EventArgs) Handles btnExaminar.Click
		Dim Examinar As New  SaveFileDialog
		Examinar.CheckFileExists = false
		Examinar.DefaultExt = "elc"
		Examinar.Filter = Language.GetText("ELC file") & " (*.elc)|*.elc"
		
		If Examinar.ShowDialog = Windows.Forms.DialogResult.OK Then
			txtExaminar.Text = Examinar.FileName
		End If
		
		Examinar.Dispose()
	End Sub
	
	
	Private WithEvents bckELCGenerator As BackgroundWorker
	Private BackgroundWorkerBusy As Boolean = False
	Private Action As String = nothing
    Private MegaLinkList As Generic.List(Of ServerEncoderLinkHelper.MegaLink) = Nothing
    Private TextTemplateToPrint As String
	Private ELC_URL As String = nothing
	Private ELCResult As String = Nothing
	Private ErrorGeneratingELC As Exception = Nothing
	Private ELCFilePath As string
	
	Public Sub bckELCGenerator_DoWork(sender As Object, e As DoWorkEventArgs) Handles bckELCGenerator.DoWork
		Dim worker As BackgroundWorker = CType(sender, BackgroundWorker)
		
		While Not worker.CancellationPending
			
			System.Threading.Thread.Sleep(300)
			
			If worker.CancellationPending Then Exit While
			
			GenerateELC
			
		End While
		
		bckELCGenerator = Nothing
	End Sub
	
	
	Private Sub GenerateELC
		
        Select Case Action
            Case "GenerateELC_Multiple"

                Try
                    If String.IsNullOrEmpty(ELC_URL) Or MegaLinkList Is Nothing OrElse MegaLinkList.Count = 0 Then
                        ' Check input
                        Throw New ApplicationException("Invalid input data")
                    Else
                        ' Encode
                        Dim temp As String = Me.TextTemplateToPrint
                        Dim index As Integer = 0
                        For Each link In MegaLinkList
                            Dim l As New Generic.List(Of ServerEncoderLinkHelper.MegaLink)
                            l.Add(link)
                            temp = temp.Replace("{" & index & "}", "mega://elc?" & ServerEncoderLinkHelper.ServerEncode(ELC_URL, l, Me.MainForm.Config))
                            index += 1
                        Next
                        ELCResult = temp
                    End If
                Catch e As Exception
                    ErrorGeneratingELC = e
                    ELCResult = String.Empty
                Finally
                    BackgroundWorkerBusy = False
                End Try
                ActualizarDatos()

            Case "GenerateELC"
                Try
                    If String.IsNullOrEmpty(ELC_URL) Or MegaLinkList Is Nothing OrElse MegaLinkList.Count = 0 Then
                        ' Check input
                        Throw New ApplicationException("Invalid input data")
                    Else
                        ' Encode
                        ELCResult = "mega://elc?" & ServerEncoderLinkHelper.ServerEncode(ELC_URL, MegaLinkList, Me.MainForm.Config)
                    End If
                Catch e As Exception
                    ErrorGeneratingELC = e
                    ELCResult = String.Empty
                Finally
                    BackgroundWorkerBusy = False
                End Try
                ActualizarDatos()

            Case "SaveELC"
                Try
                    If String.IsNullOrEmpty(ELCFilePath) Or String.IsNullOrEmpty(ELCResult) Then
                        ' Check input
                        Throw New ApplicationException("Invalid input data")
                    Else
                        Using t As New System.IO.StreamWriter(ELCFilePath, False)
                            t.Write(ELCResult)
                        End Using
                    End If
                Catch e As Exception
                    ErrorGeneratingELC = e
                    ELCResult = String.Empty
                Finally
                    BackgroundWorkerBusy = False
                End Try
                ActualizarDatos()

            Case Else
                ' Sleep
        End Select
		
	End Sub
	
	
	
	Delegate Sub ActualizarDatosCallback()
	Private Sub ActualizarDatos()
		If ClosingForm Then Exit Sub
		If Me.txtMegaURLs.InvokeRequired Then
			Try
				Dim d As New ActualizarDatosCallback(AddressOf ActualizarDatos)
				Me.Invoke(d, New Object() {})
			Catch ex As Exception
				' A veces da error al cerrar la ventana
			End Try
			
		Else
			
			If ClosingForm Then Exit Sub
			
			Select Case Action
					
                Case "GenerateELC", "GenerateELC_Multiple"
                    Action = String.Empty
                    Me.btnCrearELC.Text = Language.GetText("Generate ELC")
                    Me.btnCrearELC.Enabled = True
                    If ErrorGeneratingELC IsNot Nothing Then
                        MessageBox.Show(Language.GetText("Error generating ELC") & ": " & _
                            ErrorGeneratingELC.Message, Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
                        ErrorGeneratingELC = Nothing
                    Else
                        Me.txtELCUrl.Text = ELCResult
                        Me.btnExaminar.Enabled = True
                        Me.btnSaveFile.Enabled = True
                    End If
					
				Case "SaveELC"
					Action = String.Empty
					If ErrorGeneratingELC IsNot Nothing Then
						MessageBox.Show(Language.GetText("Error generating ELC") & ": " & _
							ErrorGeneratingELC.Message, Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error) 
					Else
						MessageBox.Show(Language.GetText("ELC created successfully"), Language.GetText("Note"), MessageBoxButtons.OK, MessageBoxIcon.Information)
					End If
			End Select
			
		End If
	End Sub
	
    Private Sub txtELCUrl_KeyDown(sender As Object, e As KeyEventArgs) Handles txtELCUrl.KeyDown
        If e.Control AndAlso (e.KeyCode = Keys.A Or e.KeyCode = Keys.E) Then
            If sender IsNot Nothing Then
                DirectCast(sender, TextBox).SelectAll()
            End If
            e.Handled = True
        End If
    End Sub

    Private Sub txtMegaURLs_KeyDown(sender As Object, e As KeyEventArgs) Handles txtMegaURLs.KeyDown
        If e.Control AndAlso (e.KeyCode = Keys.A Or e.KeyCode = Keys.E) Then
            If sender IsNot Nothing Then
                DirectCast(sender, TextBox).SelectAll()
            End If
            e.Handled = True
        End If
    End Sub
End Class
