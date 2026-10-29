Public NotInheritable Class SplashScreen

    Private Sub SplashScreen1_Load(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Load
        lblMsg.Text = Language.GetText("Loading please wait")
        lblThx.Text = InternalConfiguration.ObtenerNombreApp & InternalConfiguration.ObtenerValueFromInternalConfig("VERSION_MEGADOWNLOADER")
        ThemeManager.ApplyTheme(Me)
    End Sub

End Class
