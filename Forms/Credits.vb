Public Class Credits


    Private Sub Credits_Load(sender As Object, e As System.EventArgs) Handles Me.Load
        Translate()
    End Sub

    Private Sub Translate()
        Me.Text = Language.GetText("About")
        Me.Button1.Text = Language.GetText("Close")
        Me.lblTitle.Text = InternalConfiguration.ObtenerNombreApp & InternalConfiguration.ObtenerValueFromInternalConfig("VERSION_MEGADOWNLOADER")
        Me.lblAutor.Text = Language.GetText("Created and designed by %NAME")
        If Not Me.lblAutor.Text.Contains("%NAME") Then Me.lblAutor.Text = "Created and designed by %NAME"
        Me.lblAutor.Text = Me.lblAutor.Text.Replace("%NAME", "Andres Soliño [andres_age]")

        Me.lblEmail.Text = "andres.age@gmail.com"
        Me.lblGraciasA.Text = Language.GetText("Thanks to") & ":"
        With Me.lblListaColaboradores
            .Text = ""
            .Text &= "* " & Language.GetText("%NAME for helping with MEGA criptographic system").Replace("%NAME", "Bernardo Vadell") & Global.Microsoft.VisualBasic.ChrW(13) & Global.Microsoft.VisualBasic.ChrW(10)
            .Text &= "* " & Language.GetText("All users that have collaborated") & Global.Microsoft.VisualBasic.ChrW(13) & Global.Microsoft.VisualBasic.ChrW(10)
            If Not String.IsNullOrEmpty(Language.GetText("Translator credits")) Then
                .Text &= "* " & Language.GetText("Translator credits")
            End If
        End With
        Me.Label1.Text = Language.GetText("Thanks for using this app")
        Me.Label2.Text = Language.GetText("Legal text")

    End Sub

    Private Sub Button1_Click(sender As System.Object, e As System.EventArgs) Handles Button1.Click
        Me.Close()
    End Sub

    Private Sub lblTitle_LinkClicked(sender As System.Object, e As System.Windows.Forms.LinkLabelLinkClickedEventArgs) Handles lblTitle.LinkClicked
        System.Diagnostics.Process.Start(InternalConfiguration.ObtenerValueFromInternalConfig("CREDITS_LINK"))
    End Sub
End Class