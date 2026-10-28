using GHelper.Display;
using GHelper.Gpu.NVidia;
using GHelper.Helpers;
using GHelper.USB;
using System.Diagnostics;

namespace GHelper.Gpu
{
    public class GPUModeControl
    {
        SettingsForm settings;

        public static int gpuMode;
        public static bool? gpuExists = null;

        static bool nvRestartPending;

        public GPUModeControl(SettingsForm settingsForm)
        {
            settings = settingsForm;
        }

        public void InitGPUMode()
        {
            if (AppConfig.NoGpu())
            {
                settings.HideGPUModes(false);
                return;
            }

            int eco = Program.acpi.DeviceGet(AsusACPI.GPUEco);
            int mux = Program.acpi.DeviceGet(AsusACPI.GPUMux);

            Logger.WriteLine("Eco flag : " + eco);
            Logger.WriteLine("Mux flag : " + mux);

            settings.VisualiseGPUButtons(eco >= 0, mux >= 0);

            if (mux == 0)
            {
                gpuMode = AsusACPI.GPUModeUltimate;
            }
            else
            {
                if (eco == 1)
                    gpuMode = AsusACPI.GPUModeEco;
                else
                    gpuMode = AsusACPI.GPUModeStandard;

                if (eco < 0 && mux < 0)
                {
                    if (gpuExists is null) gpuExists = Program.acpi.GetFan(AsusFan.GPU) >= 0;
                    settings.HideGPUModes((bool)gpuExists);
                }
            }

            AppConfig.Set("gpu_mode", gpuMode);
            settings.VisualiseGPUMode(gpuMode);

            Aura.CustomRGB.ApplyGPUColor(gpuMode);

            CheckGpuError();
        }

        public void SetGPUMode(int GPUMode, int auto = 0)
        {
            int CurrentGPU = AppConfig.Get("gpu_mode");
            AppConfig.Set("gpu_auto", auto);

            if (CurrentGPU == GPUMode)
            {
                settings.VisualiseGPUMode();
                return;
            }

            var restart = false;
            var changed = false;

            if (CurrentGPU == AsusACPI.GPUModeUltimate)
            {
                DialogResult dialogResult = settings.ShowMessage(Properties.Strings.AlertUltimateOff, Properties.Strings.AlertUltimateTitle, MessageBoxButtons.YesNo);
                if (dialogResult == DialogResult.Yes)
                {
                    Program.acpi.DeviceSet(AsusACPI.GPUMux, 1, "GPUMux");
                    restart = true;
                    changed = true;
                }
            }
            else if (GPUMode == AsusACPI.GPUModeUltimate)
            {
                if (Program.acpi.DeviceGet(AsusACPI.GPUMux) < 0)
                {
                    Logger.WriteLine("Mux not supported");
                    settings.VisualiseGPUMode();
                    return;
                }

                DialogResult dialogResult = settings.ShowMessage(Properties.Strings.AlertUltimateOn, Properties.Strings.AlertUltimateTitle, MessageBoxButtons.YesNo);
                if (dialogResult == DialogResult.Yes)
                {
                    Program.acpi.SetGPUEco(0);
                    Thread.Sleep(500);

                    int eco = Program.acpi.DeviceGet(AsusACPI.GPUEco);
                    Logger.WriteLine("Eco flag : " + eco);
                    if (eco == 1)
                    {
                        settings.VisualiseGPUMode();
                        return;
                    }

                    Program.acpi.DeviceSet(AsusACPI.GPUMux, 0, "GPUMux");
                    restart = true;
                    changed = true;
                }
            }
            else if (GPUMode == AsusACPI.GPUModeEco)
            {
                settings.VisualiseGPUMode(GPUMode);
                SetGPUEco(1);
                changed = true;
            }
            else if (GPUMode == AsusACPI.GPUModeStandard)
            {
                settings.VisualiseGPUMode(GPUMode);
                SetGPUEco(0);
                changed = true;
            }

            if (changed)
            {
                AppConfig.Set("gpu_mode", GPUMode);
            }

            if (restart)
            {
                settings.VisualiseGPUMode();
                Process.Start("shutdown", "/r /t 1");
            }
        }

