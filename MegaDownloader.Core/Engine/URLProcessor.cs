using System;
using System.Collections.Generic;

namespace MegaDownloader
{
    public class URLProcessor
    {

        public class FileURL
        {

            public FileURL(string pURL, string pPath)
            {
                URL = pURL;
                Path = pPath;
            }

            public string URL;
            public string Path;
            // Decrypted folder metadata is reused by the batch preview, not as a
            // cached download URL. The download engine still verifies before transfer.
            public string DisplayName;
            public long? SizeBytes;
            internal string PreflightIdentity;
            internal string PreflightIdentityUrl;
            // Keeps identically named folders from separate input links in separate tree roots.
            internal string PreflightOrigin;

            /// <summary>
        /// 来源 MEGA 文件夹的真名(子文件夹链接为子文件夹名)。非文件夹链接为空。
        /// 文件夹展开时就已经解密出来了,此前在 MegaFolderHelper 里被丢掉,
        /// 导致无名文件夹包在主列表只能显示兜底的"封装 #N"。
        /// </summary>
            public string FolderName;

        }

        /// <summary>
    /// 批次里所有条目共有的文件夹真名:唯一且非空才返回,混合批次/多条不同文件夹返回空串
    /// (此时包名继续走兜底,不猜)。
    /// </summary>
        public static string GetCommonFolderName(List<FileURL> URLs)
        {
            string common = string.Empty;
            if (URLs is null)
                return common;
            foreach (FileURL fu in URLs)
            {
                if (fu is null || string.IsNullOrEmpty(fu.FolderName))
                    return string.Empty;
                if (string.IsNullOrEmpty(common))
                {
                    common = fu.FolderName;
                }
                else if (!string.Equals(common, fu.FolderName, StringComparison.Ordinal))
                {
                    return string.Empty;
                }
            }
            return common;
        }

        public static List<FileURL> ProcessURLs(List<string> URLs, ref Configuracion Config, IProgress<int> progress = null, System.Threading.CancellationToken ct = default)

        {

            // Convertimos los links de MegaFolder a links individuales
            // B1-⑤:per-URL 隔离——单个文件夹/ELC 失败(过期 ELC、服务端报错、断网)只跳过该条,
            // 不再 Throw 中断整批(此前同次粘贴的好链接被一条坏 ELC 团灭,须人肉二分)。
            // 全部失败时仍抛首个错误,保持单链调用方(CreateStreamingLink/错误弹窗)原有语义。
            var URLs3 = new List<FileURL>();
            Exception firstError = null;
            foreach (string URL in URLs)
            {
                if (ct.IsCancellationRequested)
                    ct.ThrowIfCancellationRequested();
                // 同一条 URL 内部分成功后又失败(如 ELC 内第 2 个文件夹已挂),回滚该条已加入项,
                // 做到"按条全有或全无",不留半截 ELC。
                int before = URLs3.Count;
                try
                {
                    if (URLExtractor.IsMegaFolder(URL))
                    {
                        string FolderID = URLExtractor.ExtraerFileID(URL);
                        string FolderKey = URLExtractor.ExtraerFileKey(URL);
                        string SubFolderID = URLExtractor.ExtraerSubFolderID(URL);
                        string SubFileID = URLExtractor.ExtraerSubFileID(URL);
                        foreach (var FileURL in MegaFolderHelper.RetrieveLinksFromFolder(FolderID, FolderKey, SubFolderID, SubFileID, progress, ct))
                            URLs3.Add(FileURL);
                    }
                    else if (URLExtractor.IsELC(URL))
                    {
                        Exception ELC_exc = null;
                        List<string> decoded = null;
                        try
                        {
                            decoded = ServerEncoderLinkHelper.ServerDecode(URL, ref Config, ref ELC_exc, ct);
                        }
                        catch (Exception ex)
                        {
                            ELC_exc = ex;
                        }
                        if (decoded is not null)
                        {
                            foreach (string FileURL in decoded)
                            {
                                if (URLExtractor.IsMegaFolder(FileURL))
                                {
                                    string FolderID = URLExtractor.ExtraerFileID(FileURL);
                                    string FolderKey = URLExtractor.ExtraerFileKey(FileURL);
                                    string SubFolderID = URLExtractor.ExtraerSubFolderID(FileURL);
                                    string SubFileID = URLExtractor.ExtraerSubFileID(FileURL);
                                    foreach (var FileURL2 in MegaFolderHelper.RetrieveLinksFromFolder(FolderID, FolderKey, SubFolderID, SubFileID, progress, ct))
                                    {
                                        var hidden = new FileURL(Fichero.HIDDEN_LINK + FileURL2.URL, FileURL2.Path);
                                        hidden.FolderName = FileURL2.FolderName;
                                        hidden.DisplayName = FileURL2.DisplayName;
                                        hidden.SizeBytes = FileURL2.SizeBytes;
                                        hidden.PreflightOrigin = FileURL;
                                        URLs3.Add(hidden);
                                    }
                                }
                                else
                                {
                                    URLs3.Add(new FileURL(Fichero.HIDDEN_LINK + FileURL, ""));
                                }
                            }
                        }
                        if (ELC_exc is not null)
                        {
                            throw ELC_exc;
                        }
                    }
                    else
                    {
                        URLs3.Add(new FileURL(URL, ""));
                    }
                    for (int index = before, loopTo = URLs3.Count - 1; index <= loopTo; index++)
                    {
                        if (URLs3[index].PreflightOrigin is null)
                            URLs3[index].PreflightOrigin = URL;
                    }
                }
                catch (OperationCanceledException ex)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    while (URLs3.Count > before)
                        URLs3.RemoveAt(URLs3.Count - 1);
                    Log.WriteWarning("ProcessURLs: skipping failed link, keeping the rest of the batch: " + Log.Redact(URL) + " - " + Log.SafeException(ex));
                    if (firstError is null)
                        firstError = ex;
                }
            }
            if (URLs3.Count == 0 && firstError is not null)
            {
                throw firstError;
            }
            return URLs3;
        }

    }
}