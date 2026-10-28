using GHelper.Helpers;

namespace GHelper.Mode
{
    public static class G513Presets
    {
        public const int ModeSilent = 2;
        public const int ModeBalanced = 0;
        public const int ModeTurbo = 1;
        public const int ModeSweetSpot = 3;

        // 8 temp points (hex °C) + 8 fan duty points (hex 0-100%)
        // Silent: 30°C..90°C -> 0%, 0%, 10%, 18%, 26%, 34%, 40%, 46%
        public const string CurveSilentCpu = "1E-32-3C-42-48-4E-54-5A-00-00-0A-12-1A-22-28-2E";
        public const string CurveSilentGpu = "1E-32-3C-42-48-4E-54-5A-00-00-0C-14-1C-24-2A-30";

        // Balanced: 45°C..90°C -> 0%, 14%, 22%, 30%, 38%, 46%, 55%, 68%
        public const string CurveBalancedCpu = "2D-34-3C-42-48-4E-54-5A-00-0E-16-1E-26-2E-37-44";
        public const string CurveBalancedGpu = "2D-34-3C-42-48-4E-54-5A-00-10-18-22-2A-32-3B-48";

        // Sweet Spot (40W 6800H + 140W RTX 3060): 45°C..90°C -> 0%, 18%, 28%, 38%, 48%, 58%, 68%, 76%
        public const string CurveSweetSpotCpu = "2D-34-3C-41-46-4B-50-5A-00-12-1C-26-30-3A-44-4C";
        public const string CurveSweetSpotGpu = "2D-34-3C-41-46-4B-50-5A-00-14-20-2A-34-40-48-52";

        // Turbo (70W 6800H + 140W RTX 3060): 40°C..90°C -> 18%, 28%, 38%, 48%, 60%, 72%, 84%, 92%
        public const string CurveTurboCpu = "28-32-3A-40-46-4C-52-5A-12-1C-26-30-3C-48-54-5C";
        public const string CurveTurboGpu = "28-32-3A-40-46-4C-52-5A-14-20-2A-34-40-4C-58-60";

        public static void EnsureSeeded(bool forceReset = false)
        {
            if (!forceReset && AppConfig.Is("g513rm_presets_seeded"))
            {
                if (!Modes.Exists(ModeSweetSpot))
                {
                    SeedSweetSpot();
                }
                return;
            }

            SeedSilent();
            SeedBalanced();
            SeedTurbo();
            SeedSweetSpot();

            AppConfig.Set("g513rm_presets_seeded", 1);
            Logger.WriteLine("Calibrated ROG Strix G513RM (Ryzen 7 6800H + RTX 3060) presets seeded.");
        }

        public static void SeedSilent()
        {
            int m = ModeSilent;
            AppConfig.Set($"limit_total_{m}", 25);      // 25W 6800H SPL
            AppConfig.Set($"limit_cpu_{m}", 25);        // 25W CPU B0
            AppConfig.Set($"limit_slow_{m}", 35);       // 35W sPPT
            AppConfig.Set($"limit_fast_{m}", 45);       // 45W fPPT
            AppConfig.Set($"cpu_temp_{m}", 75);         // 75°C CPU Cap
            AppConfig.Set($"gpu_boost_{m}", 5);         // 5W Dynamic Boost
            AppConfig.Set($"gpu_temp_{m}", 75);         // 75°C GPU Target
            AppConfig.Set($"gpu_core_{m}", 0);
            AppConfig.Set($"gpu_memory_{m}", 0);
            AppConfig.Set($"fan_profile_cpu_{m}", CurveSilentCpu);
            AppConfig.Set($"fan_profile_gpu_{m}", CurveSilentGpu);
            AppConfig.Set($"auto_apply_{m}", 1);
            AppConfig.Set($"auto_apply_power_{m}", 1);
            AppConfig.Set($"auto_uv_{m}", 1);
        }

        public static void SeedBalanced()
        {
            int m = ModeBalanced;
            AppConfig.Set($"limit_total_{m}", 45);      // 45W 6800H SPL
            AppConfig.Set($"limit_cpu_{m}", 45);        // 45W CPU B0
            AppConfig.Set($"limit_slow_{m}", 65);       // 65W sPPT
            AppConfig.Set($"limit_fast_{m}", 80);       // 80W fPPT
            AppConfig.Set($"cpu_temp_{m}", 85);         // 85°C CPU Cap
            AppConfig.Set($"gpu_boost_{m}", 15);        // 15W Dynamic Boost (130W GPU)
            AppConfig.Set($"gpu_temp_{m}", 82);         // 82°C GPU Target
            AppConfig.Set($"gpu_core_{m}", 50);         // +50 MHz Core
            AppConfig.Set($"gpu_memory_{m}", 100);      // +100 MHz VRAM
            AppConfig.Set($"fan_profile_cpu_{m}", CurveBalancedCpu);
            AppConfig.Set($"fan_profile_gpu_{m}", CurveBalancedGpu);
            AppConfig.Set($"auto_apply_{m}", 1);
            AppConfig.Set($"auto_apply_power_{m}", 1);
            AppConfig.Set($"auto_uv_{m}", 1);
        }

