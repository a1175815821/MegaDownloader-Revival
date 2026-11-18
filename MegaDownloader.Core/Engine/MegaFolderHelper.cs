using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualBasic;

namespace MegaDownloader
{
    public class MegaFolderHelper
    {

        public class FileListResponse
        {
            public string e;
            public object ok;
            public object u;
            public string sn;
            public List<FileNode> f;
        }

        public class FileNode
        {
            public string h;
            public string p;
            public string u;
            public int t;
            public string a;
            public string k;
            public long s;
            public long ts;
        }

        /// <summary>
    /// Retrieves the download links of the files inside a shared MEGA folder.
    /// </summary>
    /// <param name="FolderID">Root folder handle (from the link).</param>
    /// <param name="FolderKey">Root folder key (from the link).</param>
    /// <param name="SubFolderID">Optional subfolder handle from a modern link
    /// (mega.nz/folder/&lt;id&gt;#&lt;key&gt;/folder/&lt;subID&gt;). When set, only the files
    /// inside that subfolder are returned, with paths relative to the subfolder.</param>
    /// <param name="SubFileID">Optional file handle from a modern link
    /// (mega.nz/folder/&lt;id&gt;#&lt;key&gt;/file/&lt;fileID&gt;). When set, only that file
    /// is returned.</param>
        public static List<URLProcessor.FileURL> RetrieveLinksFromFolder(string FolderID, string FolderKey, string SubFolderID = "", string SubFileID = "", IProgress<int> progress = null, System.Threading.CancellationToken ct = default)



