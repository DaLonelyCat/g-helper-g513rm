using GHelper.Helpers;
using GHelper.Mode;
using Microsoft.Win32;
using System.Management;
using System.Text.Json;
using System.Text.RegularExpressions;

public static class AppConfig
{
    private static string configFile;
    private static string fallbackConfigFile;

    private static Dictionary<string, object> config = new Dictionary<string, object>();
    private static System.Timers.Timer timer = new System.Timers.Timer(2000) { AutoReset = false };
    private static readonly object configLock = new();

    private static readonly JsonSerializerOptions LenientOptions = new()
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private static readonly Regex KeyValueRegex = new(
        @"""((?:\\.|[^""\\])*)""\s*:\s*(""(?:\\.|[^""\\])*""|-?\d+(?:\.\d+)?|true|false|null)");

    static AppConfig()
    {
        string configName = "config.json";
        string appPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GHelper");
        string startupConfig = Path.Combine(Application.StartupPath.Trim('\\'), configName);

        fallbackConfigFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "GHelper", configName);

        configFile = File.Exists(startupConfig) ? startupConfig
        : ProcessHelper.IsRunningAsSystem() && File.Exists(fallbackConfigFile) ? fallbackConfigFile
        : Path.Combine(appPath, configName);

        Directory.CreateDirectory(Path.GetDirectoryName(configFile)!);

        if (!TryLoadConfig(configFile) && !TryRecoverConfig(configFile) && !TryLoadConfig(configFile + ".bak") && !TryLoadConfig(fallbackConfigFile)) Init();

        timer.Elapsed += Timer_Elapsed;
    }

    private static bool TryLoadConfig(string path)
    {
        if (!File.Exists(path)) return false;
        try
        {
            config = JsonSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(path), LenientOptions)!;
            Logger.WriteLine($"Config loaded from {path}");
            return true;
        }
        catch (Exception ex)
        {
            Logger.WriteLine($"Broken config {path}: {ex.Message}");
            return false;
        }
    }

    private static bool TryRecoverConfig(string path)
    {
        if (!File.Exists(path)) return false;
        try
        {
            var pairs = new Dictionary<string, string>();
            foreach (Match m in KeyValueRegex.Matches(File.ReadAllText(path)))
                pairs["\"" + m.Groups[1].Value + "\""] = m.Groups[2].Value;

            if (pairs.Count == 0) return false;

            string rebuilt = "{" + string.Join(",", pairs.Select(p => p.Key + ":" + p.Value)) + "}";
            config = JsonSerializer.Deserialize<Dictionary<string, object>>(rebuilt, LenientOptions)!;
            Logger.WriteLine($"Recovered {pairs.Count} values from broken config {path}");
            return true;
        }
        catch (Exception ex)
        {
            Logger.WriteLine($"Config recovery failed {path}: {ex.Message}");
            return false;
        }
    }

    private static void Timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        timer.Stop();
        string jsonString;
        lock (configLock) jsonString = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        try
        {
            WriteAtomic(configFile, jsonString);
            SyncFallbackConfig();
        }
        catch (Exception ex) { Logger.WriteLine("Config write failed: " + ex.Message); }
    }

    private static void WriteAtomic(string path, string content)
    {
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, content);
        using (var fs = new FileStream(tmp, FileMode.Open, FileAccess.Write))
            fs.Flush(flushToDisk: true);
        if (File.Exists(path))
            File.Replace(tmp, path, path + ".bak");
        else
            File.Move(tmp, path);
    }

    private static void SyncFallbackConfig()
    {
        if (fallbackConfigFile is null || fallbackConfigFile == configFile) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fallbackConfigFile)!);
            File.Copy(configFile, fallbackConfigFile, overwrite: true);
        }
        catch (Exception)
        {
        }
    }

    // G513RM Hardware Identification
    private static readonly Lazy<string> _model =
        new Lazy<string>(LoadModel, LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly Lazy<(string Bios, string ModelShort)> _biosData =
        new Lazy<(string, string)>(LoadBios, LazyThreadSafetyMode.ExecutionAndPublication);

    private static string LoadModel()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("Select * from Win32_ComputerSystem");
            foreach (var obj in searcher.Get())
            {
                using (obj) return obj["Model"]?.ToString() ?? "ROG Strix G513RM";
            }
        }
        catch (Exception ex)
        {
            Logger.WriteLine(ex.Message);
        }
        return "ROG Strix G513RM";
    }

    private static (string Bios, string ModelShort) LoadBios()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_BIOS");
            foreach (var obj in searcher.Get())
            {
                using (obj)
                {
                    string raw = obj["SMBIOSBIOSVersion"]?.ToString() ?? "G513RM.327";
                    string[] parts = raw.Split('.');
                    return parts.Length > 1 ? (parts[1], parts[0]) : (string.Empty, raw);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.WriteLine(ex.Message);
        }
        return ("327", "G513RM");
    }

    public static string GetModel() => _model.Value;

    public static (string, string) GetBiosAndModel() => (_biosData.Value.Bios, _biosData.Value.ModelShort);

    public static string GetModelShort() => "ROG Strix G513RM";

    public static bool ContainsModel(string contains)
        => _model.Value.Contains(contains, StringComparison.OrdinalIgnoreCase);

    private static void Init()
    {
        config = new Dictionary<string, object>();
        config["performance_mode"] = 0;
        string jsonString = JsonSerializer.Serialize(config);
        File.WriteAllText(configFile, jsonString);
    }

    public static bool Exists(string name)
    {
        lock (configLock) return config.ContainsKey(name);
    }

    public static int Get(string name, int empty = -1)
    {
        lock (configLock)
            return config.TryGetValue(name, out var val) && int.TryParse(val?.ToString(), out int result)
            ? result : empty;
    }

    public static bool Is(string name)
    {
        return Get(name) == 1;
    }

    public static bool IsNotFalse(string name)
    {
        return Get(name) != 0;
    }

    public static bool IsOnBattery(string zone)
    {
        return Get(zone + "_bat", Get(zone)) != 0;
    }

    public static string? GetString(string name, string? empty = null)
    {
        lock (configLock)
            return config.TryGetValue(name, out var val) ? val?.ToString() : empty;
    }

    private static void Write()
    {
        timer.Stop();
        timer.Start();
    }

    public static void Set(string name, int value)
    {
        lock (configLock) config[name] = value;
        Write();
    }

    public static void Set(string name, string value)
    {
        lock (configLock) config[name] = value;
        Write();
    }

    public static void Remove(string name)
    {
        lock (configLock) config.Remove(name);
        Write();
    }

    public static void RemoveMode(string name)
    {
        Remove(name + "_" + Modes.GetCurrent());
    }

    public static string GgetParamName(AsusFan device, string paramName = "fan_profile")
    {
        int mode = Modes.GetCurrent();
        string name = device switch
        {
            AsusFan.GPU => "gpu",
            AsusFan.Mid => "mid",
            _ => "cpu"
        };

        return paramName + "_" + name + "_" + mode;
    }

    public static byte[] GetFanConfig(AsusFan device)
    {
        string? curveString = GetString(GgetParamName(device));
        byte[] curve = { };

        if (curveString is not null)
            curve = StringToBytes(curveString);

        return curve;
    }

    public static void SetFanConfig(AsusFan device, byte[] curve)
    {
        string bitCurve = BitConverter.ToString(curve);
        Set(GgetParamName(device), bitCurve);
    }

    public static byte[] StringToBytes(string str)
    {
        string[] arr = str.Split('-');
        byte[] array = new byte[arr.Length];
        for (int i = 0; i < arr.Length; i++) array[i] = Convert.ToByte(arr[i], 16);
        return array;
    }

    public static byte[] GetDefaultCurve(AsusFan device)
    {
        int mode = Modes.GetCurrent();
        return G513Presets.GetCalibratedCurve(mode, device);
    }

    public static string? GetModeString(string name)
    {
        return GetString(name + "_" + Modes.GetCurrent());
    }

    public static int GetMode(string name, int empty = -1)
    {
        return Get(name + "_" + Modes.GetCurrent(), empty);
    }

    public static bool IsMode(string name)
    {
        return Get(name + "_" + Modes.GetCurrent()) == 1;
    }

    public static void SetMode(string name, int value)
    {
        Set(name + "_" + Modes.GetCurrent(), value);
    }

    public static void SetMode(string name, string value)
    {
        Set(name + "_" + Modes.GetCurrent(), value);
    }

    // Hardcoded ROG Strix G513RM Hardware Profile Flags
    public static bool IsStrix() => true;
    public static bool IsROG() => true;
    public static bool IsASUS() => true;
    public static bool IsBacklightZones() => true;
    public static bool IsStrix4ZoneFlipped() => true;
    public static bool IsFanRequired() => true;
    public static bool IsNumberPad() => true;

    public static bool IsAlly() => false;
    public static bool IsXboxAlly() => false;
    public static bool IsAuraSync() => false;
    public static bool IsKeyboardAuraSync() => false;
    public static bool IsHeadsetAuraSync() => false;
    public static bool NoMKeys() => false;
    public static bool IsARCNM() => false;
    public static bool IsTUF() => false;
    public static bool IsProArt() => false;
    public static bool IsVivoZenbook() => false;
    public static bool IsVivoZenPro() => false;
    public static bool IsHardwareFnLock() => false;
    public static bool SwappedBrightness() => false;
    public static bool IsDUO() => false;
    public static bool IsM4Button() => false;
    public static bool NoAura() => false;
    public static bool MediaKeys() => false;
    public static bool IsWhite() => Is("no_rgb");
    public static bool IsSleepBacklight() => false;
    public static bool IsAnimeMatrix() => false;
    public static bool IsSlash() => false;
    public static bool IsSlashLong() => false;
    public static bool IsInvertedFNLock() => false;
    public static bool IsOLED() => false;
    public static bool IsNoOverdrive() => Is("no_overdrive");
    public static bool IsEcoBootFix() => false;
    public static bool IsELMB() => false;
    public static bool IsStandardForceFix() => false;
    public static bool IsLampArray() => false;
    public static bool IsHardwareHotkeys() => false;
    public static bool NoWMI() => false;
    public static bool IsNoDirectRGB() => false;
    public static bool IsStrixNumpad() => false;
    public static bool IsZ1325() => false;
    public static bool IsZ13() => false;
    public static bool IsDetachableKeyboard() => false;
    public static bool HasRearLight() => false;
    public static bool IsPZ13() => false;
    public static bool IsS17() => false;
    public static bool HasTabletMode() => false;
    public static bool IsX13() => false;
    public static bool IsG14AMD() => false;
    public static bool DynamicBoost5() => false;
    public static bool DynamicBoost15() => false;
    public static bool DynamicBoost20() => false;
    public static bool IsAdvantageEdition() => false;
    public static bool IsAlwaysUltimate() => false;

    public static bool IsApplyPower() => IsMode("auto_apply_power");
    public static bool IsApplyFans() => IsMode("auto_apply");
    public static bool IsApplyUV() => IsMode("auto_uv");

    public static bool IsManualModeRequired() => IsApplyPower() && Is("manual_mode");
    public static bool IsResetRequired() => false;
    public static bool IsCPULight() => false;
    public static bool IsPowerRequired() => false;
    public static bool IsModeReapply() => IsNotFalse("mode_reapply");
    public static bool IsStandardModeFix() => Is("shutdown_gpu");
    public static bool IsShutdownReset() => Is("shutdown_reset");
    public static bool IsNVPlatform() => Is("nv_platform");
    public static bool IsForceSetGPUMode() => Is("gpu_mode_force_set");
    public static bool IsAMDiGPU() => false;
    public static bool NoGpu() => Is("no_gpu");
    public static bool IsOnlyAIMAX() => false;
    public static bool IsHardwareTouchpadToggle() => false;
    public static bool IsIntelHX() => false;
    public static bool Is8Ecores() => false;
    public static bool IsNoFNV() => false;
    public static bool IsDesktop() => false;
    public static bool IsBWIcon() => Is("bw_icon");
    public static bool IsOverlay() => false;
    public static bool IsOverlayGameOnly() => false;
    public static bool IsStopAC() => Is("stop_ac");
    public static bool IsChargeLimit6080() => false;
    public static bool IsDynamicLighting() => false;
    public static bool IsDynamicLightingOnly() => false;
    public static bool IsDynamicLightingInit() => false;
    public static bool IsForceMiniled() => false;
    public static bool IsKeystone() => false;
    public static bool IsSleepReset() => Is("sleep_reset");
    public static bool SaveDimming() => Is("save_dimming");
    public static bool IsAutoStatusLed() => Is("auto_status_led");
    public static bool IsClampFanDots() => IsNotFalse("fan_clamp");
    public static bool IsAutoASPM() => IsNotFalse("aspm");
    public static bool IsAutoStandbyNetworking() => IsNotFalse("standby_networking");
}
