using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Permissions;
using System.Threading;

namespace MegaDownloader
{



    // http://madebits.com/netz/help2.php#rem

    /// <summary>
/// shared object for processes
/// </summary>
    [Serializable()]
    [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
    public class InstanceProxy : MarshalByRefObject
    {
        /// <summary>
    /// Gets a value indicating whether this instance is first instance.
    /// </summary>
    /// <value>
    /// 	<c>true</c> if this instance is first instance; otherwise, <c>false</c>.
    /// </value>
        public static bool IsFirstInstance
        {
            get
            {
                return m_IsFirstInstance;
            }
            set
            {
                m_IsFirstInstance = value;
            }
        }
        private static bool m_IsFirstInstance;

        /// <summary>
    /// Gets the command line args.
    /// </summary>
    /// <value>The command line args.</value>
        public static string[] CommandLineArgs
        {
            get
            {
                return m_CommandLineArgs;
            }
            set
            {
                m_CommandLineArgs = value;
            }
        }
        private static string[] m_CommandLineArgs;

    }

    /// <summary>
/// 
/// </summary>
    public class InstanceCallbackEventArgs : EventArgs
    {
        /// <summary>
    /// Initializes a new instance of the <see cref="InstanceCallbackEventArgs"/> class.
    /// </summary>
    /// <param name="isFirstInstance__1">if set to <c>true</c> [is first instance].</param>
    /// <param name="commandLineArgs__2">The command line args.</param>
        public InstanceCallbackEventArgs(bool isFirstInstance__1, string[] commandLineArgs__2)
        {
            IsFirstInstance = isFirstInstance__1;
            CommandLineArgs = commandLineArgs__2;
        }

        /// <summary>
    /// Gets a value indicating whether this instance is first instance.
    /// </summary>
    /// <value>
    /// 	<c>true</c> if this instance is first instance; otherwise, <c>false</c>.
    /// </value>
        public bool IsFirstInstance
        {
            get
            {
                return m_IsFirstInstance;
            }
            private set
            {
                m_IsFirstInstance = value;
            }
        }
        private bool m_IsFirstInstance;

        /// <summary>
    /// Gets or sets the command line args.
    /// </summary>
    /// <value>The command line args.</value>
        public string[] CommandLineArgs
        {
            get
            {
                return m_CommandLineArgs;
            }
            private set
            {
                m_CommandLineArgs = value;
            }
        }
        private string[] m_CommandLineArgs;
    }

    /// <summary>
/// Application Instance Manager
/// </summary>
    public sealed class ApplicationInstanceManager
    {
        private ApplicationInstanceManager()
        {
        }
        /// <summary>
    /// Creates the single instance.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="callback">The callback.</param>
    /// <returns></returns>
        public static bool CreateSingleInstance(string name, EventHandler<InstanceCallbackEventArgs> callback)
        {
            EventWaitHandle eventWaitHandle__1 = null;
            string eventName = string.Format("{0}-{1}", Environment.MachineName, name);

            InstanceProxy.IsFirstInstance = false;
            InstanceProxy.CommandLineArgs = Environment.GetCommandLineArgs();

            // N-P3-5: ATOMIC first-instance detection. The old flow was TOCTOU-racy:
            // "OpenExisting throws => I am first" has a window where two processes
            // starting in the same millisecond both miss the not-yet-created handle and
            // both run as first instance — two main windows, Buffer.dat double-written.
            // The createdNew out-parameter of the constructor is decided by the kernel:
            // the named object either already existed (second instance) or this call
            // created it (first instance), with no in-between state another process can
            // observe.
            bool createdNew = false;
            try
            {
                eventWaitHandle__1 = new EventWaitHandle(false, EventResetMode.AutoReset, eventName, out createdNew);
                InstanceProxy.IsFirstInstance = createdNew;
            }
            catch (Exception ex)
            {
                // Naming violation / permissions / handle storms: fall back to the old
                // best-effort probe so a cosmetic failure cannot block startup.
                Log.WriteWarning("SingleInstance: atomic handle creation failed (" + Log.SafeException(ex) + "); falling back to OpenExisting.");
                try
                {
                    eventWaitHandle__1 = EventWaitHandle.OpenExisting(eventName);
                    InstanceProxy.IsFirstInstance = false;
                }
                catch
                {
                    InstanceProxy.IsFirstInstance = true;
                }
            }

            if (InstanceProxy.IsFirstInstance)
            {
                // init handle — eventWaitHandle__1 was created atomically above and is
                // ours; register the wait, then close our reference (the registration
                // keeps its own). Guard Nothing for the degraded fallback path.
                if (eventWaitHandle__1 is not null)
                {
                    // register wait handle for this instance (process)
                    ThreadPool.RegisterWaitForSingleObject(eventWaitHandle__1, WaitOrTimerCallback, callback, Timeout.Infinite, false);
                    eventWaitHandle__1.Close();
                }
                else
                {
                    Log.WriteWarning("SingleInstance: no wait handle available; cross-instance signaling disabled for this session.");
                }

                // register shared type (used to pass data between processes)
                RegisterRemoteType(name);
            }
            else
            {
                // pass console arguments to shared object
                UpdateRemoteObject(name);

                // invoke (signal) wait handle on other process
                if (eventWaitHandle__1 is not null)
                {
                    eventWaitHandle__1.Set();
                }


                // kill current process
                Environment.Exit(0);
            }

            return InstanceProxy.IsFirstInstance;
        }

        /// <summary>
    /// Updates the remote object.
    /// </summary>
    /// <param name="uri">The remote URI.</param>
        private static void UpdateRemoteObject(string uri)
        {

            // Cannot remote with mpress, arrrgh!! :(
            string PathFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MegaDownloader/Internal");

            using (var gate = AcquireBufferMutex())
            {
                try
                {
                    if (!Directory.Exists(PathFile))
                    {
                        Directory.CreateDirectory(PathFile);
                    }
                    PathFile = Path.Combine(PathFile, "Buffer.dat");
                    using (var t = new StreamWriter(PathFile, true))
                    {
                        // One message per line so concurrent second-instance launches do not glue args together
                        t.WriteLine(string.Join("|", InstanceProxy.CommandLineArgs));
                    }
                }
                finally
                {
                    gate.ReleaseMutex();
                }
            }
        }

        private static System.Threading.Mutex AcquireBufferMutex(string mutexName = null)
        {
            if (string.IsNullOrEmpty(mutexName))
                mutexName = @"Local\MegaDownloader-Buffer-" + Environment.UserName;
            var gate = new System.Threading.Mutex(false, mutexName);
            try
            {
                try
                {
                    if (!gate.WaitOne(TimeSpan.FromSeconds(10d)))
                        throw new TimeoutException("Timed out waiting for the instance parameter buffer.");
                }
                catch (AbandonedMutexException ex)
                {
                    // The previous owner died; this thread now owns the mutex.
                }
                return gate;
            }
            catch
            {
                gate.Dispose();
                throw;
            }
        }


        // Dirty trick... but works without using remote marshalling :(
        internal static bool GetParameters()
        {
            string PathLog = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MegaDownloader/Internal/Buffer.dat");
            if (!File.Exists(PathLog))
                return false;
            var mainFrm = My.MyApplication.Main_Form;
            if (mainFrm is null || mainFrm.IsDisposed || !mainFrm.IsHandleCreated)
                return false;
            var rawLines = new List<string>();
            using (var gate = AcquireBufferMutex())
            {
                try
                {
                    if (!File.Exists(PathLog))
                        return false;
                    rawLines.AddRange(File.ReadAllLines(PathLog));
                    if (rawLines.Count == 0)
                        return false;
                    File.WriteAllText(PathLog, "");
                }
                finally
                {
                    gate.ReleaseMutex();
                }
            }
            var args = new List<string>();
            foreach (string line in rawLines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                string[] parts = line.Split('|');
                if (parts.Length > 1)
                    args.AddRange(parts.Skip(1));
            }
            if (args.Count == 0)
                return false;
            try
            {
                Action d = () =>
                    {
                        mainFrm.ProcessArgs(args.ToArray());
                        mainFrm.Activate();
                    };
                if (mainFrm.InvokeRequired)
                {
                    mainFrm.Invoke(d);
                }
                else
                {
                    d.Invoke();
                }
                return true;
            }
            catch (Exception ex)
            {
                Log.WriteError("GetParameters Invoke failed: " + Log.SafeException(ex));
                using (var gate = AcquireBufferMutex())
                {
                    try
                    {
                        File.AppendAllLines(PathLog, rawLines);
                    }
                    finally
                    {
                        gate.ReleaseMutex();
                    }
                }
                return false;
            }
        }

        /// <summary>
    /// Registers the remote type.
    /// </summary>
    /// <param name="uri">The URI.</param>
        private static void RegisterRemoteType(string uri)
        {
            // No-op: IPC Remoting 已下线,单实例改走 Buffer.dat 文件投递(GetParameters 轮询)。
        }


        /// <summary>
    /// Wait Or Timer Callback Handler
    /// </summary>
    /// <param name="state">The state.</param>
    /// <param name="timedOut">if set to <c>true</c> [timed out].</param>
        private static void WaitOrTimerCallback(object state, bool timedOut)
        {
            // cast to event handler
            EventHandler<InstanceCallbackEventArgs> callback = state as EventHandler<InstanceCallbackEventArgs>;
            if (callback is null)
            {
                return;
            }

            // invoke event handler on other process
            callback(state, new InstanceCallbackEventArgs(InstanceProxy.IsFirstInstance, InstanceProxy.CommandLineArgs));
        }
    }
}