        {
            string jsonRQ;
            Conexion.Respuesta res;

            bool FromENCLink = FolderID.StartsWith(URLExtractor.FOLDERENCODEDPREFIX, StringComparison.OrdinalIgnoreCase) | FolderID.StartsWith(URLExtractor.FOLDERENCODEDPREFIX2, StringComparison.OrdinalIgnoreCase);

            URLExtractor.CheckFileIDAndFileKey(ref FolderID, ref FolderKey);

            jsonRQ = "[{\"a\":\"f\",\"c\":1,\"r\":1}]";
            res = Conexion.SendJSON(Conexion.Get_MEGA_API_Url("") + "&n=" + FolderID, jsonRQ, ct: ct);

            if (res.Excepcion is not null)
            {
                System.Net.WebException wex = res.Excepcion as System.Net.WebException;
                if (wex is not null && MegaQuotaManager.IsQuotaWebException(wex))
                {
                    var hint = MegaQuotaManager.TryGetRetryAfterSeconds(wex);
                    MegaQuotaManager.ReportQuota(hint.HasValue ? hint.Value : 0L);
                    throw new MegaQuotaExceededException("MEGA transfer quota exceeded (HTTP 509) while reading folder.");
                }
                // Network failures (TCP connect timeout, DNS pollution/hijack, firewall/VPN/proxy)
                // happen before any MEGA API logic runs. Keep the technical detail in the log
                // and show the user an actionable message instead of a raw stack trace (issue #6).
                Log.WriteError("RetrieveLinksFromFolder: network failure contacting MEGA API (g.api.mega.co.nz) - " + Log.SafeException(res.Excepcion) + (res.Excepcion.InnerException is not null ? " | Inner: " + Log.SafeException(res.Excepcion.InnerException) : string.Empty));
                if (IsFolderNetworkError(res.Excepcion))
                {
                    throw new ApplicationException(GetFolderNetworkErrorMessage(res.Excepcion));
                }
                throw new ApplicationException("Error getting file list from shared folder - " + Log.SafeException(res.Excepcion));
            }

            // MEGA reports API errors as a bracketed array ("[-9]", "[-17]"), so the brackets
            // must come off before IsNumeric (IsNumeric("[-9]") is False, which made this whole
            // branch dead) and before GetErrorFromMegaResponse (its Select Case matches the bare
            // code). res.Mensaje is left untouched — the deserialisation below trims its own.
            string body = (res.Mensaje ?? string.Empty).Trim();
            if (body.StartsWith("["))
                body = body.Substring(1);
            if (body.EndsWith("]"))
                body = body.Substring(0, body.Length - 1);
            body = body.Trim();

            if (Information.IsNumeric(body))
            {
                if (Conexion.IsQuotaErrorText(body))
                {
                    MegaQuotaManager.ReportQuota();
                    throw new MegaQuotaExceededException("MEGA transfer quota exceeded (EOVERQUOTA) while reading folder.");
                }
                throw MEGA_ErrorHandler.GetErrorFromMegaResponse(body, "getting file list from shared folder");
            }

            FileListResponse FileList;
            try
            {
                FileList = (FileListResponse)Newtonsoft.Json.JsonConvert.DeserializeObject(res.Mensaje.Trim('[', ']'), typeof(FileListResponse));

            }
            catch (Exception ex)
            {
                Log.WriteError("RetrieveLinksFromFolder: invalid folder response: " + Log.SafeException(ex));
                throw new ApplicationException(GetFolderInvalidResponseMessage(Log.SafeException(ex)));
            }
            if (FileList is null || FileList.f is null)
            {
                if (FileList is not null && Conexion.IsQuotaErrorText(FileList.e))
                {
                    MegaQuotaManager.ReportQuota();
                    throw new MegaQuotaExceededException("MEGA transfer quota exceeded (EOVERQUOTA) while reading folder.");
                }
                Log.WriteError("RetrieveLinksFromFolder: empty file list in server response.");
                throw new ApplicationException(GetFolderInvalidResponseMessage(string.Empty));
            }

            var Results = new List<URLProcessor.FileURL>();
            // B1-②:静默跳过计数。单个节点解密失败不再无声消失:部分跳过记 Warning(含示例 handle),
            // 全部失败抛错(调用方按失败处理,不再把"一个文件都没读出来"当成功)。
            int totalFiles = 0;
            int skippedFiles = 0;
            int skippedFolders = 0;
            var firstSkipped = new List<string>();
            // 取消点:解析是大文件夹逐节点解密,CPU 密集,取消必须停掉循环不再耗 API/CPU。
            if (ct.IsCancellationRequested)
                ct.ThrowIfCancellationRequested();

            // 找到文件夹本身的内部 handle (root)
            // root 节点的特征: t=1, 且 fileN.h 出现在自己的 k 字段的 handle 部分
            // MEGA API 中 k 字段格式可能为 "handle1:key1/handle2:key2/..." (多 share key)
            string root = "";
            foreach (FileNode fileN in FileList.f)
            {
                if (fileN.t == 1 && !string.IsNullOrEmpty(fileN.k) && fileN.k.Contains(":"))
                {
                    // 检查 k 字段中是否有 handle 等于 fileN.h 的对 (即文件自己的 handle)
                    string keyForSelf = ExtractKeyFromK(fileN.k, fileN.h);
                    if (!string.IsNullOrEmpty(keyForSelf))
                    {
                        root = fileN.h;
                        break;
                    }
                }
            }

            // 原始父级关系表 (handle -> parent handle), 用于子文件夹过滤的祖先链判断。
            // 与 htFolderEstructure 不同, 这里不经解密——中间某层文件夹即使解密失败,
            // 祖先链依然完整, 其下文件的归属判断不受影响。
            var parentMap = new Dictionary<string, string>();
            foreach (FileNode fileN in FileList.f)
            {
                if (fileN.t == 1 && !parentMap.ContainsKey(fileN.h))
                {
                    parentMap[fileN.h] = (fileN.h ?? "") == (root ?? "") ? "" : fileN.p;
                }
            }

            if (!string.IsNullOrEmpty(SubFolderID))
            {
                if (!parentMap.ContainsKey(SubFolderID))
                {
                    throw new ApplicationException("The subfolder specified in the link was not found in the shared folder (it may have been deleted).");
                }
            }
            if (!string.IsNullOrEmpty(SubFileID))
            {
                bool found = false;
                foreach (FileNode fileN in FileList.f)
                {
                    if (fileN.t == 0 && (fileN.h ?? "") == (SubFileID ?? ""))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    throw new ApplicationException("The file specified in the link was not found in the shared folder (it may have been deleted).");
                }
            }


            // Get folder structure
            var htFolderEstructure = new Dictionary<string, KeyValuePair<string, string>>();
            int folderCounter = 0;
            if (progress is not null)
                progress.Report(0);
            foreach (FileNode fileN in FileList.f)
            {
                ct.ThrowIfCancellationRequested();
                if (fileN.t == 1)
                {
                    folderCounter += 1;
                    if (folderCounter % 100 == 0)
                    {
                        if (progress is not null)
                            progress.Report(folderCounter);
                    }
                    string FileID = fileN.h;

                    // 从 k 字段提取与 root handle 匹配的 key (用于用 FolderKey 解密)
                    // 如果没有 root,则回退到第一个 key
                    string FileKey = ExtractKeyFromK(fileN.k, root);
                    if (string.IsNullOrEmpty(FileKey))
                    {
                        NoteSkipped(fileN.h, true, ref skippedFiles, ref skippedFolders, firstSkipped);
                        continue;
                    }

                    try
                    {
                        FileKey = Criptografia.a32_to_base64(Criptografia.decrypt_key(Criptografia.base64_to_a32(FileKey), Criptografia.base64_to_a32(FolderKey)));
                    }
                    catch (Exception exCrypt)
                    {
                        // 解密失败,跳过此节点 (可能是 k 字段格式不兼容)
                        NoteSkipped(fileN.h, true, ref skippedFiles, ref skippedFolders, firstSkipped);
                        continue;
                    }

                    string FolderName = Conexion.ExtractNameFromFileInfo(PreSharedKeyManager.DecryptFileInfo(fileN.a, FileKey));

                    if (string.IsNullOrEmpty(FolderName))
                    {
                        NoteSkipped(fileN.h, true, ref skippedFiles, ref skippedFolders, firstSkipped);
                        continue;
                    }

                    try
                    {
                        FolderName = PathGuard.RequireSafePathSegment(FolderName, "remote folder name");
                    }
                    catch
                    {
                        FolderName = PathGuard.SanitizeFileName(FolderName, "folder");
                    }

                    // 父级 handle: 如果 fileN.h == root,说明这是根文件夹本身,没有父级
                    string parent = (fileN.h ?? "") == (root ?? "") ? "" : fileN.p;
                    htFolderEstructure.Add(FileID, new KeyValuePair<string, string>(FolderName, parent));

                }
            }
            var htFolders = new Dictionary<string, string>();
            FillFolderStructure(root, htFolders, htFolderEstructure);

            // 子文件夹链接: 路径表重定基为以子文件夹为根 (handle 级 BFS, 不依赖字符串前缀去除,
            // 避免不同层级同名文件夹造成的路径误判)。解密失败的文件夹不在 htFolderEstructure 中,
            // FillFolderStructure 会把它当作"真孤儿"安置为根级目录(保留其名),其子孙在后续轮次
            // 正常归位(N-P3-4);只有循环阻塞的节点才退化为根路径。
            var pathMap = htFolders;
            if (!string.IsNullOrEmpty(SubFolderID))
            {
                pathMap = BuildSubfolderPaths(SubFolderID, htFolderEstructure);
            }

            // 链接指向的那个文件夹的真名(子文件夹链接=子文件夹名,整文件夹链接=共享根名)。
            // 名字在上面 t=1 循环里已经解密进 htFolderEstructure,此前只用于拼路径、根本身的名字被
            // FillFolderStructure 置 "" 后丢弃,无名文件夹包因此在主列表只能显示兜底的"封装 #N"。
            // 单文件链接(/file/<ID>)不带:那种包按文件名命名是既有行为,不能被文件夹名抢掉。
            // 整文件夹链接不用上面的 root 变量:那是 ExtractKeyFromK 的"找不到就回退第一个 key"
            // 语义,等价于"第一个 t=1 节点",响应里万一不含共享根自己就会指向某个子文件夹。
            // 这里改用严格判据(k 里存在 handle 等于自身 h 的键值对=共享根的特征),认不出就不命名。
            string folderDisplayName = string.Empty;
            if (string.IsNullOrEmpty(SubFileID))
            {
                string nameHandle = string.IsNullOrEmpty(SubFolderID) ? FindSelfKeyedFolderHandle(FileList.f) : SubFolderID;

                if (!string.IsNullOrEmpty(nameHandle) && htFolderEstructure.ContainsKey(nameHandle))
                {
                    folderDisplayName = htFolderEstructure[nameHandle].Key;
                }
            }


            // Get files
            int fileCounter = 0;
            foreach (FileNode fileN in FileList.f)
            {

                ct.ThrowIfCancellationRequested();
                if (fileN.t == 0)
                {
                    fileCounter += 1;
                    if (fileCounter % 100 == 0)
                    {
                        if (progress is not null)
                            progress.Report(folderCounter + fileCounter);
                    }
                    // 单文件链接: 只保留链接指向的那个文件
                    if (!string.IsNullOrEmpty(SubFileID) && !((fileN.h ?? "") == (SubFileID ?? "")))
                    {
                        continue;
                    }

                    // 子文件夹链接: 只保留父级祖先链包含目标子文件夹的文件
                    // (作用域过滤,非失败,不计入 totalFiles/skipped)
                    if (!string.IsNullOrEmpty(SubFolderID) && !IsUnderFolder(fileN.p, SubFolderID, parentMap))
                    {
                        continue;
                    }
                    totalFiles += 1;

                    // 从 k 字段提取与 root handle 匹配的 key
                    string FileKey = ExtractKeyFromK(fileN.k, root);
                    if (string.IsNullOrEmpty(FileKey))
                    {
                        NoteSkipped(fileN.h, false, ref skippedFiles, ref skippedFolders, firstSkipped);
                        continue;
                    }

                    string path = string.Empty;
                    if (pathMap.ContainsKey(fileN.p))
                    {
                        path = pathMap[fileN.p];
                    }

                    try
                    {
                        FileKey = Criptografia.a32_to_base64(Criptografia.decrypt_key(Criptografia.base64_to_a32(FileKey), Criptografia.base64_to_a32(FolderKey)));
                    }
                    catch (Exception exCrypt)
                    {
                        // 解密失败,跳过此文件 (可能无法用 FolderKey 解密)
                        NoteSkipped(fileN.h, false, ref skippedFiles, ref skippedFolders, firstSkipped);
                        continue;
                    }

                    string FileInfoDec = Conexion.ExtractNameFromFileInfo(PreSharedKeyManager.DecryptFileInfo(fileN.a, FileKey));
                    try
                    {
                        if (!string.IsNullOrEmpty(FileInfoDec))
                        {


                            // ' Ya tenemos el FileID y el FileKey
                            // Dim FileID As String = "megafolder?" & FolderID & "?" & fileN.h
                            // Dim NuevoLink As String = URLExtractor.GenerateEncodedURILink(FileID, FileKey, False, False)
                            // Results.Add(New URLProcessor.FileURL(NuevoLink, path))

                            // 25/1/15 Formato #N!
                            string NuevoLink;
                            if (FromENCLink)
                            {
                                NuevoLink = URLExtractor.GenerateEncodedURILink("N?" + fileN.h, FileKey + "=###n=" + FolderID, false, false);
                            }
                            else
                            {
                                NuevoLink = string.Format("https://mega.nz/#N!{0}!{1}=###n={2}", fileN.h, FileKey, FolderID);
                            }

                            var resolved = new URLProcessor.FileURL(NuevoLink, path);
                            resolved.FolderName = folderDisplayName;
                            resolved.DisplayName = PathGuard.SanitizeFileName(FileInfoDec, "file");
                            resolved.SizeBytes = Math.Max(0L, fileN.s);
                            Results.Add(resolved);
                        }

                        else
                        {
                            NoteSkipped(fileN.h, false, ref skippedFiles, ref skippedFolders, firstSkipped);
                            continue;
                        }
                    }

                    catch (Exception exc) // Detect error reading file from folder
                    {
                        throw;
                    }

                }

            }

            // B1-②:全灭抛错(调用方按失败处理,不再"零文件报成功");部分跳过 Warning 留痕。
            if (Results.Count == 0 && totalFiles > 0)
            {
                throw new ApplicationException("Could not read any of the " + totalFiles + " file(s) in the shared folder (keys could not be decrypted or names could not be read). The folder may have been deleted or the link key is wrong.");
            }
            else if (skippedFiles + skippedFolders > 0)
            {
                Log.WriteWarning("RetrieveLinksFromFolder: skipped " + skippedFiles + " file(s) and " + skippedFolders + " folder(s) that could not be decrypted (e.g. " + string.Join(",", firstSkipped.ToArray()) + "). The download will be incomplete.");
            }

            if (progress is not null)
                progress.Report(folderCounter + fileCounter);
            return Results;
        }

