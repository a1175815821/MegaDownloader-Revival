''' <summary>
''' 解压优先级（SharpCompress 0.49 移除了 PriorityExtension 后的自有实现）。
''' 成员名与旧枚举完全一致（Low/Normal/VeryLow），存量 Configuration.xml 里存的
''' 字符串可直接 Enum.Parse，无需数据迁移。
''' 行为说明：旧版在每次压缩流 Read 时 Sleep（Normal 无等待 / Low 1ms / VeryLow 1+2ms）；
''' 新版无此钩子，改为按条目让出（见 DescompresionThrottle.ApplyThrottle）。
''' 默认 Normal，影响为零。
''' </summary>
Public Enum DescompresionPriority
    Low = 0
    Normal = 1
    VeryLow = 2
End Enum

''' <summary>
''' 旧 PriorityExtension.Priority.DecompressionPriority 的替代。
''' </summary>
Public NotInheritable Class DescompresionThrottle

    Private Sub New()
    End Sub

    Private Shared _priority As DescompresionPriority = DescompresionPriority.Normal

    Public Shared Property DecompressionPriority As DescompresionPriority
        Get
            Return _priority
        End Get
        Set(ByVal value As DescompresionPriority)
            _priority = value
        End Set
    End Property

    ''' <summary>
    ''' 每个解压条目完成后调用。Normal 直接返回；Low 让出 1ms；VeryLow 让出 3ms。
    ''' 与旧语义方向一致（优先级越低越让 CPU），粒度由按次 Read 放宽为按条目——
    ''' 旧粒度下大包解压会被数万次 Sleep 拖慢数十秒，新粒度只是温和让出。
    ''' </summary>
    Public Shared Sub ApplyThrottle()
        Select Case _priority
            Case DescompresionPriority.Low
                System.Threading.Thread.Sleep(1)
            Case DescompresionPriority.VeryLow
                System.Threading.Thread.Sleep(3)
        End Select
    End Sub

End Class
