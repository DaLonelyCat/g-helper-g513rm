using GHelper.Helpers;
using Microsoft.Win32;
using System.Diagnostics;

namespace GHelper.Display
{
    public static class ScreenControl
    {
        public const int MAX_REFRESH = 1000;
        public static int MIN_RATE = AppConfig.Get("min_rate", 60);
        public static int MAX_RATE = AppConfig.Get("max_rate");

        public static int GetMaxRate(string? laptopScreen)
        {
            if (MAX_RATE > 0) return MAX_RATE;
            else return ScreenNative.GetMaxRefreshRate(laptopScreen);
        }

        // Strict Manual Display Policy for G513RM Edition:
        // Never auto-switch refresh rates on AC/Battery or GPU transitions.
        public static void AutoScreen(bool force = false)
        {
            AppConfig.Set("screen_auto", 0);
            InitScreen();
        }

        public static void SetAutoRefresh(int auto)
        {
            AppConfig.Set("screen_auto", 0);
            SetAsusRefreshFlag(0);
        }

        public static void SetAsusRefreshFlag(int value)
        {
            if (!ProcessHelper.IsUserAdministrator()) return;
            const string keyPath = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{8714A8D1-0F08-4681-9DF6-A8C4607A58B4}";
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(keyPath, writable: true);
                key?.SetValue("RefreshFlag", value, RegistryValueKind.DWord);
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"Failed to set RefreshFlag: {ex.Message}");
            }
        }

        public static void ToggleScreenRate()
        {
            var laptopScreen = ScreenNative.FindLaptopScreen(true);
            var refreshRate = ScreenNative.GetRefreshRate(laptopScreen);
            if (refreshRate < 0) return;

            ScreenNative.SetRefreshRate(laptopScreen, refreshRate > MIN_RATE ? MIN_RATE : GetMaxRate(laptopScreen));
            InitScreen();
        }

        public static void SetScreen(int frequency = -1, int overdrive = -1, int miniled = -1)
        {
            var laptopScreen = ScreenNative.FindLaptopScreen(true);
            var refreshRate = ScreenNative.GetRefreshRate(laptopScreen);

            if (refreshRate < 0) return;

            if (frequency >= MAX_REFRESH)
            {
                frequency = GetMaxRate(laptopScreen);
            }

            if (frequency > 0 && frequency != refreshRate)
            {
                ScreenNative.SetRefreshRate(laptopScreen, frequency);
            }

            if (Program.acpi.IsOverdriveSupported() && overdrive >= 0)
            {
                if (AppConfig.IsNoOverdrive()) overdrive = 0;
                if (overdrive != Program.acpi.DeviceGet(AsusACPI.ScreenOverdrive))
                {
                    Program.acpi.DeviceSet(AsusACPI.ScreenOverdrive, overdrive, "ScreenOverdrive");
                }
            }

            SetMiniled(miniled);

            InitScreen();
        }

        public static void SetMiniled(int miniled = -1)
        {
            if (miniled >= 0)
            {
                if (Program.acpi.IsSupported(AsusACPI.ScreenMiniled1))
                    Program.acpi.DeviceSet(AsusACPI.ScreenMiniled1, miniled, "Miniled1");
                else
                {
                    Program.acpi.DeviceSet(AsusACPI.ScreenMiniled2, miniled, "Miniled2");
                    Thread.Sleep(100);
                }
            }
        }

        public static void InitMiniled()
        {
        }

        public static void InitELMB()
        {
        }

        public static void InitOptimalBrightness()
        {
            int optimalBrightness = AppConfig.Get("optimal_brightness");
            if (optimalBrightness >= 0) SetOptimalBrightness(optimalBrightness);
        }

        public static void SetOptimalBrightness(int status)
        {
            AppConfig.Set("optimal_brightness", status);
            if (status == 2) status = SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline ? 1 : 0;
            Program.acpi.DeviceSet(AsusACPI.ScreenOptimalBrightness, status, "Optimal Brightness");
        }

        public static int GetOptimalBrightness()
        {
            return Program.acpi.DeviceGet(AsusACPI.ScreenOptimalBrightness);
        }

        public static void ToogleFHD()
        {
        }

        public static void SetHDRControl(int status = -1)
        {
            if (status >= 0)
            {
                AppConfig.Set("hdr_control", status);
                Program.acpi.DeviceSet(AsusACPI.ScreenHDRControl, status, "HDR Control");
            }
        }

        public static void ToogleHDRControl()
        {
            int hdrControl = Program.acpi.DeviceGet(AsusACPI.ScreenHDRControl);
            Logger.WriteLine($"HDR Control Toggle: {hdrControl}");
            SetHDRControl((hdrControl == 1) ? 1 : 0);
            Thread.Sleep(200);
            InitScreen();
        }

        public static string ToogleMiniled()
        {
            return string.Empty;
        }

        public static void InitScreen()
        {
            var laptopScreen = ScreenNative.FindLaptopScreen();
            int frequency = ScreenNative.GetRefreshRate(laptopScreen);
            int maxFrequency = GetMaxRate(laptopScreen);

            if (maxFrequency > 0) AppConfig.Set("max_frequency", maxFrequency);
            else maxFrequency = AppConfig.Get("max_frequency");

            bool screenAuto = false;
            bool overdriveSetting = Program.acpi.IsOverdriveSupported() && !AppConfig.IsNoOverdrive();

            int overdrive = overdriveSetting ? Program.acpi.DeviceGet(AsusACPI.ScreenOverdrive) : 0;

            bool hdr = false;
            bool acm = false;

            try
            {
                hdr = ScreenCCD.GetHDRStatus(out acm);
            }
            catch (Exception ex)
            {
                Logger.WriteLine(ex.Message);
            }

            bool screenEnabled = (frequency >= 0);

            int hdrControl = Program.acpi.DeviceGet(AsusACPI.ScreenHDRControl);

            AppConfig.Set("frequency", frequency);
            AppConfig.Set("overdrive", overdrive);

            Program.settingsForm.Invoke(delegate
            {
                Program.settingsForm.VisualiseScreen(
                    screenEnabled: screenEnabled,
                    screenAuto: screenAuto,
                    frequency: frequency,
                    maxFrequency: maxFrequency,
                    overdrive: overdrive,
                    overdriveSetting: overdriveSetting,
                    miniled1: -1,
                    miniled2: -1,
                    hdr: hdr,
                    acm: acm,
                    fhd: -1,
                    hdrControl: hdrControl
                );
            });
        }
    }
}
