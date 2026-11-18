using System;
using System.IO;

namespace MegaDownloader.Stegano
{
    public class SteganoManager
    {


        public string CheckPassword(string Password)
        {
            if (string.IsNullOrEmpty(Password))
                Password = URLExtractor.ENCODE_PASSWORD;
            return Password;
        }

        // P1-3: 向导前置校验 —— 本地文件(含 file:// URI)必须真实存在,或必须是 http(s) 远程 URL。
        // 旧校验 IsValidUri 对 Windows 路径恒真(Uri 把 "C:\..." 解析成 scheme=file),
        // 不存在的路径一路放行,最后只得到 "未找到隐藏链接" 的误导提示。
        public static bool IsValidImageSource(string Input)
        {
            if (string.IsNullOrWhiteSpace(Input))
                return false;
            string s = Input.Trim();
            Uri uri = null;
            if (Uri.TryCreate(s, UriKind.Absolute, out uri))
            {
                if (uri.IsFile)
                    return File.Exists(uri.LocalPath);
                return (uri.Scheme ?? "") == (Uri.UriSchemeHttp ?? "") || (uri.Scheme ?? "") == (Uri.UriSchemeHttps ?? "");
            }
            return File.Exists(s);
        }

        // P1-3: 归一化输入来源 —— 本地路径(含 file:// URI 与 Windows 路径)返回
        // LocalPath,远程 http(s) 返回 URL,二者恰好其一非 Nothing。本地路径不存在时
        // 抛出明确的 ApplicationException("Input image does not exist")。
        // 旧代码让 WebRequest.Create 把 Windows 路径解析成 FileWebRequest 再 CType 成
        // HttpWebRequest(InvalidCastException 直接冒给用户),或被裸 Catch 压成
        // "未找到隐藏链接" 的误导提示。
        private static string NormalizeInput(string Input, ref string LocalPath)
        {
            LocalPath = null;
            string s = (Input ?? string.Empty).Trim();
            Uri uri = null;
            if (Uri.TryCreate(s, UriKind.Absolute, out uri))
            {
                if (uri.IsFile)
                {
                    LocalPath = uri.LocalPath;
                }
                else if ((uri.Scheme ?? "") == (Uri.UriSchemeHttp ?? "") || (uri.Scheme ?? "") == (Uri.UriSchemeHttps ?? ""))
                {
                    return uri.AbsoluteUri;
                }
                else
                {
                    // 其余 scheme 一律按本地路径处理,由下面的存在性检查给提示
                    LocalPath = s;
                }
            }
            else
            {
                LocalPath = s;
            }
            if (!File.Exists(LocalPath))
            {
                throw new ApplicationException(Language.GetText("Input image does not exist"));
            }
            return null;
        }

        // B3:远端图片下载上限 64MB + 30s 超时。WebClient.DownloadData 无超时无上限,
        // 误填/恶意的大文件 URL 直接整块进内存 OOM。超限抛 ApplicationException,
        // 向导已有友好分支接住弹窗(不记为崩溃);超时/404 等 WebException 原样上抛,记日志。
        private const long MaxRemoteImageBytes = 64L * 1024L * 1024L;

