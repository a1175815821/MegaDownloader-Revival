''' <summary>
''' Web 远程控制对外契约。P0-3：把原来 Web 层对 Main 窗体的直接依赖
'''（Public Downloader As Main）收敛到这 9 个 ControlRemoto* 方法 + 1 个枚举。
''' 实现仍留在 Main 里，行为不变；Web/HttpModule 层只依赖本接口，不再引用 Main 类型。
''' 注意：Main.TipoEstadoAplicacion（Friend，定义在 Main.vb:215）保留作内部状态，
''' 其成员顺序与 DownloaderEstado 保持一致，可用 CType 直接映射。
''' </summary>
Public Enum DownloaderEstado
    Descargando = 0
    Pausa = 1
    Parado = 2
End Enum

Public Interface IDownloaderService
    Function ControlRemotoObtenerVelocidad() As Decimal?
    Function ControlRemotoObtenerDescargasActivas() As Integer?
    Function ControlRemotoObtenerDescargasCompletadas() As Integer?
    Function ControlRemotoObtenerDescargasErroneas() As Integer?
    Function ControlRemotoObtenerDescargasEnCola() As Integer?
    Function ControlRemotoObtenerEstado() As DownloaderEstado
    Sub ControlRemotoDescargar()
    Sub ControlRemotoParar()
    Function ControlRemotoAgregarLinks(ByVal Links As String, ByVal NombrePaquete As String, ByVal CrearDirectorio As Boolean) As String
End Interface
