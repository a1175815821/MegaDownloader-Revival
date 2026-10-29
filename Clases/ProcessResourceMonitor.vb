Imports System.Diagnostics

''' <summary>Process-local resource sampling. Owned and disposed by one background worker.</summary>
Friend NotInheritable Class ProcessResourceMonitor
    Implements IDisposable

    Private ReadOnly _process As Process
    Private ReadOnly _processorCount As Integer
    Private _hasBaseline As Boolean
    Private _lastCpuTime As TimeSpan
    Private _lastTimestamp As Long

    Public Sub New()
        _processorCount = Math.Max(1, Environment.ProcessorCount)
        _process = Process.GetCurrentProcess()
    End Sub

    Public Sub ReadSnapshot(ByRef memoryMegabytes As Double, ByRef cpuPercentage As Double?)
        _process.Refresh()
        Dim cpuTime As TimeSpan = _process.TotalProcessorTime
        Dim timestamp As Long = Stopwatch.GetTimestamp()
        Dim workingSet As Long = _process.WorkingSet64

        memoryMegabytes = workingSet / (1024.0 * 1024.0)
        cpuPercentage = Nothing
        If _hasBaseline AndAlso timestamp > _lastTimestamp Then
            Dim elapsedSeconds As Double = (timestamp - _lastTimestamp) / CDbl(Stopwatch.Frequency)
            Dim cpuSeconds As Double = (cpuTime - _lastCpuTime).TotalSeconds
            If cpuSeconds >= 0 Then
                cpuPercentage = Math.Max(0.0, Math.Min(100.0, cpuSeconds / elapsedSeconds / _processorCount * 100.0))
            End If
        End If

        ' The first sample establishes a baseline; CPU usage needs two observations.
        _lastCpuTime = cpuTime
        _lastTimestamp = timestamp
        _hasBaseline = True
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        _process.Dispose()
    End Sub
End Class
