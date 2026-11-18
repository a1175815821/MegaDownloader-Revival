
namespace MegaDownloader
{
    public interface IDescarga
    {


        int DescargaPrioridad();

        string DescargaNombre();

        long DescargaTamanoBytes();

        decimal DescargaPorcentaje();

        decimal DescargaVelocidadKBs();

        Estado DescargaEstado();

        bool DescargaExtraccionAutomatica();

        string DescargaExtraccionPassword();

        string DescargaTiempoEstimadoDescarga();

    }

    public enum Estado
    {
        Verificando,
        CreandoLocal,
        EnCola,
        Descargando,
        Pausado,
        Completado,
        ComprobandoMD5,
        Descomprimiendo,
        Erroneo,
        // Append new states: Fichero persists Estado by its symbolic name, but keeping
        // the original numeric values stable also protects external integrations.
        EsperandoVolumenes
    }
}