Public Class Mutex
    Public Shared NumeroConexionesMaxima As New System.Threading.Mutex()
    Public Shared GuardarConfig As New System.Threading.Mutex()
    Public Shared GuardarDownloadList As New System.Threading.Mutex()
    Public Shared ListaDescargas As New System.Threading.Mutex()
    Public Shared FicheroDownloader As New System.Threading.Mutex()
    Public Shared DeletingFiles As New System.Threading.Mutex()
    Public Shared MEGAUriParameters As New System.Threading.Mutex()
End Class
