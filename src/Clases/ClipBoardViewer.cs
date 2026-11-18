using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Windows.Forms;

namespace MegaDownloader
{

    // Requires unmanaged code
    // <Assembly: SecurityPermission(SecurityAction.RequestMinimum, UnmanagedCode:=True)> 

    // Requires all clipboard access
    // <Assembly: UIPermission(SecurityAction.RequestMinimum, Clipboard:=UIPermissionClipboard.AllClipboard)> 


    /// <summary>
/// Provides a way to receive notifications of changes to the 
/// content of the clipboard using the Windows API.  
/// </summary>
/// <remarks>
/// To be a part of the change notification you need to register a 
/// window in the Clipboard Viewer Chain.  This provides
/// notification messages to the window whenever the 
/// clipboard changes, and also messages associated with managing
/// the chain.  This class manages the detail of keeping the
/// chain intact and ensuring that the application is removed
/// from the chain at the right point.
/// 
/// Use the <see cref="System.Windows.Forms.NativeWindow.AssignHandle"/> method 
/// to connect this class up to a form to begin receiving notifications.
/// Note that a Form can change its <see cref="NativeWindow.Handle"/>   
/// under certain circumstances; for example, if you change the 
/// <see cref="System.Windows.Forms.Form.ShowInTaskbar"/> property the Framework
/// must re-create the form from scratch since Windows ignores changes to 
/// that style unless they are in place when the Window is created.
/// (As a consequence, you should try to set as many high-level Window 
/// style details as possible prior to creating the Window, or at least,
/// prior to making it visible).  The OnHandleChange
/// method of this class is called when this happens.  You need to
/// re-assign the handle again whenever this occurs.  Unfortunately
/// OnHandleChange is not a useful event in which to
/// do anything since the window handle at that point contains neither
/// a valid old window or a valid new one.  Therefore you need to
/// make the call to re-assign at a point when you know the window
/// is valid, for example, after styles have been changed, or 
/// by overriding <see cref="System.Windows.Forms.Form.OnHandleCreated"/>.
/// </remarks>
    public class ClipboardViewer : NativeWindow, IDisposable
    {

        #region Unmanaged Code

        private const int WM_CHANGECBCHAIN = 0x30D;
        private const int WM_CLIPBOARDUPDATE = 0x31D;
        private const int WM_DESTROY = 0x2;
        private const int WM_DRAWCLIPBOARD = 0x308;

        private const string CS_CLIPBOARD_VIEWER_IGNORE = "Clipboard Viewer Ignore";

        [DllImport("user32")]
        private static extern IntPtr SetClipboardViewer(IntPtr hWnd);

        [DllImport("user32")]
        private static extern int ChangeClipboardChain(IntPtr hWnd, IntPtr hWndNext);

        [DllImport("user32")]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32")]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int wMsg, IntPtr wParam, IntPtr lParam);

        #endregion

        #region Member Variables

        private bool _firstUse;


        /// <summary>
    /// Whether this class has been disposed or not.
    /// </summary>
        private bool _disposed;

        /// <summary>
    /// The Window clipboard change notification was installed for.
    /// </summary>
        private IntPtr _installedHandle = IntPtr.Zero;

        /// <summary>
    /// The next handle in the clipboard viewer chain when the 
    /// clipboard notification is installed, otherwise <see cref="IntPtr.Zero"/>
    /// </summary>
        private IntPtr _nextViewerHandle = IntPtr.Zero;


        #endregion

        #region Events

        /// <summary>
    /// Notifies of a change to the clipboard's content.
    /// </summary>
        public event EventHandler<HandledEventArgs> ClipboardChanged;

        #endregion

        /// <summary>
    /// Initializes a new instance of the ClipboardViewer class
    /// </summary>
        public ClipboardViewer()
        {

        }

        /// <summary>
    /// Clear clipboard if is sucesfully processed (accepted)
    /// </summary>
        public bool ClearOnReceive;

        #region IDisposable Members

        /// <summary>
    /// Uninstalls clipboard event notifications if necessary
    /// during dispose of this object.
    /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Uninstall();
            _disposed = true;
        }

        #endregion


