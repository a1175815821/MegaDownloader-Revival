' 注意:本类名为 Mutex,会遮蔽 System.Threading.Mutex。
' 在本文件之外若同时需要两者,可用 Imports 别名:
'     Imports ThreadingMutex = System.Threading.Mutex
' 此处保留类名以减少全项目改动的风险;字段类型已用完整命名 System.Threading.Mutex 避免歧义。
Public Class Mutex
    Public Shared NumeroConexionesMaxima As New System.Threading.Mutex()
    Public Shared GuardarConfig As New System.Threading.Mutex()
    Public Shared GuardarDownloadList As New System.Threading.Mutex()
    Public Shared ListaDescargas As New System.Threading.Mutex()
    Public Shared FicheroDownloader As New System.Threading.Mutex()
    Public Shared DeletingFiles As New System.Threading.Mutex()
    Public Shared MEGAUriParameters As New System.Threading.Mutex()
End Class
