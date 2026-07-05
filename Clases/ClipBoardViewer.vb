Imports System
Imports System.ComponentModel
Imports System.Runtime.InteropServices
Imports System.Security.Permissions
Imports System.Text
Imports System.Windows.Forms

' Requires unmanaged code
' <Assembly: SecurityPermission(SecurityAction.RequestMinimum, UnmanagedCode:=True)> 

' Requires all clipboard access
' <Assembly: UIPermission(SecurityAction.RequestMinimum, Clipboard:=UIPermissionClipboard.AllClipboard)> 


''' <summary>
''' Provides a way to receive notifications of changes to the 
''' content of the clipboard using the Windows API.  
''' </summary>
''' <remarks>
''' To be a part of the change notification you need to register a 
''' window in the Clipboard Viewer Chain.  This provides
''' notification messages to the window whenever the 
''' clipboard changes, and also messages associated with managing
''' the chain.  This class manages the detail of keeping the
''' chain intact and ensuring that the application is removed
''' from the chain at the right point.
''' 
''' Use the <see cref="System.Windows.Forms.NativeWindow.AssignHandle"/> method 
''' to connect this class up to a form to begin receiving notifications.
''' Note that a Form can change its <see cref="NativeWindow.Handle"/>   
''' under certain circumstances; for example, if you change the 
''' <see cref="System.Windows.Forms.Form.ShowInTaskbar"/> property the Framework
''' must re-create the form from scratch since Windows ignores changes to 
''' that style unless they are in place when the Window is created.
''' (As a consequence, you should try to set as many high-level Window 
''' style details as possible prior to creating the Window, or at least,
''' prior to making it visible).  The OnHandleChange
''' method of this class is called when this happens.  You need to
''' re-assign the handle again whenever this occurs.  Unfortunately
''' OnHandleChange is not a useful event in which to
''' do anything since the window handle at that point contains neither
''' a valid old window or a valid new one.  Therefore you need to
''' make the call to re-assign at a point when you know the window
''' is valid, for example, after styles have been changed, or 
''' by overriding <see cref="System.Windows.Forms.Form.OnHandleCreated"/>.
''' </remarks>      
Public Class ClipboardViewer
    Inherits NativeWindow
    Implements IDisposable