        private static byte[] DownloadImageBytes(string url)
        {
            // file:// URI 不走 HTTP 栈(FileWebRequest 不支持 Timeout 等属性,此前 WebClient 兼容):
            // 直接读本地文件,同样执行 64MB 上限。
            if (url.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            {
                byte[] localBytes = File.ReadAllBytes(new Uri(url).LocalPath);
                if (localBytes.Length > MaxRemoteImageBytes)
                {
                    throw new ApplicationException("Remote image is too large (over 64 MB).");
                }
                return localBytes;
            }
            // P1-3: 不再 CType 强转 HttpWebRequest —— Windows 路径会被 WebRequest.Create
            // 解析成 FileWebRequest,强转抛 InvalidCastException 原样冒给用户。
            // HTTP 属性只在真正是 HttpWebRequest 时设置。
            var req = System.Net.WebRequest.Create(url);
            System.Net.HttpWebRequest httpReq = req as System.Net.HttpWebRequest;
            if (httpReq is not null)
            {
                httpReq.Method = "GET";
                httpReq.Timeout = 30000;
                httpReq.ReadWriteTimeout = 60000;
                httpReq.AllowAutoRedirect = true;
                httpReq.MaximumAutomaticRedirections = 5;
            }
            // issue #6: transport failures (no internet, DNS, firewall/VPN/proxy,
            // timeout) become an actionable ApplicationException — the wizard's
            // ApplicationException branch shows it as a warning. API-level
            // failures (404 etc.) keep their original message.
            try
            {
                using (var resp = req.GetResponse())
                {
                    if (resp.ContentLength > MaxRemoteImageBytes)
                    {
                        throw new ApplicationException("Remote image is too large (over 64 MB).");
                    }
                    using (var src = resp.GetResponseStream())
                    {
                        using (var mem = new MemoryStream())
                        {
                            var buf = new byte[81920];
                            long total = 0L;
                            while (true)
                            {
                                int n = src.Read(buf, 0, buf.Length);
                                if (n <= 0)
                                    break;
                                total += n;
                                if (total > MaxRemoteImageBytes)
                                {
                                    throw new ApplicationException("Remote image is too large (over 64 MB).");
                                }
                                mem.Write(buf, 0, n);
                            }
                            return mem.ToArray();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (NetworkErrors.IsTransportError(ex))
                {
                    Log.WriteError("DownloadImageBytes: transport failure for [" + url + "] - " + Log.SafeException(ex));
                    throw new ApplicationException(NetworkErrors.DescribeTransportError(ex), ex);
                }
                throw;
            }
        }

        public bool CreateImage(string Text, string Input, string Output, int Quality, string Password)
        {

            // Cipher data
            Password = CheckPassword(Password);

            var AES = new Cryptography.AES();
            string encryptedText = AES.Encrypt(Text, Password);

            byte[] data = Convert.FromBase64String(encryptedText);

            Image img;
            MemoryStream backingStream = null;
            // 编码结果(在 Try 内生成,Try 外使用,故声明在块外)
            byte[] encodedBytes = null;

            // P1-3: 统一解析来源(本地路径 / file:// / 远程 URL),不存在的本地路径
            // 在此给出 "Input image does not exist",不再走到 DownloadImageBytes 的
            // WebRequest 强转路径。
            string localPath = null;
            string remoteUrl = NormalizeInput(Input, ref localPath);

            try
            {
                if (localPath is not null)
                {
                    // R-P4-1: a direct local path used to bypass the 64 MB cap that the
                    // URL / file:// branches enforce — a huge image went fully into
                    // memory (ReadAllBytes) and then through JPEG decode + re-encode,
                    // an easy OOM. Same limit, same shape as DownloadImageBytes.
                    var fi = new FileInfo(localPath);
                    if (fi.Length > MaxRemoteImageBytes)
                    {
                        throw new ApplicationException("Image file is too large (over 64 MB).");
                    }
                    // Load file into memory first so the source file is not locked and
                    // the backing stream stays open for the Image lifetime (FromStream requires it)
                    byte[] fileBytes = File.ReadAllBytes(localPath);
                    backingStream = new MemoryStream(fileBytes);
                    img = Image.FromStream(backingStream);
                }
                else
                {
                    // From URL
                    byte[] imgBytes = DownloadImageBytes(remoteUrl);
                    backingStream = new MemoryStream(imgBytes);
                    img = Image.FromStream(backingStream);
                }

                // 先在内存中编码与校验,全部通过后才落盘:
                // 1) 容量不足时不再留下写坏的 .jpg(此前先写盘后检查,用户目录残留截断文件)
                // 2) WriteAllBytes 截断覆盖,不会像 OpenWrite(OpenOrCreate)那样保留旧文件尾部字节
                using (img)
                using (var memOut = new MemoryStream())
                {
                    using (var jpg = new F5.James.JpegEncoder(img, memOut, default, Quality))
                    {

                        jpg.Compress(new MemoryStream(data), System.Text.Encoding.Unicode.GetBytes(Password));

                        double MaxSize = jpg.MaxSizeToEmbed * 0.8d; // For security we consider 80% of capacity

                        long fileSize = data.Length;
                        int K_Used = jpg.K_Used;

                        if (MaxSize < fileSize)
                        {
                            throw new ApplicationException(Language.GetText("Warning: image too small, maybe the data is corrupted"));
                        }

                    }
                    encodedBytes = memOut.ToArray();
                }
            }
            finally
            {
                if (backingStream is not null)
                    backingStream.Dispose();
            }

            // Check the encoded data (in memory)
            using (var mem = new MemoryStream())
            {
                using (var fsCheck = new MemoryStream(encodedBytes))
                {
                    using (var extractor = new F5.JpegExtract(mem, System.Text.Encoding.Unicode.GetBytes(Password)))
                    {
                        extractor.Extract(fsCheck);
                    }
                }

                data = mem.ToArray();

                try
                {
                    string CipheredText = AES.Decrypt(Convert.ToBase64String(data), Password);
                    if ((CipheredText ?? "") != (Text ?? ""))
                    {
                        throw new ApplicationException();
                    }
                }
                catch (Exception ex)
                {
                    throw new ApplicationException(Language.GetText("Warning: data verification failed. The output file has not been created. This may happen if the image is too small, try with a bigger image"));
                }

            }

            // 校验通过才写盘(截断覆盖)
            File.WriteAllBytes(Output, encodedBytes);

            return true;
        }

        public bool LoadImages(string Input, string Password, ref string HiddenText)
        {
            byte[] data;
            var AES = new Cryptography.AES();

            Password = CheckPassword(Password);

            // P1-3: 先解析来源(不存在的本地路径抛 "Input image does not exist"),
            // 取数据/提取阶段的 ApplicationException(如网络失败)原样上抛,
            // 只有"图片里提取不到隐藏数据"才压成 False —— 旧代码裸 Catch 把
            // 路径写错、网络失败、无隐藏数据混成同一句误导提示。
            string localPath = null;
            string remoteUrl = NormalizeInput(Input, ref localPath);

            // Retrieve data
            using (var mem = new MemoryStream())
            {
                try
                {
                    if (localPath is not null)
                    {
                        using (Stream st = File.OpenRead(localPath))
                        {
                            using (var extractor = new F5.JpegExtract(mem, System.Text.Encoding.Unicode.GetBytes(Password)))
                            {
                                extractor.Extract(st);
                            }
                        }
                    }
                    else
                    {
                        data = DownloadImageBytes(remoteUrl);
                        using (var st = new MemoryStream(data))
                        {
                            using (var extractor = new F5.JpegExtract(mem, System.Text.Encoding.Unicode.GetBytes(Password)))
                            {
                                extractor.Extract(st);
                            }
                        }
                    }

                    data = mem.ToArray();
                }

                catch (ApplicationException ex)
                {
                    // 可定位的输入问题(路径不存在/网络失败等),向导按警告显示具体原因
                    throw;
                }
                catch (Exception ex)
                {
                    return false;
                }
            }

            // Decrypt data
            try
            {
                string CipheredText = AES.Decrypt(Convert.ToBase64String(data), Password);
                HiddenText = CipheredText;
            }
            catch (Exception ex)
            {
                return false;
            }

            return true;
        }



    }


}