        // 判断 startFolder 的祖先链 (含自身) 是否包含 targetFolder。
        // parentMap: handle -> parent handle (root 的父级为 "")。带环路防护。
        private static bool IsUnderFolder(string startFolder, string targetFolder, Dictionary<string, string> parentMap)
        {
            if (string.IsNullOrEmpty(startFolder) || string.IsNullOrEmpty(targetFolder))
                return false;

            string current = startFolder;
            int guard = 0;
            while (!string.IsNullOrEmpty(current) && guard < 10000)
            {
                if (string.Equals(current, targetFolder, StringComparison.Ordinal))
                    return true;
                if (!parentMap.ContainsKey(current))
                    return false;
                current = parentMap[current];
                guard += 1;
            }
            return false;
        }

        // 构建以 subFolderID 为根的相对路径表 (handle -> 相对子文件夹的路径)。
        // 只遍历子文件夹的后代; 子文件夹本身路径为 ""。
        private static Dictionary<string, string> BuildSubfolderPaths(string subFolderID, Dictionary<string, KeyValuePair<string, string>> folderEstructure)
        {
            var childrenMap = new Dictionary<string, List<string>>();
            foreach (KeyValuePair<string, KeyValuePair<string, string>> entry in folderEstructure)
            {
                string parentHandle = entry.Value.Value;
                if (string.IsNullOrEmpty(parentHandle))
                    continue;
                List<string> children = null;
                if (!childrenMap.TryGetValue(parentHandle, out children))
                {
                    children = new List<string>();
                    childrenMap[parentHandle] = children;
                }
                children.Add(entry.Key);
            }

            var paths = new Dictionary<string, string>();
            var pending = new Queue<string>();
            paths[subFolderID] = "";
            pending.Enqueue(subFolderID);

            while (pending.Count > 0)
            {
                string current = pending.Dequeue();
                List<string> children = null;
                if (!childrenMap.TryGetValue(current, out children))
                    continue;
                foreach (string child in children)
                {
                    if (paths.ContainsKey(child))
                        continue;
                    paths[child] = PathGuard.CombineSafeRelativePath(paths[current], folderEstructure[child].Key);
                    pending.Enqueue(child);
                }
            }

            return paths;
        }

