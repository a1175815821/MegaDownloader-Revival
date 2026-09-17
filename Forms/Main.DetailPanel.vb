''' <summary>
''' Main 右侧栏结构化详情（第二批第 7 项：先抽纯展示、无事件绑定的部分）。
''' 由 Forms/Main.vb 的同名 Region 纯平移而来，无逻辑改动：
''' 无 Handles 绑定、无 Designer 控件（运行时创建）、调用点（2633/2684/2686 选中变更）
''' 与 Main 同类，Partial 合并后行为完全一致。
''' </summary>
Partial Public Class Main

#Region "v2.5: 右侧栏结构化详情"

    ' 结构化详情:标签列 + 值列,进度单独一条细进度条。替代原先"一个 Label 灌多行文本"
    ' 的写法——长文件名/长路径无法换行对齐,标签与值也没有视觉区分。
    ' 无选中时不建/不显示这些控件,仍由 detailLabel 承担空态引导(B 段)。
    Private detailContentPanel As Panel = Nothing
    Private detailTitleLabel As Label = Nothing
    Private detailRows As New Generic.List(Of KeyValuePair(Of Label, Label))()
    Private detailProgressBar As ProgressBar = Nothing

    ''' <summary>按需构建结构化详情面板(挂在 detailGroup 内,与空态 detailLabel 互斥显示)。
    ''' 行数固定为 状态/进度/大小/速度/剩余/路径,内容在 UpdateDetailContent 里填。</summary>
    Private Sub EnsureDetailContentPanel()
        If detailContentPanel IsNot Nothing AndAlso Not detailContentPanel.IsDisposed Then Return
        If detailGroup Is Nothing Then Return

        detailContentPanel = New Panel()
        detailContentPanel.Name = "detailContentPanel"
        detailContentPanel.Dock = DockStyle.Fill
        detailContentPanel.Visible = False

        detailTitleLabel = New Label()
        detailTitleLabel.Name = "detailTitleLabel"
        detailTitleLabel.AutoSize = False
        detailTitleLabel.AutoEllipsis = True
        detailTitleLabel.Height = 36
        ' 标题加粗,只在此处建一次 Font(换肤不重建,避免 GDI 对象堆积)。
        detailTitleLabel.Font = New Drawing.Font(detailTitleLabel.Font, Drawing.FontStyle.Bold)

        detailContentPanel.Controls.Add(detailTitleLabel)

        detailRows.Clear()
        For i As Integer = 0 To 5
            Dim cap As New Label()
            cap.Name = "detailCap" & i.ToString()
            cap.AutoSize = False
            cap.Width = 48
            cap.Height = 20
            cap.TextAlign = Drawing.ContentAlignment.MiddleLeft

            Dim val As New Label()
            val.Name = "detailVal" & i.ToString()
            val.AutoSize = False
            val.Height = 20
            val.TextAlign = Drawing.ContentAlignment.MiddleLeft
            val.AutoEllipsis = True

            detailContentPanel.Controls.Add(cap)
            detailContentPanel.Controls.Add(val)
            detailRows.Add(New KeyValuePair(Of Label, Label)(cap, val))
        Next

        detailProgressBar = New ProgressBar()
        detailProgressBar.Name = "detailProgressBar"
        detailProgressBar.Minimum = 0
        detailProgressBar.Maximum = 100
        detailProgressBar.Visible = False
        detailProgressBar.Height = 10
        detailContentPanel.Controls.Add(detailProgressBar)

        detailGroup.Controls.Add(detailContentPanel)
        detailContentPanel.BringToFront()
        ' 运行时创建的详情面板同样接受链接文本拖放(与 detailLabel 一致)。
        EnableLinkDrop(detailContentPanel)
        LayoutDetailContent()
        ApplyDetailContentTheme()
    End Sub

    ''' <summary>详情面板内部几何。宽度按 detailGroup 客户区算,避免 DPI 变化后错位。</summary>
    Private Sub LayoutDetailContent()
        Try
            If detailContentPanel Is Nothing OrElse detailContentPanel.IsDisposed Then Return
            Dim sc As Single = GetUiScale()
            Dim pad As Integer = CInt(8 * sc)
            Dim w As Integer = detailContentPanel.ClientSize.Width
            If w <= 0 Then w = detailGroup.ClientSize.Width - pad * 2
            Dim innerW As Integer = Math.Max(60, w - pad * 2)
            Dim capW As Integer = CInt(46 * sc)
            Dim rowH As Integer = Math.Max(18, CInt(20 * sc))

            detailTitleLabel.SetBounds(pad, pad, innerW, CInt(34 * sc))

            Dim y As Integer = detailTitleLabel.Bottom + CInt(4 * sc)
            For i As Integer = 0 To detailRows.Count - 1
                Dim cap As Label = detailRows(i).Key
                Dim val As Label = detailRows(i).Value
                cap.SetBounds(pad, y, capW, rowH)
                val.SetBounds(pad + capW, y, innerW - capW, rowH)
                y += rowH

                ' 第 2 行(进度)下方插一条进度条
                If i = 1 AndAlso detailProgressBar IsNot Nothing Then
                    detailProgressBar.SetBounds(pad + capW, y, innerW - capW, CInt(10 * sc))
                    detailProgressBar.Visible = True
                    y += detailProgressBar.Height + CInt(2 * sc)
                End If
            Next
        Catch ex As Exception
            Log.WriteDebug("LayoutDetailContent failed: " & Log.SafeException(ex))
        End Try
    End Sub

    Private Sub ApplyDetailContentTheme()
        Try
            If detailContentPanel Is Nothing OrElse detailContentPanel.IsDisposed Then Return
            Dim subtle As Drawing.Color = ThemeManager.GetColor("Border")
            Dim fore As Drawing.Color = ThemeManager.GetColor("Fore")
            If detailTitleLabel IsNot Nothing Then
                detailTitleLabel.ForeColor = fore
            End If
            For Each kv As KeyValuePair(Of Label, Label) In detailRows
                kv.Key.ForeColor = subtle
                kv.Value.ForeColor = fore
            Next
            If detailProgressBar IsNot Nothing Then
                detailProgressBar.BackColor = ThemeManager.GetColor("ControlBack")
                detailProgressBar.ForeColor = ThemeManager.GetColor("Selection")
            End If
        Catch ex As Exception
            Log.WriteDebug("ApplyDetailContentTheme failed: " & Log.SafeException(ex))
        End Try
    End Sub

    ''' <summary>把选中项的数据填进结构化详情。标签文案走语言系统,随语言切换同步。</summary>
    Private Sub UpdateDetailContent(ele As IDescarga)
        Try
            EnsureDetailContentPanel()
            If detailContentPanel Is Nothing Then Return

            detailTitleLabel.Text = ele.DescargaNombre

            Dim st As Estado = ele.DescargaEstado()
            Dim pct As Decimal = ele.DescargaPorcentaje()
            Dim size As Long = ele.DescargaTamanoBytes()
            Dim done As String = "-"
            If size > 0 Then
                done = PintarTamano(Math.Ceiling(pct * size / 100)) & " / " & PintarTamano(size)
            End If

            Dim fic As Fichero = TryCast(ele, Fichero)
            Dim ruta As String = "-"
            If fic IsNot Nothing AndAlso Not String.IsNullOrEmpty(fic.RutaRelativa) Then ruta = fic.RutaRelativa

            Dim captions() As String = {
                Language.GetText("Status"),
                Language.GetText("Progress"),
                Language.GetText("Size"),
                Language.GetText("Speed"),
                Language.GetText("Remaining"),
                Language.GetText("Detail_Path")
            }
            ' 空字符串统一显示为 "-",避免详情出现空白行(速度/剩余在未下载时本就无值)。
            Dim speedTxt As String = PintarVelocidadDescarga(ele)
            If String.IsNullOrEmpty(speedTxt) Then speedTxt = "-"
            Dim remainTxt As String = ele.DescargaTiempoEstimadoDescarga()
            If String.IsNullOrEmpty(remainTxt) Then remainTxt = "-"

            Dim values() As String = {
                EstadoDisplayText(st),
                pct.ToString("F2") & "%",
                done,
                speedTxt,
                remainTxt,
                ruta
            }

            For i As Integer = 0 To Math.Min(captions.Length, detailRows.Count) - 1
                detailRows(i).Key.Text = captions(i)
                detailRows(i).Value.Text = values(i)
                ' 失败态用语义色标注,与列表"状态"列的着色保持一致
                If i = 0 AndAlso st = Estado.Erroneo Then
                    detailRows(i).Value.ForeColor = ThemeManager.GetColor("ErrorFore")
                ElseIf i = 0 AndAlso st = Estado.Completado Then
                    detailRows(i).Value.ForeColor = ThemeManager.GetColor("SuccessFore")
                Else
                    detailRows(i).Value.ForeColor = ThemeManager.GetColor("Fore")
                End If
            Next

            If detailProgressBar IsNot Nothing Then
                Dim p As Integer = CInt(Math.Floor(pct))
                If p < 0 Then p = 0
                If p > 100 Then p = 100
                Try
                    detailProgressBar.Value = p
                Catch
                End Try
            End If

            detailContentPanel.Visible = True
            If detailLabel IsNot Nothing Then detailLabel.Visible = False
            LayoutDetailContent()
        Catch ex As Exception
            Log.WriteDebug("UpdateDetailContent failed: " & Log.SafeException(ex))
        End Try
    End Sub

    ''' <summary>回到空态:显示引导文案(Detail_Empty + Detail_EmptyHint),隐藏结构化面板。
    ''' 主/副两行通过换行拼进同一个 detailLabel,不新增控件。</summary>
    Private Sub ShowDetailEmptyState()
        Try
            If detailContentPanel IsNot Nothing AndAlso Not detailContentPanel.IsDisposed Then
                detailContentPanel.Visible = False
            End If
            If detailLabel Is Nothing OrElse detailLabel.IsDisposed Then Return
            detailLabel.Visible = True
            Dim main As String = Language.GetText("Detail_Empty")
            Dim hint As String = Language.GetText("Detail_EmptyHint")
            Dim text As String = main
            If Not String.IsNullOrEmpty(hint) AndAlso Not String.Equals(hint, "Detail_EmptyHint") Then
                text = main & vbCrLf & vbCrLf & hint
            End If
            If detailLabel.Text <> text Then detailLabel.Text = text
        Catch ex As Exception
            Log.WriteDebug("ShowDetailEmptyState failed: " & Log.SafeException(ex))
        End Try
    End Sub

#End Region

End Class
