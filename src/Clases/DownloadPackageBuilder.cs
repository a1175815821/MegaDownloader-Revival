using System;
using System.Collections.Generic;
using System.Threading;

namespace MegaDownloader
{

    /// <summary>Construct an unpublished package off the UI thread; create each directory once.</summary>
    public sealed class DownloadPackageBuilder
    {
        public static Paquete Build(List<URLProcessor.FileURL> URLs2, string packageName, string destination, bool createDirectory, bool extractArchives, string password, CancellationToken cancellation, IProgress<int> progress = null)
        {
            cancellation.ThrowIfCancellationRequested();

            // Creamos el paquete
            var oPaquete = new Paquete();
            oPaquete.ListaFicheros = new List<Fichero>();
            var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Action<string> ensureDirectory = path =>
                {
                    if (directories.Add(path))
                        System.IO.Directory.CreateDirectory(path);
                };
            try
            {
                oPaquete.Nombre = packageName;
                oPaquete.RutaLocal = destination;
                oPaquete.CrearSubdirectorio = createDirectory;
                oPaquete.PendienteNombrePaquete = string.IsNullOrEmpty(packageName);

                // 文件夹分享的真名(MegaFolderHelper 展开时就解密带出了,此前被丢掉)。
                // 用户填了包名不动;单条目也不动——那种包验名后按文件名命名并建目录是既有行为。
                string folderName = URLProcessor.GetCommonFolderName(URLs2);
                bool folderNamePending = oPaquete.PendienteNombrePaquete && !string.IsNullOrEmpty(folderName) && URLs2 is not null && URLs2.Count > 1;


                // 散装多文件(所有条目都无相对子目录)+勾选创建目录+包名空:自动用"新建包"
                // 建子目录,否则多文件无名包会直接散落下载目录(此前取首个文件名建目录即 issue #4)。
                // 文件夹分享(任一 Path 非空)保持现状,不额外套顶层;单文件走验名流程。
                if (oPaquete.PendienteNombrePaquete && oPaquete.CrearSubdirectorio && URLs2 is not null && URLs2.Count > 1)
                {
                    bool allFlat = true;
                    foreach (URLProcessor.FileURL fu in URLs2)
                    {
                        if (!string.IsNullOrEmpty(fu.Path))
                        {
                            allFlat = false;
                            break;
                        }
                    }
                    if (allFlat)
                    {
                        // 扁平文件夹分享也走这里:目录照建,只是名字从"新建包"换成真名(落盘形状不变)。
                        string autoName = folderName;
                        if (string.IsNullOrEmpty(autoName))
                        {
                            autoName = Language.GetText("New package");
                            if (string.IsNullOrEmpty(autoName))
                                autoName = "New package";
                        }
                        oPaquete.Nombre = autoName;
                        oPaquete.PendienteNombrePaquete = false;
                    }
                }

                // 带子目录的文件夹分享:真名同样先赋值再建目录,与 Main.ControlRemotoAgregarLinks 形状统一
                // (此前建目录之后才回写显示名、刻意不套顶层目录,同一文件夹链接两个入口落盘形状不一致:
                // Web 推送多套一层真名目录而 AddLinks 不套)。赋值必须在建目录之前,建目录分支才会用真名建顶层目录。
                if (folderNamePending && oPaquete.PendienteNombrePaquete)
                {
                    oPaquete.Nombre = folderName;
                    oPaquete.PendienteNombrePaquete = false;
                }

                // Creamos el directorio
                if (oPaquete.CrearSubdirectorio & !string.IsNullOrEmpty(oPaquete.Nombre))
                {
                    string packageSegment = PathGuard.SanitizeFileName(oPaquete.Nombre, "package");
                    oPaquete.RutaLocal = PathGuard.GetSafePathUnderRoot(oPaquete.RutaLocal, packageSegment, allowRoot: false);
                    ensureDirectory(oPaquete.RutaLocal);
                }

                Log.WriteWarning("Adding package in " + oPaquete.RutaLocal);

                oPaquete.set_SetDescargaExtraccionAutomatica(password, extractArchives);
                int completed = 0;
                foreach (var URL in URLs2)
                {
                    cancellation.ThrowIfCancellationRequested();

                    string ruta = PathGuard.GetSafePathUnderRoot(oPaquete.RutaLocal, URL.Path ?? string.Empty, allowRoot: true);
                    ensureDirectory(ruta);

                    string URLFile = URL.URL;
                    bool Visible = true;
                    if (!string.IsNullOrEmpty(URLFile) && URLFile.Contains(Fichero.HIDDEN_LINK))
                    {
                        Visible = false;
                        URLFile = URLFile.Replace(Fichero.HIDDEN_LINK, "");
                    }

                    var oFichero = new Fichero(URLFile);
                    oPaquete.ListaFicheros.Add(oFichero);
                    oFichero.LinkVisible = Visible;
                    oFichero.RutaLocal = ruta;
                    oFichero.RutaRelativa = URL.Path;
                    oFichero.NombreFichero = Visible ? URLFile : Fichero.HIDDEN_LINK_DESC;
                    if (!string.IsNullOrEmpty(URL.DisplayName))
                        oFichero.NombreFichero = URL.DisplayName;
                    if (URL.SizeBytes.HasValue)
                        oFichero.TamanoBytes = URL.SizeBytes.Value;
                    oFichero.FileID = Fichero.ExtraerFileID(URLFile);
                    oFichero.FileKey = Fichero.ExtraerFileKey(URLFile);
                    oFichero.set_SetDescargaExtraccionAutomatica(password, extractArchives);
                    Log.WriteWarning("Adding file to the new package: " + oFichero.FileID);
                    completed += 1;
                    // Publish counters, not one marshalled UI callback per imported file.
                    if (progress is not null && (completed % 64 == 0 || completed == URLs2.Count))
                        progress.Report(completed);
                }

                cancellation.ThrowIfCancellationRequested();
                return oPaquete;
            }
            catch
            {
                foreach (Fichero @file in oPaquete.ListaFicheros)
                    @file.Dispose();
                throw;
            }
        }
    }
}