        // B1-②:跳过记账 helper(只记前 5 个 handle,大文件夹不刷屏;key 材料绝不进日志)。
        private static void NoteSkipped(string handle, bool isFolder, ref int skippedFiles, ref int skippedFolders, List<string> firstSkipped)

        {
            if (isFolder)
            {
                skippedFolders += 1;
            }
            else
            {
                skippedFiles += 1;
            }
            if (firstSkipped.Count < 5 && !string.IsNullOrEmpty(handle))
            {
                firstSkipped.Add(handle);
            }
        }

        // 从 MEGA API 的 k 字段中提取指定 handle 对应的 key
        // k 字段格式: "handle1:key1" 或 "handle1:key1/handle2:key2/handle3:key3"
        // 当文件被多个用户分享时,会出现多个 handle:key 对
        private static string ExtractKeyFromK(string kField, string handle)
        {
            if (string.IsNullOrEmpty(kField) || !kField.Contains(":"))
                return "";

            // 如果 k 字段包含 / (多个 handle:key 对)
            if (kField.Contains("/"))
            {
                string[] parts = kField.Split('/');

                // 优先匹配指定的 handle
                if (!string.IsNullOrEmpty(handle))
                {
                    foreach (string part in parts)
                    {
                        int colonIdx = part.IndexOf(':');
                        if (colonIdx > 0)
                        {
                            string h = part.Substring(0, colonIdx);
                            if ((h ?? "") == (handle ?? ""))
                            {
                                return part.Substring(colonIdx + 1);
                            }
                        }
                    }
                }

                // 回退:返回第一个有效 key
                foreach (string part in parts)
                {
                    int colonIdx = part.IndexOf(':');
                    if (colonIdx > 0)
                    {
                        string key = part.Substring(colonIdx + 1);
                        if (!string.IsNullOrEmpty(key))
                            return key;
                    }
                }
                return "";
            }
            else
            {
                // 单个 handle:key 对
                return kField.Substring(kField.IndexOf(':') + 1);
            }
        }