#Region "Unmanaged Code"

    Private Const WM_CHANGECBCHAIN As Integer = &H30D
    Private Const WM_CLIPBOARDUPDATE As Integer = &H31D
    Private Const WM_DESTROY As Integer = &H2
    Private Const WM_DRAWCLIPBOARD As Integer = &H308

    Private Const CS_CLIPBOARD_VIEWER_IGNORE As String = "Clipboard Viewer Ignore"

    <DllImport("user32")> _
    Private Shared Function SetClipboardViewer(hWnd As IntPtr) As IntPtr
    End Function

    <DllImport("user32")> _
    Private Shared Function ChangeClipboardChain(hWnd As IntPtr, hWndNext As IntPtr) As Integer
    End Function

    <DllImport("user32")> _
    Private Shared Function AddClipboardFormatListener(hwnd As IntPtr) As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

    <DllImport("user32")> _
    Private Shared Function RemoveClipboardFormatListener(hwnd As IntPtr) As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

    <DllImport("user32", CharSet:=CharSet.Auto)> _
    Private Shared Function SendMessage(hWnd As IntPtr, wMsg As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
    End Function

#End Region

#Region "Member Variables"

    Private _firstUse As Boolean


    ''' <summary>
    ''' Whether this class has been disposed or not.
    ''' </summary>
    Private _disposed As Boolean

    ''' <summary>
    ''' The Window clipboard change notification was installed for.
    ''' </summary>
    Private _installedHandle As IntPtr = IntPtr.Zero

    ''' <summary>
    ''' The next handle in the clipboard viewer chain when the 
    ''' clipboard notification is installed, otherwise <see cref="IntPtr.Zero"/>
    ''' </summary>
    Private _nextViewerHandle As IntPtr = IntPtr.Zero


#End Region

#Region "Events"

    ''' <summary>
    ''' Notifies of a change to the clipboard's content.
    ''' </summary>
    Public Event ClipboardChanged As EventHandler(Of HandledEventArgs)

#End Region

    ''' <summary>
    ''' Initializes a new instance of the ClipboardViewer class
    ''' </summary>
    Public Sub New()

    End Sub

    ''' <summary>
    ''' Clear clipboard if is sucesfully processed (accepted)
    ''' </summary>
    Public ClearOnReceive As Boolean

    ''' <summary>
    ''' Return true if ClipboardViewer is installed
    ''' </summary>
    Public ReadOnly Property IsInstalled() As Boolean
        Get
            Return Not _installedHandle.Equals(IntPtr.Zero)
        End Get
    End Property

#Region "IDisposable Members"

    ''' <summary>
    ''' Uninstalls clipboard event notifications if necessary
    ''' during dispose of this object.
    ''' </summary>
    Public Sub Dispose() Implements System.IDisposable.Dispose
        If _disposed Then
            Return
        End If

        Uninstall()
        _disposed = True
    End Sub

#End Region


    ''' <summary>
    ''' Provides default WndProc processing and responds to
    ''' clipboard change notifications.
    ''' </summary>
    ''' <param name="m"></param>
    <SecurityPermission(SecurityAction.LinkDemand, Flags:=SecurityPermissionFlag.UnmanagedCode)> _
    Protected Overrides Sub WndProc(ByRef m As Message)
        ' if the message is a clipboard change notification
        Select Case m.Msg
            Case WM_CHANGECBCHAIN
                'If the next window is closing, repair the chain
                If m.WParam = _nextViewerHandle Then
                    _nextViewerHandle = m.LParam
                Else
                    'Otherwise, pass the message to the next link
                    If Not _nextViewerHandle.Equals(IntPtr.Zero) Then
                        SendMessage(_nextViewerHandle, m.Msg, m.WParam, m.LParam)
                    End If
                End If
                Exit Select
            Case WM_DRAWCLIPBOARD
                If Not _firstUse Then
                    _firstUse = Not _firstUse
                Else
                    If Not Clipboard.ContainsData(CS_CLIPBOARD_VIEWER_IGNORE) Then
                        ' content of clipboard has changed:
                        OnClipboardChanged()
                    End If
                End If
                ' pass the message on:
                If Not _nextViewerHandle.Equals(IntPtr.Zero) Then
                    SendMessage(_nextViewerHandle, m.Msg, m.WParam, m.LParam)
                End If
                Exit Select
            Case WM_CLIPBOARDUPDATE
                ' New Windows Vista Clipboard API
                If Not Clipboard.ContainsData(CS_CLIPBOARD_VIEWER_IGNORE) Then
                    ' content of clipboard has changed:
                    OnClipboardChanged()
                End If
                Exit Select
            Case WM_DESTROY
                ' Very important: ensure we are uninstalled.
                Uninstall()
                ' And call the superclass:
                MyBase.WndProc(m)
                Return
            Case Else
                ' call the superclass implementation:
                MyBase.WndProc(m)
                Return
        End Select

        ' We have processed this message:
        m.Result = IntPtr.Zero
    End Sub

    ''' <summary>
    ''' Responds to Window Handle change events and uninstalls
    ''' the clipboard change notification if it is installed.
    ''' </summary>
    <SecurityPermission(SecurityAction.LinkDemand, Flags:=SecurityPermissionFlag.UnmanagedCode)> _
    Protected Overrides Sub OnHandleChange()
        ' If we did get to this point, and we're still
        ' installed then the chain will be broken.
        ' The response to the WM_TERMINATE message should
        ' prevent this.
        Uninstall()
        MyBase.OnHandleChange()
    End Sub

    ''' <summary>
    ''' Installs clipboard change notification.  The
    ''' <see cref="NativeWindow.AssignHandle"/> method of this class
    ''' must have been called first.
    ''' </summary>
    Public Sub Install()
        Uninstall()
        If Handle.Equals(IntPtr.Zero) Then
            Return
        End If

        If IsVista() Then
            AddClipboardFormatListener(Handle)
        Else
            _nextViewerHandle = SetClipboardViewer(Handle)
            _installedHandle = Handle
        End If
    End Sub

    ''' <summary>
    ''' Uninstalls clipboard change notification.
    ''' </summary>
    Public Sub Uninstall()
        If _installedHandle.Equals(IntPtr.Zero) Then
            Return
        End If

        If IsVista() Then
            RemoveClipboardFormatListener(_installedHandle)
        Else
            ChangeClipboardChain(_installedHandle, _nextViewerHandle)
            _nextViewerHandle = IntPtr.Zero
            _installedHandle = IntPtr.Zero
        End If
    End Sub

    Private Shared Function IsVista() As Boolean
        ' Si es Windows XP, usamos SetClipboardViewer
        ' Si es Vista o superior usamos AddClipboardFormatListener (solo funciona en Vista o superior)
        Return (Environment.OSVersion.Platform = PlatformID.Win32NT And _
                Environment.OSVersion.Version.Major >= 6)
    End Function

    ''' <summary>
    ''' Raises the <c>ClipboardChanged</c> event.
    ''' </summary>
    Protected Overridable Sub OnClipboardChanged()

        Dim handled = New HandledEventArgs(False)
        ' content of clipboard has changed:
        RaiseEvent ClipboardChanged(Me, handled)

        If handled.Handled AndAlso ClearOnReceive Then
            ' Borramos el portapapeles
            Clipboard.Clear()
        ElseIf handled.Handled Then
            Dim data = Clipboard.GetDataObject()
            If data IsNot Nothing Then
                ' Los datos han sido tratados, volvemos a ponerlos en el portapapeles
                Dim newData = New DataObject(CS_CLIPBOARD_VIEWER_IGNORE, 0)

                For Each format As String In data.GetFormats(False)
                    newData.SetData(format, True, data.GetData(format))
                Next

                Clipboard.Clear()
                Clipboard.SetDataObject(newData, True)
            End If
        End If
    End Sub


End Class
