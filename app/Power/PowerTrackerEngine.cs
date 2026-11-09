using System.Diagnostics;

namespace GHelper.Power
{
    public class ProcessSample
    {
        public string ProcessName { get; set; } = string.Empty;
        public int ProcessCount { get; set; } = 1;
        public float CpuWatts { get; set; }
        public float GpuWatts { get; set; }
        public float TotalWatts => CpuWatts + GpuWatts;
        public bool IsOnGpu { get; set; }
    }

    public class SystemSample
    {
        public DateTime Timestamp { get; set; }
        public float DurationSeconds { get; set; }
        public float CpuWatts { get; set; }
        public float GpuWatts { get; set; }
        public float BatteryDischargeWatts { get; set; }
        public float TotalWatts { get; set; }
        public Dictionary<string, ProcessSample> Processes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public class ProcessSummary
    {
        public string Name { get; set; } = string.Empty;
        public int ProcessCount { get; set; } = 1;
        public float CurrentWatts { get; set; }
        public float AverageWatts { get; set; }
        public float PeakWatts { get; set; }
        public float TotalEnergyWh { get; set; }
        public float PercentShare { get; set; }
        public string EngineTag { get; set; } = "CPU";
        public bool IsActive { get; set; }
    }

    public class PowerTrackerSummary
    {
        public TimeSpan SelectedPeriod { get; set; }
        public int SampleCount { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public float CurrentTotalWatts { get; set; }
        public float AverageTotalWatts { get; set; }
        public float PeakTotalWatts { get; set; }
        public DateTime PeakTimestamp { get; set; }
        public float TotalEnergyWh { get; set; }
        public float CpuEnergyWh { get; set; }
        public float GpuEnergyWh { get; set; }
        public float BatteryDischargeEnergyWh { get; set; }
        public float CurrentCpuWatts { get; set; }
        public float CurrentGpuWatts { get; set; }
        public float CurrentBatteryWatts { get; set; }
        public bool IsOnBattery { get; set; }
        public double? EstimatedBatteryMinutes { get; set; }
        public List<ProcessSummary> TopProcesses { get; set; } = [];
        public List<(DateTime Time, float TotalWatts, float CpuWatts, float GpuWatts)> TrendData { get; set; } = [];
    }

    public static class PowerTrackerEngine
    {
        private static readonly object _lock = new();
        private static System.Threading.Timer? _timer;
        private static readonly LinkedList<SystemSample> _samples = new();
        private const int MaxSamples = 3600; // 2 hours at 2s interval

        private static Dictionary<int, (string Name, TimeSpan CpuTime)> _prevProcCpu = new();
        private static DateTime _prevSampleTime = DateTime.UtcNow;
        private static DateTime _sessionStartTime = DateTime.UtcNow;
        private static bool _isRunning = false;

        private static readonly HashSet<string> _systemGpuProcesses = new(StringComparer.OrdinalIgnoreCase)
        {
            "dwm", "csrss", "winlogon", "services", "lsass", "smss", "wininit",
            "svchost", "fontdrvhost", "nvcontainer", "nvdisplay.container",
            "explorer", "taskhostw", "sihost", "shellexperiencehost",
            "startmenuexperiencehost", "textinputhost", "applicationframehost", "systemsettings",
            "lockapp", "phoneexperiencehost", "crossdeviceresume", "amdrssxcext"
        };

        public static void Start()
        {
            lock (_lock)
            {
                if (_isRunning) return;
                _isRunning = true;
                _sessionStartTime = DateTime.UtcNow;
                _prevSampleTime = DateTime.UtcNow;
                _prevProcCpu.Clear();
                _samples.Clear();

                _timer = new System.Threading.Timer(OnTick, null, 1000, 2000);
            }
        }

        public static void Stop()
        {
            lock (_lock)
            {
                _isRunning = false;
                _timer?.Dispose();
                _timer = null;
            }
        }

        public static void ResetSession()
        {
            lock (_lock)
            {
                _samples.Clear();
                _sessionStartTime = DateTime.UtcNow;
                _prevSampleTime = DateTime.UtcNow;
                _prevProcCpu.Clear();
            }
        }