        // 共享根的特征: t=1, 且自己的 k 字段里存在 handle 等于自身 h 的键值对
        // (子节点的 k 里放的是共享根的 handle, 不是自己的)。认不出返回空串, 调用方按"无名"处理。
        // 与 ExtractKeyFromK 的区别: 这里不做"找不到就回退第一个 key", 回退会把第一个 t=1
        // 子文件夹误认成根, 从而拿子文件夹名当整个分享的包名。
        private static string FindSelfKeyedFolderHandle(List<FileNode> nodes)
        {
            if (nodes is null)
                return string.Empty;
            foreach (FileNode fileN in nodes)
            {
                if (fileN.t != 1 || string.IsNullOrEmpty(fileN.k) || !fileN.k.Contains(":"))
                    continue;
                foreach (string part in fileN.k.Split('/'))
                {
                    int colonIdx = part.IndexOf(':');
                    if (colonIdx > 0 && part.Length > colonIdx + 1 && (part.Substring(0, colonIdx) ?? "") == (fileN.h ?? ""))
                    {
                        return fileN.h;
                    }
                }
            }
            return string.Empty;
        }

        // Issue #6: distinguish transport failures (TCP/DNS/firewall/VPN/proxy, including
        // DNS pollution that resolves the MEGA API host to a non-MEGA IP and then times out
        // on 443) from real API errors. Transport failures get an actionable message;
        // everything else keeps a short one-line technical hint instead of a full stack trace.
        // Classification lives in NetworkErrors (shared with the ELC / stegano surfaces).
        private static bool IsFolderNetworkError(Exception ex)
        {
            return NetworkErrors.IsTransportError(ex);
        }