        /// <summary>
    /// Provides default WndProc processing and responds to
    /// clipboard change notifications.
    /// </summary>
    /// <param name="m"></param>
        [SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.UnmanagedCode)]
        protected override void WndProc(ref Message m)
        {
            // if the message is a clipboard change notification
            switch (m.Msg)
            {
                case WM_CHANGECBCHAIN:
                    // If the next window is closing, repair the chain
                    if (m.WParam == _nextViewerHandle)
                    {
                        _nextViewerHandle = m.LParam;
                    }
                    // Otherwise, pass the message to the next link
                    else if (!_nextViewerHandle.Equals(IntPtr.Zero))
                    {
                        SendMessage(_nextViewerHandle, m.Msg, m.WParam, m.LParam);
                    }
                    break;
                case WM_DRAWCLIPBOARD:
                    if (!_firstUse)
                    {
                        _firstUse = !_firstUse;
                    }
                    else if (!ContainsIgnoreMarker())
                    {
                        // content of clipboard has changed:
                        OnClipboardChanged();
                    }
                    // pass the message on:
                    if (!_nextViewerHandle.Equals(IntPtr.Zero))
                    {
                        SendMessage(_nextViewerHandle, m.Msg, m.WParam, m.LParam);
                    }
                    break;
                case WM_CLIPBOARDUPDATE:
                    // New Windows Vista Clipboard API
                    if (!ContainsIgnoreMarker())
                    {
                        // content of clipboard has changed:
                        OnClipboardChanged();
                    }
                    break;
                case WM_DESTROY:
                    // Very important: ensure we are uninstalled.
                    Uninstall();
                    // And call the superclass:
                    base.WndProc(ref m);
                    return;
                default:
                    // call the superclass implementation:
                    base.WndProc(ref m);
                    return;
            }

            // We have processed this message:
            m.Result = IntPtr.Zero;
        }

        /// <summary>
    /// Responds to Window Handle change events and uninstalls
    /// the clipboard change notification if it is installed.
    /// </summary>
        [SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.UnmanagedCode)]
        protected override void OnHandleChange()
        {
            // If we did get to this point, and we're still
            // installed then the chain will be broken.
            // The response to the WM_TERMINATE message should
            // prevent this.
            Uninstall();
            base.OnHandleChange();
        }

        /// <summary>
    /// Installs clipboard change notification.  The
    /// <see cref="NativeWindow.AssignHandle"/> method of this class
    /// must have been called first.
    /// </summary>
        public void Install()
        {
            Uninstall();
            if (Handle.Equals(IntPtr.Zero))
            {
                return;
            }

            if (IsVista())
            {
                if (AddClipboardFormatListener(Handle))
                {
                    _installedHandle = Handle;
                }
            }
            else
            {
                _nextViewerHandle = SetClipboardViewer(Handle);
                _installedHandle = Handle;
            }
        }

        /// <summary>
    /// Uninstalls clipboard change notification.
    /// </summary>
        public void Uninstall()
        {
            if (_installedHandle.Equals(IntPtr.Zero))
            {
                return;
            }

            if (IsVista())
            {
                RemoveClipboardFormatListener(_installedHandle);
                _installedHandle = IntPtr.Zero;
            }
            else
            {
                ChangeClipboardChain(_installedHandle, _nextViewerHandle);
                _nextViewerHandle = IntPtr.Zero;
                _installedHandle = IntPtr.Zero;
            }
        }

        private static bool IsVista()
        {
            // Si es Windows XP, usamos SetClipboardViewer
            // Si es Vista o superior usamos AddClipboardFormatListener (solo funciona en Vista o superior)
            return Environment.OSVersion.Platform == PlatformID.Win32NT & Environment.OSVersion.Version.Major >= 6;
        }

        /// <summary>
    /// Checks for the ignore marker without letting a transient CLIPBRD_E_CANT_OPEN
    /// (another process holding the clipboard open, common with delayed rendering
    /// from web browsers) bubble up through the WndProc and break the message loop.
    /// When the clipboard cannot be opened we report "no marker" so the change is
    /// still processed — the read itself is retried by the event consumer.
    /// </summary>
        private bool ContainsIgnoreMarker()
        {
            try
            {
                return Clipboard.ContainsData(CS_CLIPBOARD_VIEWER_IGNORE);
            }
            catch (Exception ex)
            {
                // CLIPBRD_E_CANT_OPEN or similar transient failure — assume no marker
                // so the change notification is still delivered.
                return false;
            }
        }

        /// <summary>
    /// Raises the <c>ClipboardChanged</c> event.
    /// </summary>
        protected virtual void OnClipboardChanged()
        {

            var handled = new HandledEventArgs(false);
            // content of clipboard has changed:
            ClipboardChanged?.Invoke(this, handled);

            if (handled.Handled && ClearOnReceive)
            {
                // Borramos el portapapeles
                try
                {
                    Clipboard.Clear();
                }
                catch (Exception ex)
                {
                    // Clipboard busy — not fatal, the marker simply won't be applied
                }
            }
            else if (handled.Handled)
            {
                try
                {
                    var data = Clipboard.GetDataObject();
                    if (data is not null)
                    {
                        // Los datos han sido tratados, volvemos a ponerlos en el portapapeles
                        var newData = new DataObject(CS_CLIPBOARD_VIEWER_IGNORE, 0);

                        foreach (string format in data.GetFormats(false))
                            newData.SetData(format, true, data.GetData(format));

                        Clipboard.Clear();
                        Clipboard.SetDataObject(newData, true);
                    }
                }
                catch (Exception ex)
                {
                    // CLIPBRD_E_CANT_OPEN while restoring data — the copy stays as-is,
                    // it will simply be re-analyzed on the next clipboard change.
                }
            }
        }


    }
}