Imports System.Drawing
Imports System.Windows.Forms

''' <summary>
''' P0-4 UI:轻量 Toast 提示。None 边框 / TopMost / 不进任务栏,2 秒自动关闭,点击即关。
''' 与 PantallaMsg 互不复用(后者是带文本框的错误窗,且被 ComprobarYAgregarLinks 的
''' formsDiscarded 判据依赖);本窗体已加入该判据名单,避免干扰剪贴板监控。
''' ShowToast 内部编组回 UI 线程,后台线程可直接调用,失败只记日志不抛错。
''' </summary>
Public Class ToastForm
    Inherits Form

    Private Shared _current As ToastForm = Nothing

    Private WithEvents _timer As New Timer()
    Private _label As Label

    Public Sub New(text As String)
        Me.FormBorderStyle = FormBorderStyle.None
        Me.ShowInTaskbar = False
        Me.TopMost = True
        Me.StartPosition = FormStartPosition.Manual
        Me.Size = New Size(300, 60)
        Me.Padding = New Padding(1)
        Me.BackColor = ThemeManager.GetColor("Border")
        Me.ForeColor = ThemeManager.GetColor("Fore")

        _label = New Label()
        _label.Dock = DockStyle.Fill
        _label.BackColor = ThemeManager.GetColor("AltBack")
        _label.ForeColor = Me.ForeColor
        _label.TextAlign = ContentAlignment.MiddleLeft
        _label.Padding = New Padding(12, 6, 12, 6)
        _label.Text = text
        AddHandler _label.Click, AddressOf ToastForm_Click
        Me.Controls.Add(_label)
        AddHandler Me.Click, AddressOf ToastForm_Click

        _timer.Interval = 2000
    End Sub

    Protected Overrides Sub OnShown(e As EventArgs)
        MyBase.OnShown(e)
        _timer.Start()
    End Sub

    Private Sub SetText(text As String)
        If _label IsNot Nothing AndAlso Not _label.IsDisposed Then
            _label.Text = text
        End If
        _timer.Stop()
        _timer.Start()
    End Sub

    Private Sub _timer_Tick(sender As Object, e As EventArgs) Handles _timer.Tick
        Me.Close()
    End Sub

    Private Sub ToastForm_Click(sender As Object, e As EventArgs)
        Me.Close()
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        Try
            If disposing Then
                If _timer IsNot Nothing Then
                    _timer.Dispose()
                    _timer = Nothing
                End If
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    Private Shared Sub Current_FormClosed(sender As Object, e As FormClosedEventArgs)
        Dim f As ToastForm = TryCast(sender, ToastForm)
        If f IsNot Nothing Then
            RemoveHandler f.FormClosed, AddressOf Current_FormClosed
        End If
        _current = Nothing
    End Sub

    ''' <summary>显示 Toast。若已有 Toast 打开则刷新文本并重计时,不会堆叠。</summary>
    Public Shared Sub ShowToast(owner As Form, text As String)
        Try
            If String.IsNullOrEmpty(text) Then Return
            If owner IsNot Nothing AndAlso owner.InvokeRequired Then
                owner.BeginInvoke(New Action(Of Form, String)(AddressOf ShowToast), owner, text)
                Return
            End If
            If _current IsNot Nothing AndAlso Not _current.IsDisposed Then
                _current.SetText(text)
                If Not _current.Visible Then _current.Show()
                Return
            End If
            _current = New ToastForm(text)
            AddHandler _current.FormClosed, AddressOf Current_FormClosed
            Dim area As Rectangle = Screen.PrimaryScreen.WorkingArea
            If owner IsNot Nothing AndAlso owner.Visible Then
                area = Screen.FromControl(owner).WorkingArea
                _current.Location = New Point(owner.Right - _current.Width - 16, owner.Bottom - _current.Height - 48)
                If _current.Left < area.Left Then _current.Left = area.Left + 16
                If _current.Top < area.Top Then _current.Top = area.Top + 16
            Else
                _current.Location = New Point(area.Right - _current.Width - 16, area.Bottom - _current.Height - 16)
            End If
            _current.Show()
        Catch ex As Exception
            Log.WriteError("ShowToast failed: " & ex.ToString)
        End Try
    End Sub

End Class
