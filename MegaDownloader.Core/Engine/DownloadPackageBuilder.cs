using System;
using System.Collections.Generic;
using System.Threading;

namespace MegaDownloader
{

    /// <summary>Construct an unpublished package off the UI thread; create each directory once.</summary>
    public sealed class DownloadPackageBuilder
    {
        /// <summary>
        /// Where the package and its files would land, computed without touching the
        /// disk so the preflight can show the real destination before anything is added.
        /// <see cref="PackageRoot"/> is null when the name is only known after the first
        /// file is verified (single unnamed link with "create subfolder").
        /// </summary>
        public sealed class DestinationPreview
        {
            public string PackageRoot;
            public string PackageName;
            public bool NamePending;
            public readonly List<string> SampleFiles = new List<string>();
        }

        public static DestinationPreview PreviewDestination(List<URLProcessor.FileURL> urls, string packageName, string destination, bool createDirectory)
        {
            var preview = new DestinationPreview { PackageRoot = destination };
            if (urls is null || urls.Count == 0)
                return preview;

            string folderName = URLProcessor.GetCommonFolderName(urls);
            bool namePending = string.IsNullOrEmpty(packageName);
            string name = packageName;

            bool folderNamePending = namePending && !string.IsNullOrEmpty(folderName) && urls.Count > 1;
            if (namePending && createDirectory && urls.Count > 1)
            {
                bool allFlat = true;
                foreach (URLProcessor.FileURL url in urls)
                {
                    if (!string.IsNullOrEmpty(url.Path))
                    {
                        allFlat = false;
                        break;
                    }
                }
                if (allFlat)
                {
                    name = string.IsNullOrEmpty(folderName) ? Language.GetText("New package") : folderName;
                    if (string.IsNullOrEmpty(name))
                        name = "New package";
                    namePending = false;
                }
            }

            if (folderNamePending && namePending)
            {
                name = folderName;
                namePending = false;
            }

            preview.NamePending = namePending && urls.Count == 1;
            preview.PackageName = name;

            if (createDirectory && !string.IsNullOrEmpty(name))
                preview.PackageRoot = PathGuard.GetSafePathUnderRoot(destination, PathGuard.SanitizeFileName(name, "package"), allowRoot: false);
            else if (namePending && urls.Count == 1)
                preview.PackageRoot = null;

            if (preview.PackageRoot is not null)
            {
                int samples = Math.Min(3, urls.Count);
                for (int index = 0; index < samples; index++)
                {
                    string relative = urls[index].Path ?? string.Empty;
                    string display = urls[index].DisplayName ?? string.Empty;
                    preview.SampleFiles.Add(PathGuard.GetSafePathUnderRoot(preview.PackageRoot, System.IO.Path.Combine(relative, display), allowRoot: true));
                }
            }

            return preview;
        }

        public static Paquete Build(List<URLProcessor.FileURL> URLs2, string packageName, string destination, bool createDirectory, bool extractArchives, string password, CancellationToken cancellation, IProgress<int> progress = null, string megaAccountEmail = "", string publicFolderId = "")
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

                    if (!string.IsNullOrEmpty(megaAccountEmail) &&
                        (!Visible || !string.IsNullOrEmpty(URL.Path) || !string.IsNullOrEmpty(URL.FolderName) ||
                         Fichero.GetMegaAccountLink(URLFile) is null))
                    {
                        throw new ArgumentException(Language.GetText("AddLinks_MegaAccountFilesOnly"));
                    }

                    var oFichero = new Fichero(URLFile) { MegaAccountEmail = megaAccountEmail };
                    oPaquete.ListaFicheros.Add(oFichero);
                    oFichero.PublicFolderId = publicFolderId;
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