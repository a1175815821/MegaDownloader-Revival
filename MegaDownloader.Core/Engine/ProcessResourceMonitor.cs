using System;
using System.Diagnostics;

namespace MegaDownloader
{

    /// <summary>Process-local resource sampling. Owned and disposed by one background worker.</summary>
    internal sealed class ProcessResourceMonitor : IDisposable
    {

        private readonly Process _process;
        private readonly int _processorCount;
        private bool _hasBaseline;
        private TimeSpan _lastCpuTime;
        private long _lastTimestamp;

        public ProcessResourceMonitor()
        {
            _processorCount = Math.Max(1, Environment.ProcessorCount);
            _process = Process.GetCurrentProcess();
        }

        public void ReadSnapshot(ref double memoryMegabytes, ref double? cpuPercentage)
        {
            _process.Refresh();
            var cpuTime = _process.TotalProcessorTime;
            long timestamp = Stopwatch.GetTimestamp();
            long workingSet = _process.WorkingSet64;

            memoryMegabytes = workingSet / (1024.0d * 1024.0d);
            cpuPercentage = default;
            if (_hasBaseline && timestamp > _lastTimestamp)
            {
                double elapsedSeconds = (timestamp - _lastTimestamp) / (double)Stopwatch.Frequency;
                double cpuSeconds = (cpuTime - _lastCpuTime).TotalSeconds;
                if (cpuSeconds >= 0d)
                {
                    cpuPercentage = Math.Max(0.0d, Math.Min(100.0d, cpuSeconds / elapsedSeconds / _processorCount * 100.0d));
                }
            }

            // The first sample establishes a baseline; CPU usage needs two observations.
            _lastCpuTime = cpuTime;
            _lastTimestamp = timestamp;
            _hasBaseline = true;
        }

        public void Dispose()
        {
            _process.Dispose();
        }
    }
}