
namespace MegaDownloader
{
    /// <summary>
/// P2-11 UI:左导航状态分组判定。纯谓词工具类:计数、Roots 子集、ChildrenGetter 包裹
/// 三处共用同一规则(包自身命中,或任一子文件命中;树只有包->文件两层),所见即所数。
/// 注意:最初用 OLV 自带 UseFiltering/ModelFilter 实现,但在本工程 VirtualMode
/// TreeListView 上会导致启动期静默退出(反射仅证明属性存在,不证明可用,已二分验证),
/// 故改用 Roots 子集 + ChildrenGetter 包裹,本类不再实现 IModelFilter。
/// 过滤在列表重建时生效(切换分组/增删/启停等);导航计数一侧永远实时。
/// </summary>
    public class DownloadEstadoFilter
    {

        public enum NavScope
        {
            All = 0,
            Downloading = 1,
            Waiting = 2,
            Failed = 3,
            Completed = 4
        }

        private DownloadEstadoFilter()
        {
        }

        /// <summary>对象是否属于分组(含包的子文件穿透)。计数与过滤共用,保证一致。</summary>
        public static bool MatchesScope(object modelObject, NavScope scope)
        {
            if (scope == NavScope.All)
                return true;
            IDescarga d = modelObject as IDescarga;
            if (d is null)
                return true;
            if (ScopeOf(d.DescargaEstado()) == scope)
                return true;
            Paquete p = modelObject as Paquete;
            if (p is not null && p.ListaFicheros is not null)
            {
                foreach (Fichero f in p.ListaFicheros)
                {
                    if (ScopeOf(f.DescargaEstado()) == scope)
                        return true;
                }
            }
            return false;
        }

        public static NavScope ScopeOf(Estado st)
        {
            switch (st)
            {
                case Estado.Descargando:
                case Estado.Verificando:
                case Estado.Descomprimiendo:
                case Estado.EsperandoVolumenes:
                case Estado.CreandoLocal:
                case Estado.ComprobandoMD5:
                    return NavScope.Downloading;
                case Estado.EnCola:
                case Estado.Pausado:
                    return NavScope.Waiting;
                case Estado.Erroneo:
                    return NavScope.Failed;
                case Estado.Completado:
                    return NavScope.Completed;
                default:
                    // 未知状态宁可落在已完成,也不在各分组里消失。
                    return NavScope.Completed;
            }
        }

    }
}