using System;
using System.Diagnostics;
using System.Reflection;

namespace MegaDownloader.My
{

    // The following events are available for MyApplication:
    // 
    // Startup: Raised when the application starts, before the startup form is created.
    // Shutdown: Raised after all application forms are closed.  This event is not raised if the application terminates abnormally.
    // UnhandledException: Raised if the application encounters an unhandled exception.
    // StartupNextInstance: Raised when launching a single-instance application and the application is already active. 
    // NetworkAvailabilityChanged: Raised when the network connection is connected or disconnected.
    internal partial class MyApplication
    {


        private static Main _mainFrm;
        internal static readonly Stopwatch StartupClock = new Stopwatch();
        internal static Main Main_Form
        {
            set
            {
                _mainFrm = value;
            }
            get
            {
                return _mainFrm;
            }
        }

        private void MyApplication_Startup(object sender, Microsoft.VisualBasic.ApplicationServices.StartupEventArgs e)
        {

            StartupClock.Start();

            // The engine reports a handful of user-facing messages (e.g. a file that
            // could not be deleted). It lives in MegaDownloader.Core and must not
            // reference WinForms, so the shell installs the actual MessageBox here.
            MegaDownloader.UserNotification.Sink = (kind, title, message) =>
            {
                var form = _mainFrm;
                Action show = () => System.Windows.Forms.MessageBox.Show(
                    form,
                    message,
                    title,
                    System.Windows.Forms.MessageBoxButtons.OK,
                    kind == MegaDownloader.UserNotificationKind.Error
                        ? System.Windows.Forms.MessageBoxIcon.Error
                        : System.Windows.Forms.MessageBoxIcon.Information);
                if (form is not null && !form.IsDisposed && form.InvokeRequired)
                {
                    form.BeginInvoke(show);
                }
                else
                {
                    show();
                }
            };

            if (!ApplicationInstanceManager.CreateSingleInstance("MegaDownloader", SingleInstanceCallback))
            {
                return;
            }


            MyProject.Application.MinimumSplashScreenDisplayTime = 600;
            AppDomain.CurrentDomain.AssemblyResolve += LoadDLLFromStream;
            // 后台线程未处理异常至少留下日志,否则进程直接消失且无任何线索
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        }

        private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                MegaDownloader.Log.WriteError("AppDomain unhandled exception (isTerminating=" + e.IsTerminating.ToString() + "): " + (e.ExceptionObject is null ? "<null>" : e.ExceptionObject.ToString()));
            }
            catch
            {
                // 日志系统本身不可用时无处可写,只能放弃
            }
        }

        private void MyApplication_UnhandledException(object sender, Microsoft.VisualBasic.ApplicationServices.UnhandledExceptionEventArgs e)
        {
            try
            {
                MegaDownloader.Log.WriteError("Unhandled exception: " + e.Exception.ToString());
            }
            catch
            {
            }
            // 不直接终止进程:错误已写日志,让用户有机会保存状态后手动退出
            e.ExitApplication = false;
        }

        private static void SingleInstanceCallback(object sender, InstanceCallbackEventArgs args)
        {
            if (args is null || _mainFrm is null || _mainFrm.IsDisposed)
            {
                return;
            }

            try
            {
                Action<bool> d = (x) =>
                {
                    // P2-6: do NOT replay InstanceProxy.CommandLineArgs here.
                    // That field holds the FIRST instance's own startup arguments
                    // (assigned once at process start), so every second launch
                    // re-added the ORIGINAL links as duplicate downloads. The real
                    // arguments of the second instance are delivered through the
                    // Buffer.dat file and picked up by the 430ms GetParameters poll
                    // (Main.vb), which calls ProcessArgs — this callback only needs
                    // to focus the already-running window.
                    _mainFrm.ActivateExistingInstance();
                };
                _mainFrm.Invoke(d, true);
            }
            catch (InvalidOperationException ex)
            {
            }
            // Form may have been closed between the IsDisposed check and Invoke; ignore.
            catch (Exception ex)
            {
                MegaDownloader.Log.WriteError("Error invoking SingleInstanceCallback: " + ex.ToString());
            }
        }

        private Assembly LoadDLLFromStream(object sender, ResolveEventArgs args)

        {

            string resourceName = new AssemblyName(args.Name).Name + ".dll";


            resourceName = ResourceHelper.GetResourceName(resourceName);

            if (!string.IsNullOrEmpty(resourceName))
            {
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                {

                    if (stream is null)
                    {
                        throw new ApplicationException("DLL resource stream could not be opened: " + resourceName);
                    }

                    var assemblyData = new byte[(int)(stream.Length - 1L) + 1];
                    stream.Read(assemblyData, 0, assemblyData.Length);
                    return Assembly.Load(assemblyData);

                }
            }
            else
            {
                throw new ApplicationException("DLL could not be loaded: " + args.Name);
            }


        }


    }


}