        private static void OnTick(object? state)
        {
            try
            {
                var now = DateTime.UtcNow;
                float durationSeconds;
                lock (_lock)
                {
                    durationSeconds = (float)(now - _prevSampleTime).TotalSeconds;
                    if (durationSeconds <= 0.1f) durationSeconds = 2.0f;
                    _prevSampleTime = now;
                }

                // 1. Hardware Power Readings
                float cpuWatts = HardwareControl.GetCPUPower() ?? HardwareControl.cpuPower ?? 0f;
                float gpuWatts = HardwareControl.GetGPUPower() ?? HardwareControl.gpuPower ?? 0f;
                float batteryRate = (float)HardwareControl.batteryRate;
                bool onBattery = SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline;

                float totalWatts;
                if (onBattery && batteryRate > 0)
                {
                    totalWatts = Math.Max(batteryRate, cpuWatts + gpuWatts);
                }
                else
                {
                    totalWatts = cpuWatts + gpuWatts;
                }

                // 2. Active Processes CPU Snapshot
                var currentProcCpu = new Dictionary<int, (string Name, TimeSpan CpuTime)>();
                var currentProcCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                try
                {
                    Process[] procs = Process.GetProcesses();
                    foreach (Process p in procs)
                    {
                        try
                        {
                            if (p.Id == 0) continue; // Skip idle
                            string name = p.ProcessName;
                            TimeSpan cpuTime = p.TotalProcessorTime;
                            currentProcCpu[p.Id] = (name, cpuTime);

                            if (currentProcCounts.ContainsKey(name))
                                currentProcCounts[name]++;
                            else
                                currentProcCounts[name] = 1;
                        }
                        catch
                        {
                            // System process access denied
                        }
                        finally
                        {
                            p.Dispose();
                        }
                    }
                }
                catch
                {
                    // Error enumerating processes
                }

                // 3. Active GPU Processes via NVAPI
                var gpuAppNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    Process[] gpuProcs = HardwareControl.GetActiveGpuProcesses();
                    foreach (Process gp in gpuProcs)
                    {
                        try
                        {
                            gpuAppNames.Add(gp.ProcessName);
                        }
                        catch { }
                        finally
                        {
                            gp.Dispose();
                        }
                    }
                }
                catch { }

                // 4. Compute CPU deltas per process
                var appCpuDeltas = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                double totalCpuDeltaMs = 0;

                lock (_lock)
                {
                    foreach (var kv in currentProcCpu)
                    {
                        int pid = kv.Key;
                        string name = kv.Value.Name;
                        TimeSpan curTime = kv.Value.CpuTime;

                        if (_prevProcCpu.TryGetValue(pid, out var prev) && prev.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                        {
                            double deltaMs = (curTime - prev.CpuTime).TotalMilliseconds;
                            if (deltaMs > 0)
                            {
                                totalCpuDeltaMs += deltaMs;
                                if (appCpuDeltas.ContainsKey(name))
                                    appCpuDeltas[name] += deltaMs;
                                else
                                    appCpuDeltas[name] = deltaMs;
                            }
                        }
                    }

                    _prevProcCpu = currentProcCpu;
                }

                // 5. Apportion CPU & GPU power to applications
                var sample = new SystemSample
                {
                    Timestamp = now,
                    DurationSeconds = durationSeconds,
                    CpuWatts = cpuWatts,
                    GpuWatts = gpuWatts,
                    BatteryDischargeWatts = (onBattery && batteryRate > 0) ? batteryRate : 0f,
                    TotalWatts = totalWatts
                };

                // Allocate CPU power
                double accountedCpuWatts = 0;
                if (totalCpuDeltaMs > 0 && cpuWatts > 0)
                {
                    foreach (var kv in appCpuDeltas)
                    {
                        string name = kv.Key;
                        double share = kv.Value / totalCpuDeltaMs;
                        float procCpuWatts = (float)(cpuWatts * share);
                        accountedCpuWatts += procCpuWatts;

                        int count = currentProcCounts.TryGetValue(name, out int c) ? c : 1;
                        sample.Processes[name] = new ProcessSample
                        {
                            ProcessName = name,
                            ProcessCount = count,
                            CpuWatts = procCpuWatts,
                            GpuWatts = 0,
                            IsOnGpu = false
                        };
                    }
                }

                // Unaccounted CPU power goes to System / Idle
                float cpuRemainder = (float)Math.Max(0, cpuWatts - accountedCpuWatts);
                if (cpuRemainder > 0.05f)
                {
                    if (sample.Processes.TryGetValue("System / Idle", out var idleSample))
                    {
                        idleSample.CpuWatts += cpuRemainder;
                    }
                    else
                    {
                        sample.Processes["System / Idle"] = new ProcessSample
                        {
                            ProcessName = "System / Idle",
                            ProcessCount = 1,
                            CpuWatts = cpuRemainder,
                            GpuWatts = 0,
                            IsOnGpu = false
                        };
                    }
                }

                // Allocate GPU power
                if (gpuWatts > 0)
                {
                    if (gpuAppNames.Count > 0)
                    {
                        var userGpuApps = gpuAppNames.Where(a => !_systemGpuProcesses.Contains(a)).ToList();
                        var sysGpuApps = gpuAppNames.Where(a => _systemGpuProcesses.Contains(a)).ToList();

                        float sysPortion = sysGpuApps.Count > 0 ? (userGpuApps.Count > 0 ? Math.Min(3.0f, gpuWatts * 0.1f) : gpuWatts) : 0f;
                        float userPortion = Math.Max(0, gpuWatts - sysPortion);

                        // Allocate to system compositor apps
                        if (sysGpuApps.Count > 0 && sysPortion > 0)
                        {
                            float perSys = sysPortion / sysGpuApps.Count;
                            foreach (string sysApp in sysGpuApps)
                            {
                                if (sample.Processes.TryGetValue(sysApp, out var existing))
                                {
                                    existing.GpuWatts += perSys;
                                    existing.IsOnGpu = true;
                                }
                                else
                                {
                                    int count = currentProcCounts.TryGetValue(sysApp, out int c) ? c : 1;
                                    sample.Processes[sysApp] = new ProcessSample
                                    {
                                        ProcessName = sysApp,
                                        ProcessCount = count,
                                        CpuWatts = 0,
                                        GpuWatts = perSys,
                                        IsOnGpu = true
                                    };
                                }
                            }
                        }

                        // Allocate to user apps
                        if (userGpuApps.Count > 0 && userPortion > 0)
                        {
                            // Weight by CPU activity if available, otherwise divide equally
                            double userCpuSum = userGpuApps.Sum(a => appCpuDeltas.TryGetValue(a, out double d) ? d : 1.0);
                            foreach (string uApp in userGpuApps)
                            {
                                double w = userGpuApps.Count == 1 ? 1.0 : ((appCpuDeltas.TryGetValue(uApp, out double d) ? d : 1.0) / userCpuSum);
                                float appGpuWatts = (float)(userPortion * w);

                                if (sample.Processes.TryGetValue(uApp, out var existing))
                                {
                                    existing.GpuWatts += appGpuWatts;
                                    existing.IsOnGpu = true;
                                }
                                else
                                {
                                    int count = currentProcCounts.TryGetValue(uApp, out int c) ? c : 1;
                                    sample.Processes[uApp] = new ProcessSample
                                    {
                                        ProcessName = uApp,
                                        ProcessCount = count,
                                        CpuWatts = 0,
                                        GpuWatts = appGpuWatts,
                                        IsOnGpu = true
                                    };
                                }
                            }
                        }
                    }
                    else
                    {
                        // Active dGPU draw with no direct process attached (Display / Desktop Compositor)
                        string idleKey = "GPU Idle / Display";
                        if (sample.Processes.TryGetValue(idleKey, out var existing))
                        {
                            existing.GpuWatts += gpuWatts;
                            existing.IsOnGpu = true;
                        }
                        else
                        {
                            sample.Processes[idleKey] = new ProcessSample
                            {
                                ProcessName = idleKey,
                                ProcessCount = 1,
                                CpuWatts = 0,
                                GpuWatts = gpuWatts,
                                IsOnGpu = true
                            };
                        }
                    }
                }

                lock (_lock)
                {
                    _samples.AddLast(sample);
                    while (_samples.Count > MaxSamples)
                    {
                        _samples.RemoveFirst();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("PowerTrackerEngine tick error: " + ex.Message);
            }
        }

        public static PowerTrackerSummary GetSummary(TimeSpan window)
        {
            var now = DateTime.UtcNow;
            DateTime cutoff = (window <= TimeSpan.Zero || window >= TimeSpan.FromDays(1))
                ? _sessionStartTime
                : now.Subtract(window);

            List<SystemSample> matchingSamples;
            lock (_lock)
            {
                matchingSamples = _samples.Where(s => s.Timestamp >= cutoff).ToList();
            }

            var summary = new PowerTrackerSummary
            {
                SelectedPeriod = window,
                SampleCount = matchingSamples.Count,
                StartTime = matchingSamples.Count > 0 ? matchingSamples.First().Timestamp : cutoff,
                EndTime = now,
                IsOnBattery = SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline
            };

            if (matchingSamples.Count == 0)
            {
                summary.CurrentCpuWatts = HardwareControl.GetCPUPower() ?? HardwareControl.cpuPower ?? 0f;
                summary.CurrentGpuWatts = HardwareControl.GetGPUPower() ?? HardwareControl.gpuPower ?? 0f;
                summary.CurrentBatteryWatts = (float)HardwareControl.batteryRate;
                summary.CurrentTotalWatts = summary.IsOnBattery && summary.CurrentBatteryWatts > 0
                    ? Math.Max(summary.CurrentBatteryWatts, summary.CurrentCpuWatts + summary.CurrentGpuWatts)
                    : (summary.CurrentCpuWatts + summary.CurrentGpuWatts);
                return summary;
            }

            var latest = matchingSamples.Last();
            summary.CurrentTotalWatts = latest.TotalWatts;
            summary.CurrentCpuWatts = latest.CpuWatts;
            summary.CurrentGpuWatts = latest.GpuWatts;
            summary.CurrentBatteryWatts = latest.BatteryDischargeWatts;

            float sumTotalWatts = 0;
            float peakWatts = 0;
            DateTime peakTime = latest.Timestamp;
            float totalEnergyWs = 0;
            float cpuEnergyWs = 0;
            float gpuEnergyWs = 0;
            float batteryEnergyWs = 0;

            // Rollup per application
            var appAccumulators = new Dictionary<string, (float SumWatts, float PeakWatts, float EnergyWs, int LastCount, bool UsedGpu, bool UsedCpu, float LastWatts, int SampleHits)>(StringComparer.OrdinalIgnoreCase);

            foreach (var s in matchingSamples)
            {
                sumTotalWatts += s.TotalWatts;
                if (s.TotalWatts > peakWatts)
                {
                    peakWatts = s.TotalWatts;
                    peakTime = s.Timestamp;
                }

                float dt = s.DurationSeconds > 0 ? s.DurationSeconds : 2.0f;
                totalEnergyWs += s.TotalWatts * dt;
                cpuEnergyWs += s.CpuWatts * dt;
                gpuEnergyWs += s.GpuWatts * dt;
                batteryEnergyWs += s.BatteryDischargeWatts * dt;

                foreach (var kv in s.Processes)
                {
                    string name = kv.Key;
                    var p = kv.Value;

                    if (!appAccumulators.TryGetValue(name, out var acc))
                    {
                        acc = (0f, 0f, 0f, p.ProcessCount, p.IsOnGpu, p.CpuWatts > 0.01f, 0f, 0);
                    }

                    acc.SumWatts += p.TotalWatts;
                    if (p.TotalWatts > acc.PeakWatts) acc.PeakWatts = p.TotalWatts;
                    acc.EnergyWs += p.TotalWatts * dt;
                    acc.LastCount = p.ProcessCount;
                    if (p.IsOnGpu) acc.UsedGpu = true;
                    if (p.CpuWatts > 0.05f) acc.UsedCpu = true;
                    acc.SampleHits++;

                    if (s == latest)
                    {
                        acc.LastWatts = p.TotalWatts;
                    }

                    appAccumulators[name] = acc;
                }
            }

            summary.AverageTotalWatts = sumTotalWatts / matchingSamples.Count;
            summary.PeakTotalWatts = peakWatts;
            summary.PeakTimestamp = peakTime;
            summary.TotalEnergyWh = totalEnergyWs / 3600f;
            summary.CpuEnergyWh = cpuEnergyWs / 3600f;
            summary.GpuEnergyWh = gpuEnergyWs / 3600f;
            summary.BatteryDischargeEnergyWh = batteryEnergyWs / 3600f;

            // Battery Run Time Estimation
            if (summary.IsOnBattery && summary.AverageTotalWatts > 1.0f)
            {
                // Query current battery charge capacity in Wh
                double chargePercent = HardwareControl.GetBatteryChargePercentage();
                double designWh = 90.0; // G513RM has 90Wh battery
                double remainingWh = (chargePercent / 100.0) * designWh;
                summary.EstimatedBatteryMinutes = (remainingWh / summary.AverageTotalWatts) * 60.0;
            }

            // Build Top Processes List
            var processList = new List<ProcessSummary>();
            foreach (var kv in appAccumulators)
            {
                string name = kv.Key;
                var acc = kv.Value;

                float avgWatts = acc.SumWatts / matchingSamples.Count;
                float energyWh = acc.EnergyWs / 3600f;
                float share = summary.TotalEnergyWh > 0.001f ? (energyWh / summary.TotalEnergyWh) * 100f : 0f;

                string tag;
                if (acc.UsedCpu && acc.UsedGpu) tag = "CPU + dGPU";
                else if (acc.UsedGpu) tag = "dGPU";
                else tag = "CPU";

                bool isActive = latest.Processes.ContainsKey(name);

                processList.Add(new ProcessSummary
                {
                    Name = name,
                    ProcessCount = acc.LastCount,
                    CurrentWatts = acc.LastWatts,
                    AverageWatts = avgWatts,
                    PeakWatts = acc.PeakWatts,
                    TotalEnergyWh = energyWh,
                    PercentShare = share,
                    EngineTag = tag,
                    IsActive = isActive
                });
            }

            // Sort by Total Energy Wh descending
            summary.TopProcesses = processList
                .OrderByDescending(p => p.TotalEnergyWh)
                .ThenByDescending(p => p.AverageWatts)
                .ToList();

            // Pull recent 60s for trend sparkline
            var trendCutoff = now.AddSeconds(-60);
            summary.TrendData = matchingSamples
                .Where(s => s.Timestamp >= trendCutoff)
                .Select(s => (s.Timestamp, s.TotalWatts, s.CpuWatts, s.GpuWatts))
                .ToList();

            return summary;
        }
    }
}