        public void SetGPUEco(int eco)
        {
            settings.LockGPUModes();

            Task.Run(async () =>
            {
                Program.modeControl.WaitForApply();

                if (eco == 1)
                {
                    HardwareControl.KillGPUApps();
                    HardwareControl.DisposeGpuControl();
                    if (AppConfig.IsNVPlatform()) NvidiaGpuControl.StopNVService();
                }

                Logger.WriteLine($"Running eco command {eco}");

                try
                {
                    Program.acpi.SetGPUEco(eco);
                    await Task.Delay(TimeSpan.FromMilliseconds(AppConfig.Get("refresh_delay", 500)));

                    settings.Invoke(delegate
                    {
                        InitGPUMode();
                        ScreenControl.InitScreen();
                    });

                    if (eco == 0)
                    {
                        if ((AppConfig.IsNVPlatform() || nvRestartPending) && ProcessHelper.IsUserAdministrator())
                        {
                            settings.LockGPUModes(Properties.Strings.RestartingNVServices);
                            await Task.Delay(TimeSpan.FromMilliseconds(AppConfig.Get("nv_delay", 5000)));
                            if (AppConfig.IsNVPlatform()) NvidiaGpuControl.RestartNVService();
                            else NvidiaGpuControl.RestartNvContainer();
                            nvRestartPending = false;
                            settings.Invoke(delegate { InitGPUMode(); });
                            await Task.Delay(TimeSpan.FromMilliseconds(1000));
                        }

                        await HardwareControl.RecreateGpuControlWithRetry(3, 2);
                        if (HardwareControl.GpuControl is null) await HardwareControl.RecreateGpuControlWithRetry(3, 5);
                        CheckGpuError();
                        CheckStandardHalfState();
                    }

                    if (AppConfig.IsModeReapply())
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(1000));
                        Program.modeControl.AutoPerformance();
                    }
                    else
                    {
                        Program.modeControl.SetGPUClocks(false);
                    }
                }
                catch (Exception ex)
                {
                    Logger.WriteLine("Error setting GPU Eco: " + ex.Message);
                }
            });
        }

        public static bool IsPlugged() =>
            Program.currentSource == Program.PowerSource.Barrel ||
            (Program.currentSource == Program.PowerSource.USBC && !AppConfig.Is("optimized_usbc"));

        public static bool suspended = false;

        public bool AutoGPUMode(bool optimized = false, int delay = 0)
        {
            bool GpuAuto = AppConfig.Is("gpu_auto");
            if (!GpuAuto) return false;

            if (suspended)
            {
                Logger.WriteLine("Skipping GPU Mode switch: Suspend");
                return false;
            }

            int eco = Program.acpi.DeviceGet(AsusACPI.GPUEco);
            int mux = Program.acpi.DeviceGet(AsusACPI.GPUMux);

            if (mux == 0)
            {
                if (optimized) SetGPUMode(AsusACPI.GPUModeStandard, 1);
                return false;
            }

            if (eco == 1 && IsPlugged())
            {
                if (delay > 0) Thread.Sleep(delay);
                SetGPUEco(0);
                return true;
            }

            if (eco == 0 && !IsPlugged())
            {
                if (delay > 0) Thread.Sleep(delay);
                SetGPUEco(1);
                return true;
            }

            return false;
        }

        public void KillGPUApps()
        {
            if (HardwareControl.GpuControl is not null)
            {
                HardwareControl.GpuControl.KillGPUApps();
            }
        }

        public void CaptureNvBootState()
        {
            nvRestartPending = Program.acpi.IsNVidiaGPU() && Program.acpi.DeviceGet(AsusACPI.GPUEco) == 1;
        }

        public void CheckStandardHalfState()
        {
            if (gpuMode != AsusACPI.GPUModeStandard || HardwareControl.GpuControl is not null) return;
            Logger.WriteLine("Standard half-state");
        }

        public void StandardModeFix()
        {
            if (!AppConfig.IsStandardModeFix()) return;
            if (Program.acpi.DeviceGet(AsusACPI.GPUMux) == 0) return;

            Logger.WriteLine("Forcing Standard Mode on shutdown");
            Program.acpi.SetGPUEco(0);
        }

        public static string? gpuError = null;

        public static void CheckGpuError() => Task.Run(() =>
        {
            string? error = DeviceHelper.GetGpuError();
            if (gpuError == error) return;
            gpuError = error;
            if (error != null) Logger.WriteLine(error);
            Program.settingsForm.VisualiseGPUMode();
        });
    }
}
