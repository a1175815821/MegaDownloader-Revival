Imports System.Windows.Forms

' A 收口版:下载/解压失败的统一落点——持久化的失败标签页，带计数与一键重试。
' 背景:此前只有配额横幅有用户可见的落点；下载/解压失败只进日志+红色态（列表行）。
' 设计取舍:
'  - 不复用 Toast（Toast 是短命的 2-10s 提示，错误需要持久展示直至用户采取行动）。
'  - 失败 O(1) 快照由 Main 的 RefreshLista tick 认领：Fichero 只翻 Erroneo 态，
'    横幅计数由 Main 轮询 ListaPaquetes，已有 quota 刷新协程同款编组：BeginInvoke 回 UI。
'  - 只有“首次发现”才弹托盘气球，避免 430ms 轮询的每次失效重复打扰；横幅本身常驻。
Partial Public Class Main
    ' 仅持有引用的  ID 单例； disposed guard 与 quotaBannerPanel 同款。
    Private failureBannerPanel As Panel = Nothing
    Private failureBannerLabel As Label = Nothing
    Private failureRetryButton As Button = Nothing
    Private failureDismissButton As Button = Nothing

    ''' <summary>A 收口版：由 430ms UI 刷新 tick 认领 end-state 的 Erroneo 计数快照。
    ''' 与 quota 同款：UI 线程常驻刷新 vs 线程池 worker 调用，故内部 BeginInvoke。 </summary>
    Friend Sub RefreshFailureBanner()
        Try
            If IsDisposed OrElse Not IsHandleCreated Then Return
            If InvokeRequired Then
                BeginInvoke(New Action(AddressOf RefreshFailureBanner))
                Return
            End If
            If failureBannerPanel Is Nothing Then Return
            Dim count As Integer = 0
            Try
                If ListaPaquetes IsNot Nothing Then
                    SyncLock Mutex.ListaDescargas
                        For Each paq As Paquete In ListaPaquetes
                            For Each fic As Fichero In paq.ListaFicheros
                                If fic.DescargaEstado = Estado.Erroneo Then count += 1
                            Next
                        Next
                    End SyncLock
                End If
            Catch ex As ObjectDisposedException
            End Try
            If count > 0 Then
                If Not failureBannerPanel.Visible Then
                    failureBannerPanel.Visible = True
                    failureBannerPanel.BringToFront()
                    LayoutDownloadArea()
                End If
                failureBannerLabel.Text = Language.GetText("Downloads_Failed_Banner").Replace("%N%", count.ToString())
            ElseIf failureBannerPanel.Visible Then
                failureBannerPanel.Visible = False
                LayoutDownloadArea()
            End If
        Catch
        End Try
    End Sub

    Private Sub InitFailureBanner()
        Dim sc As Single = 1.0F
        Try
            Using g As Drawing.Graphics = Me.CreateGraphics()
                sc = g.DpiY / 96.0F
            End Using
        Catch
        End Try
        If sc < 1.0F Then sc = 1.0F
        Dim bannerH As Integer = CInt(30 * sc)
        failureBannerPanel = New Panel()
        failureBannerPanel.Name = "failureBannerPanel"
        failureBannerPanel.Height = bannerH
        failureBannerPanel.Left = 0
        Try
            failureBannerPanel.Top = TableLayoutPanel1.Bottom + (If(quotaBannerPanel IsNot Nothing AndAlso quotaBannerPanel.Visible, quotaBannerPanel.Height, 0))
        Catch
            failureBannerPanel.Top = 70
        End Try
        failureBannerPanel.Width = Me.ClientSize.Width
        failureBannerPanel.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        failureBannerPanel.Visible = False

        failureBannerLabel = New Label()
        failureBannerLabel.Name = "failureBannerLabel"
        failureBannerLabel.AutoSize = False
        failureBannerLabel.Left = 12
        failureBannerLabel.Top = 0
        failureBannerLabel.Height = bannerH
        failureBannerLabel.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        failureBannerLabel.TextAlign = Drawing.ContentAlignment.MiddleLeft

        failureRetryButton = New CommandButton()
        failureRetryButton.Name = "failureRetryButton"
        failureRetryButton.Height = CInt(23 * sc)
        failureRetryButton.Width = CInt(110 * sc)
        failureRetryButton.Top = (bannerH - failureRetryButton.Height) \ 2
        failureRetryButton.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        AddHandler failureRetryButton.Click, AddressOf FailureRetry_Click

        failureDismissButton = New CommandButton()
        failureDismissButton.Name = "failureDismissButton"
        failureDismissButton.Text = "×"
        failureDismissButton.Height = CInt(23 * sc)
        failureDismissButton.Width = CInt(23 * sc)
        failureDismissButton.Top = (bannerH - failureDismissButton.Height) \ 2
        failureDismissButton.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        AddHandler failureDismissButton.Click, Sub()
                                                 ' 隐藏而不清除计数：下轮刷新会按当前 Erroneo 重算，
                                                 ' 若仍有失败会再次弹出，因此这里只隐藏的是“已看到”状态。
                                                 ' 用户真正要消除的是修掉失败（重试/删除），不是关横幅。
                                                 ' 若仍有失败，下轮刷新会重新弹出——这正是为了防止“关一次就永远看不到”。
                                                 failureBannerPanel.Visible = False
                                                 LayoutDownloadArea()
                                             End Sub

        failureBannerPanel.Controls.Add(failureBannerLabel)
        failureBannerPanel.Controls.Add(failureRetryButton)
        failureBannerPanel.Controls.Add(failureDismissButton)
        Me.Controls.Add(failureBannerPanel)
        failureBannerPanel.BringToFront()
        LayoutFailureBanner()
        ApplyFailureBannerTheme()
        UpdateFailureBannerTexts()
    End Sub

    Private Sub LayoutFailureBanner()
        If failureBannerPanel Is Nothing OrElse failureRetryButton Is Nothing OrElse failureBannerLabel Is Nothing Then Return
        Try
            Dim top As Integer = TableLayoutPanel1.Bottom
            If quotaBannerPanel IsNot Nothing AndAlso quotaBannerPanel.Visible Then top += quotaBannerPanel.Height
            failureBannerPanel.Top = top
        Catch
        End Try
        failureBannerPanel.Width = Me.ClientSize.Width
        failureDismissButton.Left = failureBannerPanel.Width - failureDismissButton.Width - 12
        failureRetryButton.Left = failureDismissButton.Left - failureRetryButton.Width - 8
        failureBannerLabel.Width = Math.Max(50, failureRetryButton.Left - 18)
    End Sub

    Private Sub ApplyFailureBannerTheme()
        If failureBannerPanel Is Nothing OrElse failureBannerLabel Is Nothing Then Return
        failureBannerPanel.BackColor = ThemeManager.GetColor("QuotaBack")
        failureBannerLabel.ForeColor = ThemeManager.GetColor("QuotaFore")
        Try
            failureRetryButton.BackColor = ThemeManager.GetColor("QuotaBack")
            failureRetryButton.ForeColor = ThemeManager.GetColor("QuotaFore")
            failureRetryButton.FlatStyle = FlatStyle.Flat
            failureRetryButton.FlatAppearance.BorderColor = ThemeManager.GetColor("QuotaFore")
            failureDismissButton.BackColor = ThemeManager.GetColor("QuotaBack")
            failureDismissButton.ForeColor = ThemeManager.GetColor("QuotaFore")
            failureDismissButton.FlatStyle = FlatStyle.Flat
            failureDismissButton.FlatAppearance.BorderColor = ThemeManager.GetColor("QuotaFore")
        Catch
        End Try
    End Sub

    Private Sub UpdateFailureBannerTexts()
        If failureBannerLabel Is Nothing OrElse failureRetryButton Is Nothing Then Return
        failureRetryButton.Text = Language.GetText("Quota_RetryNow")
        If String.IsNullOrEmpty(failureRetryButton.Text) Then failureRetryButton.Text = "Retry now"
    End Sub

    ''' <summary>失败横幅“重试”：与 Retry All Failed 菜单同语义——重置全部非永久 Erroneo 回 EnCola。
    ''' 永久错误（EsErrorPermanente=True）不重试：链接已失效/密钥错误，自动重下只会烧配额（与自愈调度一致）。
    ''' 配额失败保留断点（ResetearDescarga(preserveProgress:=True)），普通失败全量重置。 </summary>
    Private Sub FailureRetry_Click(sender As Object, e As EventArgs)
        Try
            Dim retried As Integer = 0
            Dim pend As New Generic.List(Of Fichero)
            SyncLock Mutex.ListaDescargas
                For Each paq As Paquete In Me.ListaPaquetes
                    For Each fic As Fichero In paq.ListaFicheros
                        If fic.DescargaEstado = Estado.Erroneo AndAlso Not fic.EsErrorPermanente Then
                            pend.Add(fic)
                        End If
                    Next
                Next
            End SyncLock
            For Each fic As Fichero In pend
                If fic.FailedByQuota Then
                    fic.ResetearDescarga(preserveProgress:=True)
                Else
                    fic.ResetearDescarga()
                End If
                fic.PausaIndividual = False
                fic.SetDescargaEstado = Estado.EnCola
                retried += 1
            Next
            If retried > 0 Then Log.WriteWarning("Failure banner retry: woke " & retried & " failed files.")
            RefreshListaDescargas(True)
            RefreshFailureBanner()
        Catch ex As Exception
            Log.WriteError("FailureRetry failed: " & Log.SafeException(ex))
        End Try
    End Sub
End Class
