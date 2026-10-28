using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GHelper.Helpers
{
    public static class G513Telemetry
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool EmptyWorkingSet(IntPtr hProcess);

        [DllImport("ntdll.dll")]
        private static extern uint NtSetSystemInformation(int infoClass, ref int infoData, int infoLength);

        private const int SystemMemoryListInformation = 80;
        private const int MemoryEmptyWorkingSets = 2;
        private const int MemoryPurgeStandbyList = 4;

        private static bool _powerInitialized = false;

        public struct TelemetrySnapshot
        {
            public string CpuHeader;
            public string CpuDetail;
            public string GpuHeader;
            public string GpuDetail;
            public string RamHeader;
            public string RamDetail;
            public int RamPercent;
        }

        public static TelemetrySnapshot Capture()
        {
            if (!_powerInitialized)
            {
                _powerInitialized = true;
                HardwareControl.InitCPUPowerAsync();
            }

            // 1. CPU Telemetry (AMD Ryzen 7 6800H)
            float? cpuTemp = HardwareControl.cpuTemp ?? HardwareControl.GetCPUTemp();
            float? cpuWatts = HardwareControl.GetCPUPower();
            int cpuRpm = Program.acpi != null ? Math.Max(0, Program.acpi.GetFan(AsusFan.CPU) * 100) : 0;

            string cpuTempStr = cpuTemp is > 0 ? $"{Math.Round(cpuTemp.Value)}°C" : "--°C";
            string cpuWattStr = cpuWatts is > 0 ? $"{cpuWatts.Value:F0}W" : $"{AppConfig.GetMode("limit_cpu", 45)}W SPL";
            string cpuRpmStr = $"{cpuRpm} RPM";

            // 2. GPU Telemetry (NVIDIA GeForce RTX 3060 Laptop GPU)
            float? gpuTemp = HardwareControl.gpuTemp;
            float? gpuWatts = HardwareControl.GetGPUPower();
            var vram = HardwareControl.GpuControl?.GetVramInfo();
            int gpuRpm = Program.acpi != null ? Math.Max(0, Program.acpi.GetFan(AsusFan.GPU) * 100) : 0;

            string gpuHeader;
            string gpuDetail;
            if (gpuTemp is null || gpuTemp <= 0)
            {
                gpuHeader = "RTX 3060: Eco";
                gpuDetail = $"0W | {gpuRpm} RPM";
            }
            else
            {
                string wattText = gpuWatts is > 0 ? $"{gpuWatts.Value:F0}W" : "dGPU";
                gpuHeader = $"RTX 3060: {Math.Round(gpuTemp.Value)}°C";
                gpuDetail = $"{wattText} | {gpuRpm} RPM";
            }

            // 3. System Memory Telemetry (32 GB DDR5)
            var memStatus = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            string ramHeader = "RAM: 32 GB";
            string ramDetail = "[Purge RAM]";
            int ramPct = 0;

            if (GlobalMemoryStatusEx(ref memStatus) && memStatus.ullTotalPhys > 0)
            {
                double totalGb = memStatus.ullTotalPhys / (1024.0 * 1024.0 * 1024.0);
                double usedGb = (memStatus.ullTotalPhys - memStatus.ullAvailPhys) / (1024.0 * 1024.0 * 1024.0);
                ramPct = (int)memStatus.dwMemoryLoad;
                ramHeader = $"RAM: {usedGb:F1}/{totalGb:F0}GB";
                ramDetail = $"{ramPct}% [Purge]";
            }

            return new TelemetrySnapshot
            {
                CpuHeader = $"6800H: {cpuTempStr}",
                CpuDetail = $"{cpuWattStr} | {cpuRpmStr}",
                GpuHeader = gpuHeader,
                GpuDetail = gpuDetail,
                RamHeader = ramHeader,
                RamDetail = ramDetail,
                RamPercent = ramPct
            };
        }

        public static string PurgeStandbyAndWorkingSet()
        {
            var before = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            GlobalMemoryStatusEx(ref before);

            int trimmedProcs = 0;
            foreach (Process proc in Process.GetProcesses())
            {
                try
                {
                    if (EmptyWorkingSet(proc.Handle)) trimmedProcs++;
                }
                catch { }
                finally
                {
                    proc.Dispose();
                }
            }

            try
            {
                int cmdWork = MemoryEmptyWorkingSets;
                NtSetSystemInformation(SystemMemoryListInformation, ref cmdWork, sizeof(int));
                int cmdStandby = MemoryPurgeStandbyList;
                NtSetSystemInformation(SystemMemoryListInformation, ref cmdStandby, sizeof(int));
            }
            catch { }

            MemoryHelper.TrimAfter();

            var after = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            if (GlobalMemoryStatusEx(ref after) && after.ullAvailPhys > before.ullAvailPhys)
            {
                long freedMb = (long)((after.ullAvailPhys - before.ullAvailPhys) / (1024 * 1024));
                Logger.WriteLine($"RAM Purge reclaimed {freedMb} MB across {trimmedProcs} processes.");
                return $"Freed {freedMb} MB";
            }

            Logger.WriteLine($"RAM Purge trimmed {trimmedProcs} working sets.");
            return $"Trimmed ({trimmedProcs} apps)";
        }
    }
}
