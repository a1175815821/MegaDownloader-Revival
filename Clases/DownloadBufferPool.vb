''' <summary>
''' P1-1: 下载 chunk 的固定大小缓冲区池。
'''
''' 每个 chunk worker 原来都要 New 一个 750KiB 的 BufferDisk（超过 85KB → LOH）
''' 和一个 50KiB 的 BufferMem；按默认 3 并发 × 3 连接，一个 1MB 文件就要分配
''' 约 2.4MiB 的 LOH 数组，大量小文件队列会持续制造大对象 GC。这里按数组长度
''' 池化复用，让整个小文件样本的每文件 LOH 分配接近 0；Rent 同时按需收窄
''' 请求尺寸（调用方按 chunk 实际大小申请），小文件连池命中之外的新分配也很小。
'''
''' 容量上限：每尺寸最多保留 8 个，池内总字节数最多 32MiB；超出的归还直接丢弃，
''' 交给 GC，避免不同文件尺寸把池撑大。租出的数组不与任何其他 worker 共享。
''' </summary>
Friend NotInheritable Class DownloadBufferPool

    Private Const MaxRetainedBytes As Long = 32L * 1024 * 1024
    Private Const MaxPerSize As Integer = 8
    Private Const SizeGranularity As Integer = 1024

    Private Shared ReadOnly _sync As New Object()
    Private Shared ReadOnly _buckets As New Generic.Dictionary(Of Integer, Generic.Stack(Of Byte()))
    Private Shared _retainedBytes As Long

    Private Sub New()
    End Sub

    ''' <summary>Rent 一个长度不小于 requestedSize 的数组（长度按 1KiB 向上取整）。</summary>
    Friend Shared Function Rent(requestedSize As Long) As Byte()
        Dim size As Integer = NormalizeSize(requestedSize)
        SyncLock _sync
            Dim stack As Generic.Stack(Of Byte()) = Nothing
            If _buckets.TryGetValue(size, stack) AndAlso stack.Count > 0 Then
                Dim pooled As Byte() = stack.Pop()
                _retainedBytes -= pooled.Length
                Return pooled
            End If
        End SyncLock
        Return New Byte(size - 1) {}
    End Function

    ''' <summary>归还数组；池已满时直接丢弃。</summary>
    Friend Shared Sub ReturnBuffer(buffer As Byte())
        If buffer Is Nothing OrElse buffer.Length = 0 Then Exit Sub
        SyncLock _sync
            If _retainedBytes + buffer.Length > MaxRetainedBytes Then Exit Sub
            Dim stack As Generic.Stack(Of Byte()) = Nothing
            If Not _buckets.TryGetValue(buffer.Length, stack) Then
                stack = New Generic.Stack(Of Byte())()
                _buckets(buffer.Length) = stack
            End If
            If stack.Count >= MaxPerSize Then Exit Sub
            stack.Push(buffer)
            _retainedBytes += buffer.Length
        End SyncLock
    End Sub

    Private Shared Function NormalizeSize(requestedSize As Long) As Integer
        If requestedSize < 1 Then requestedSize = 1
        Dim size As Long = ((requestedSize + SizeGranularity - 1) \ SizeGranularity) * SizeGranularity
        If size > Integer.MaxValue Then size = Integer.MaxValue
        Return CInt(size)
    End Function

End Class
