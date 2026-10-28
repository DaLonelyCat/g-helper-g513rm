using GHelper.Helpers;
using System.Diagnostics;

namespace GHelper.Mode
{
    public static class ProcessProfileWatcher
    {
        private static System.Timers.Timer? _timer;
        private static bool _isGameActive = false;
        private static int _savedBaseProfile = G513Presets.ModeBalanced;
        private static int _consecutiveHighGpuTicks = 0;

        private static readonly HashSet<string> KnownGameEnginesAndLaunchTargets = new(StringComparer.OrdinalIgnoreCase)
        {
            "cs2", "valorant-win64-shipping", "dota2", "eldenring", "cyberpunk2077",
            "witcher3", "r5apex", "overwatch", "helldivers2", "bg3", "bg3_dx11",
            "forzahorizon5", "gta5", "rdr2", "palworld-win64-shipping", "MonsterHunterWorld",
            "MonsterHunterWilds", "blackmythwukong", " b1-win64-shipping", "genshinimpact",
            "starrail", "wuwa", "client-win64-shipping", "cod", "bf2042", " marvel-win64-shipping",
            "poe2", "pathofexile", "diablo iv", "balatro", "rustclient", "escapefromtarkov",
            "javaw", "minecraft.windows", "robloxplayerbeta", "league of legends", "hd-player"
        };

        public static bool IsEnabled => AppConfig.Is("auto_game_profile");

        public static bool IsGameCurrentlyActive => _isGameActive;

        public static void Init()
        {
            _timer?.Dispose();
            _timer = new System.Timers.Timer(3500) { AutoReset = true };
            _timer.Elapsed += OnTick;
            if (IsEnabled) _timer.Start();
        }

        public static void SetEnabled(bool enabled)
        {
            AppConfig.Set("auto_game_profile", enabled ? 1 : 0);
            if (_timer == null) Init();

            if (enabled)
            {
                _timer!.Start();
                Logger.WriteLine("Smart Auto-Profile Watcher enabled.");
            }
            else
            {
                _timer!.Stop();
                if (_isGameActive)
                {
                    _isGameActive = false;
                    Program.modeControl.SetPerformanceMode(_savedBaseProfile, true);
                }
                Logger.WriteLine("Smart Auto-Profile Watcher disabled.");
            }
        }

        private static void OnTick(object? sender, System.Timers.ElapsedEventArgs e)
        {
            if (!IsEnabled) return;

            try
            {
                bool detected = DetectGameOrHeavy3D(out string reason);

                if (detected && !_isGameActive)
                {
                    int current = Modes.GetCurrent();
                    int target = AppConfig.Get("auto_game_target_mode", G513Presets.ModeSweetSpot);
                    if (!Modes.Exists(target)) target = G513Presets.ModeTurbo;

                    _isGameActive = true;
                    if (current != target && current != G513Presets.ModeTurbo)
                    {
                        _savedBaseProfile = current;
                        Logger.WriteLine($"Auto-Profile: Game/3D detected ({reason}) -> Switching from Mode {current} to {Modes.GetName(target)}");
                        Program.modeControl.SetPerformanceMode(target, true);
                    }
                }
                else if (!detected && _isGameActive)
                {
                    _isGameActive = false;
                    int current = Modes.GetCurrent();
                    int target = AppConfig.Get("auto_game_target_mode", G513Presets.ModeSweetSpot);
                    if (current == target)
                    {
                        Logger.WriteLine($"Auto-Profile: Game/3D exited -> Restoring Mode {Modes.GetName(_savedBaseProfile)}");
                        Program.modeControl.SetPerformanceMode(_savedBaseProfile, true);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine("ProcessProfileWatcher tick error: " + ex.Message);
            }
        }

        private static bool DetectGameOrHeavy3D(out string reason)
        {
            reason = string.Empty;

            // 1. Check custom user list in config ("game_processes": "exe1,exe2")
            string? customList = AppConfig.GetString("game_processes");
            HashSet<string>? customSet = null;
            if (!string.IsNullOrWhiteSpace(customList))
            {
                customSet = new HashSet<string>(
                    customList.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                    StringComparer.OrdinalIgnoreCase);
            }

            foreach (Process p in Process.GetProcesses())
            {
                try
                {
                    string name = p.ProcessName;
                    if (KnownGameEnginesAndLaunchTargets.Contains(name) ||
                        name.EndsWith("-Win64-Shipping", StringComparison.OrdinalIgnoreCase) ||
                        (customSet != null && customSet.Contains(name)))
                    {
                        reason = name;
                        return true;
                    }
                }
                catch { }
                finally
                {
                    p.Dispose();
                }
            }

            // 2. Check sustained RTX 3060 3D load (> 35% GPU load + > 45W GPU power for 2 consecutive ticks)
            float? gpuWatts = HardwareControl.GetGPUPower();
            int? gpuUse = HardwareControl.GpuControl?.GetGpuUse();
            if (gpuUse is > 35 && gpuWatts is > 45f)
            {
                _consecutiveHighGpuTicks++;
                if (_consecutiveHighGpuTicks >= 2)
                {
                    reason = $"RTX 3060 Load {gpuUse}% ({gpuWatts:F0}W)";
                    return true;
                }
            }
            else if (gpuUse is < 15 || gpuWatts is < 25f)
            {
                _consecutiveHighGpuTicks = 0;
            }

            return false;
        }
    }
}