        private static string GetFolderNetworkErrorMessage(Exception ex)
        {
            string template = Language.GetText("Folder_NetworkError");
            if (string.IsNullOrEmpty(template) || template == "Folder_NetworkError")
            {
                template = "Cannot connect to MEGA to read the shared folder (network error)." + Constants.vbCrLf + "Check your internet connection, firewall/VPN/proxy, and try again." + " If mega.nz is restricted in your region, configure a proxy in Configuration or use a VPN," + " and make sure https://mega.nz opens in your browser." + Constants.vbCrLf + "Technical detail: %DETAIL%";
            }
            // Outer WebException message alone ("unable to connect") hides the useful part
            // (inner SocketException carries the failed IP:port, e.g. a polluted non-MEGA IP).
            string detail = Log.SafeException(ex);
            if (ex is not null && ex.InnerException is not null)
            {
                detail += " (" + Log.SafeException(ex.InnerException) + ")";
            }
            return template.Replace("%DETAIL%", detail);
        }

        private static string GetFolderInvalidResponseMessage(string detail)
        {
            string template = Language.GetText("Folder_InvalidResponse");
            if (string.IsNullOrEmpty(template) || template == "Folder_InvalidResponse")
            {
                template = "MEGA returned an unexpected response while reading the shared folder." + " The link may have been deleted, is private, or the key is wrong." + " Please verify the link opens in your browser." + Constants.vbCrLf + "Technical detail: %DETAIL%";
            }
            if (string.IsNullOrEmpty(detail))
                detail = "invalid server response";
            return template.Replace("%DETAIL%", detail);
        }

