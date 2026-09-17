' 注意:本类名为 Mutex,会遮蔽 System.Threading.Mutex。
' 第三批第 11 项:全部互斥已由内核 Mutex 改为 SyncLock/Monitor 轻量锁。
' 字段现为普通 Object 锁对象(仅进程内、Monitor 可重入、SyncLock 自带 Try/Finally)，
' 类名保留以避免全项目改名风险;新增锁请直接用 New Object() 并以 SyncLock 使用。
Public Class Mutex
    Public Shared NumeroConexionesMaxima As New Object()
    Public Shared GuardarConfig As New Object()
    Public Shared GuardarDownloadList As New Object()
    Public Shared ListaDescargas As New Object()
    Public Shared FicheroDownloader As New Object()
    Public Shared DeletingFiles As New Object()
    Public Shared MEGAUriParameters As New Object()
End Class
