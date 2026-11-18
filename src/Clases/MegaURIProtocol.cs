using System;
using Microsoft.VisualBasic.CompilerServices;
using Microsoft.Win32;

namespace MegaDownloader
{

    public class MegaURIProtocol
    {

        // http://www.openwinforms.com/run_exe_from_javascript.html


        public const string UrlProtocol = "mega";
        public static Exception RegisterUrlProtocol()
        {
            RegistryKey rootKey = null;
            RegistryKey rKey = null;
            try
            {
                // P1-1:写 HKCU\Software\Classes 而不是 HKCR。HKCR 的写入落到 HKLM 需要
                // 提权,而 manifest 是 asInvoker——标准用户(以及 -silent 开机自启)下必然抛
                // UnauthorizedAccessException,导致每次启动弹框、静默模式更会以无属主模态框
                // 阻塞 Main_Load。HKCU 每用户注册无需提权,且优先级高于 HKLM 旧项,
                // 便携版挪位置后也不再需要管理员重注册。
                rootKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes");
                rKey = rootKey.OpenSubKey(UrlProtocol, false);

                if (rKey is null)
                {
                    rKey = rootKey.CreateSubKey(UrlProtocol);
                    rKey.SetValue("", "URL: mega Protocol");
                    rKey.SetValue("URL Protocol", "");

                    // Dim rKeyIcon As RegistryKey = rKey.CreateSubKey("DefaultIcon")
                    // rKeyIcon.SetValue("", """" & iconFilePath & """")
                    if (rKey is not null)
                        rKey.Close();

                    rKey = rootKey.CreateSubKey(UrlProtocol + @"\shell\open\command");
                    rKey.SetValue("", "\"" + Application.ExecutablePath + "\" %1");
                    Log.WriteInfo(@"MEGA URI protocol added to registry (HKCU\Software\Classes).");
                }
                else
                {
                    rKey.Close();
                    string val = "\"" + Application.ExecutablePath + "\" %1";

                    var subKey = rootKey.OpenSubKey(UrlProtocol + @"\shell\open\command", false);
                    if (subKey is null)
                    {
                        subKey = rootKey.CreateSubKey(UrlProtocol + @"\shell\open\command");
                    }
                    if (subKey is null || subKey.GetValue("") is null || (Conversions.ToString(subKey.GetValue("")) ?? "") != (val ?? ""))
                    {
                        if (subKey is not null)
                            subKey.Close();
                        subKey = rootKey.OpenSubKey(UrlProtocol + @"\shell\open\command", true);
                        if (subKey is not null)
                            subKey.SetValue("", val);
                    }
                    if (subKey is not null)
                        subKey.Close();
                }
                return null;
            }
            catch (UnauthorizedAccessException ex)
            {
                Log.WriteError(@"SECURITY ERROR: could not write HKCU\Software\Classes for the MEGA URI protocol. Check group policy / registry restrictions. Error: " + Log.SafeException(ex));
                return ex;
            }

            catch (System.Security.SecurityException ex)
            {
                Log.WriteError(@"SECURITY ERROR: could not write HKCU\Software\Classes for the MEGA URI protocol. Check group policy / registry restrictions. Error: " + Log.SafeException(ex));
                return ex;
            }

            catch (Exception ex)
            {
                Log.WriteError("Error accessing the registry for registering MEGA URI protocol. Error: " + ex.ToString());
                return ex;
            }
            finally
            {
                if (rKey is not null)
                    rKey.Close();
                if (rootKey is not null)
                    rootKey.Close();
            }
        }

    }
}