        private static void FillFolderStructure(string id, Dictionary<string, string> final, Dictionary<string, KeyValuePair<string, string>> unprocessed)
        {
            // Two-phase safe build: index first so parent order does not matter.
            if (final.Count == 0 && unprocessed.Count > 0)
            {
                var remaining = new HashSet<string>(unprocessed.Keys);
                int guard = 0;
                while (remaining.Count > 0 && guard < unprocessed.Count + 2)
                {
                    guard += 1;
                    bool progressed = false;
                    foreach (string nodeId in remaining.ToList())
                    {
                        string parent = unprocessed[nodeId].Value;
                        if (string.Equals(nodeId, id, StringComparison.Ordinal))
                        {
                            // 共享根自己:文件直挂其下时 path 为 "",不占名。
                            final[nodeId] = "";
                            remaining.Remove(nodeId);
                            progressed = true;
                        }
                        else if (string.IsNullOrEmpty(parent) || (parent ?? "") == (id ?? ""))
                        {
                            // 根的直接子文件夹:路径应为自己的名字(此前误置 "",
                            // 导致首层目录名丢失、文件被拍平到根)。
                            final[nodeId] = PathGuard.SanitizeFileName(unprocessed[nodeId].Key, "folder");
                            remaining.Remove(nodeId);
                            progressed = true;
                        }
                        else if (final.ContainsKey(parent))
                        {
                            final[nodeId] = PathGuard.CombineSafeRelativePath(final[parent], unprocessed[nodeId].Key);
                            remaining.Remove(nodeId);
                            progressed = true;
                        }
                    }
                    if (!progressed)
                    {
                        // N-P3-4: no node could be placed this round. A node whose parent is
                        // NOT in the structure at all (that folder failed to decrypt or was
                        // skipped) is a true orphan: seat it at the relative root under its
                        // sanitized name and CONTINUE the loop — its children resolve
                        // normally on later rounds via the ContainsKey(parent) branch above.
                        // The old code placed EVERY remaining node (orphans and waiters
                        // alike) and exited, so every descendant of an orphan was stranded:
                        // with no pathMap entry its files fell back to the package root and
                        // the whole subtree's structure was lost.
                        foreach (var nodeId in remaining.ToList())
                        {
                            string orphanParent = unprocessed[nodeId].Value;
                            if (string.IsNullOrEmpty(orphanParent) || !unprocessed.ContainsKey(orphanParent))
                            {
                                final[nodeId] = PathGuard.SanitizeFileName(unprocessed[nodeId].Key, "folder");
                                remaining.Remove(nodeId);
                                progressed = true;
                            }
                        }
                        if (!progressed)
                        {
                            // Every waiter is blocked behind another waiter: cyclic or
                            // otherwise unplaceable structure. Give up as before.
                            Log.WriteWarning("MegaFolderHelper: " + remaining.Count + " folder node(s) could not be placed in the structure; their files will land at the folder root.");
                            break;
                        }
                    }
                }
                return;
            }

            if (unprocessed.ContainsKey(id) && !final.ContainsKey(id))
            {
                string parent = unprocessed[id].Value;
                if (!string.IsNullOrEmpty(parent) && final.ContainsKey(parent))
                {
                    final.Add(id, PathGuard.CombineSafeRelativePath(final[parent], unprocessed[id].Key));
                }
                else
                {
                    final.Add(id, "");
                }
                foreach (var son in from n in unprocessed.Keys
                                    where unprocessed[n].Value == id
                                    select n)
                    FillFolderStructure(son, final, unprocessed);
            }
        }


    }
}