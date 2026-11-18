
namespace MegaDownloader
{
    // 注意:本类名为 Mutex,会遮蔽 System.Threading.Mutex。
    // 第三批第 11 项:全部互斥已由内核 Mutex 改为 SyncLock/Monitor 轻量锁。
    // 字段现为普通 Object 锁对象(仅进程内、Monitor 可重入、SyncLock 自带 Try/Finally)，
    // 类名保留以避免全项目改名风险;新增锁请直接用 New Object() 并以 SyncLock 使用。
    public class Mutex
    {
        public static object NumeroConexionesMaxima = new object();
        public static object GuardarConfig = new object();
        public static object GuardarDownloadList = new object();
        public static object ListaDescargas = new object();
        public static object FicheroDownloader = new object();
        public static object DeletingFiles = new object();
        public static object MEGAUriParameters = new object();
    }
}