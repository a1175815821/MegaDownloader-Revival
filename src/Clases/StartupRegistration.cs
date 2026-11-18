using System;
using System.Security;

namespace MegaDownloader
{
    /// <summary>
    /// Windows-only "run at login" integration. Kept in the WinForms shell: the
    /// cross-platform engine exposes <c>Configuracion.IniciarConWindows</c> as data,
    /// while the host decides how to apply it (registry on Windows, autostart
    /// entries elsewhere).
    /// </summary>
    public static class StartupRegistration
    {
        public static void RegisterInStartup(bool isChecked)
        {
            try
            {
                var registryKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                if (registryKey is null & isChecked)
                {
                    registryKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
                }
                if (registryKey is null)
                {
                    // Could not open/create the registry key; nothing else to do.
                    return;
                }
                try
                {
                    if (isChecked)
                    {
                        registryKey.SetValue("MegaDownloader", "\"" + System.Windows.Forms.Application.ExecutablePath + "\" -silent");
                    }
                    else
                    {
                        registryKey.DeleteValue("MegaDownloader", false);
                    }
                }
                finally
                {
                    registryKey.Close();
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Log.WriteError(@"SECURITY ERROR: Not enough privileges to access the registry (CU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run). Execute the application with administrator privileges (at least one time) in order to access the registry. Please note that if you move the application, you will have to execute it again with administrator privileges.");
            }
            catch (SecurityException ex)
            {
                Log.WriteError(@"SECURITY ERROR: Not enough privileges to access the registry (CU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run). Execute the application with administrator privileges (at least one time) in order to access the registry. Please note that if you move the application, you will have to execute it again with administrator privileges.");
            }
            catch (Exception ex)
            {
                Log.WriteError(@"Error accessing the registry (CU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run). Error: " + ex.ToString());
            }
        }
    }
}