        public static void SeedTurbo()
        {
            int m = ModeTurbo;
            AppConfig.Set($"limit_total_{m}", 70);      // 70W 6800H SPL
            AppConfig.Set($"limit_cpu_{m}", 70);        // 70W CPU B0
            AppConfig.Set($"limit_slow_{m}", 90);       // 90W sPPT
            AppConfig.Set($"limit_fast_{m}", 115);      // 115W fPPT
            AppConfig.Set($"cpu_temp_{m}", 92);         // 92°C CPU Cap
            AppConfig.Set($"gpu_boost_{m}", 25);        // 25W Max Dynamic Boost (140W RTX 3060)
            AppConfig.Set($"gpu_temp_{m}", 86);         // 86°C GPU Target
            AppConfig.Set($"gpu_core_{m}", 100);        // +100 MHz Core
            AppConfig.Set($"gpu_memory_{m}", 200);      // +200 MHz VRAM
            AppConfig.Set($"fan_profile_cpu_{m}", CurveTurboCpu);
            AppConfig.Set($"fan_profile_gpu_{m}", CurveTurboGpu);
            AppConfig.Set($"auto_apply_{m}", 1);
            AppConfig.Set($"auto_apply_power_{m}", 1);
            AppConfig.Set($"auto_uv_{m}", 1);
        }

        public static void SeedSweetSpot()
        {
            int m = ModeSweetSpot;
            AppConfig.Set($"mode_base_{m}", AsusACPI.PerformanceTurbo);
            AppConfig.Set($"mode_name_{m}", "Sweet Spot");
            AppConfig.Set($"limit_total_{m}", 40);      // 40W 6800H Sweet Spot (low heat, max gaming efficiency)
            AppConfig.Set($"limit_cpu_{m}", 40);        // 40W CPU B0
            AppConfig.Set($"limit_slow_{m}", 55);       // 55W sPPT
            AppConfig.Set($"limit_fast_{m}", 75);       // 75W fPPT
            AppConfig.Set($"cpu_temp_{m}", 83);         // 83°C CPU Cap
            AppConfig.Set($"gpu_boost_{m}", 25);        // Full 25W Dynamic Boost (140W RTX 3060)
            AppConfig.Set($"gpu_temp_{m}", 83);         // 83°C GPU Target
            AppConfig.Set($"gpu_core_{m}", 120);        // +120 MHz Core
            AppConfig.Set($"gpu_memory_{m}", 250);      // +250 MHz VRAM
            AppConfig.Set($"fan_profile_cpu_{m}", CurveSweetSpotCpu);
            AppConfig.Set($"fan_profile_gpu_{m}", CurveSweetSpotGpu);
            AppConfig.Set($"auto_apply_{m}", 1);
            AppConfig.Set($"auto_apply_power_{m}", 1);
            AppConfig.Set($"auto_uv_{m}", 1);
        }

        public static byte[] GetCalibratedCurve(int mode, AsusFan device)
        {
            bool isGpu = device == AsusFan.GPU;
            string hex = mode switch
            {
                ModeSilent => isGpu ? CurveSilentGpu : CurveSilentCpu,
                ModeTurbo => isGpu ? CurveTurboGpu : CurveTurboCpu,
                ModeSweetSpot => isGpu ? CurveSweetSpotGpu : CurveSweetSpotCpu,
                _ => isGpu ? CurveBalancedGpu : CurveBalancedCpu
            };
            return AppConfig.StringToBytes(hex);
        }

        public static string GetModeSummary(int mode)
        {
            int cpuW = AppConfig.Get($"limit_total_{mode}", mode == ModeSilent ? 25 : mode == ModeTurbo ? 70 : mode == ModeSweetSpot ? 40 : 45);
            int gpuBoost = AppConfig.Get($"gpu_boost_{mode}", mode == ModeSilent ? 5 : mode == ModeBalanced ? 15 : 25);
            int gpuTotal = 115 + gpuBoost;
            int cpuTemp = AppConfig.Get($"cpu_temp_{mode}", mode == ModeSilent ? 75 : mode == ModeTurbo ? 92 : mode == ModeSweetSpot ? 83 : 85);
            return $"{Modes.GetName(mode)}: 6800H {cpuW}W ({cpuTemp}°C) | RTX 3060 {gpuTotal}W";
        }
    }
}
