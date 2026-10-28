using GHelper.AutoUpdate;
using GHelper.Battery;
using GHelper.Display;
using GHelper.Fan;
using GHelper.Gpu;
using GHelper.Helpers;
using GHelper.Input;
using GHelper.Mode;
using GHelper.Properties;
using GHelper.UI;
using GHelper.USB;
using System.Diagnostics;
using System.Timers;

namespace GHelper
{
    public partial class SettingsForm : RForm
    {
        ContextMenuStrip contextMenuStrip = new CustomContextMenu();
        ToolStripMenuItem menuEco = default!, menuStandard = default!, menuUltimate = default!, menuOptimized = default!;
        DonateControl donateControl;

        public GPUModeControl gpuControl;
        AutoUpdateControl updateControl;

        public static System.Timers.Timer sensorTimer = default!;
        private static readonly bool sensorsAlways = AppConfig.Is("sensors_always");
        private readonly System.Windows.Forms.Timer batteryTimer = new() { Interval = 200 };

        public Fans? fansForm;
        public Extra? extraForm;
        public Updates? updatesForm;

        // Custom ROG Strix G513RM UI controls
        private Panel panelTelemetry = default!;
        private TableLayoutPanel tableTelemetry = default!;
        private RButton buttonTelemCPU = default!;
        private RButton buttonTelemGPU = default!;
        private RButton buttonTelemRAM = default!;

        private RButton buttonSweetSpot = default!;
        private Panel panelPerfSub = default!;
        private Label labelModeSpec = default!;
        private RButton buttonAutoGame = default!;

        private TableLayoutPanel tableZones = default!;
        private RColorButton buttonZone1 = default!;
        private RColorButton buttonZone2 = default!;
        private RColorButton buttonZone3 = default!;
        private RColorButton buttonZone4 = default!;

        private RButton buttonBattery60 = default!;
        private RButton buttonBattery80 = default!;

        static long lastRefresh;
        static long lastBatteryRefresh;
        static long lastLostFocus;

        bool isGpuSection = true;
        bool isMuxGpu = true;

        bool batteryMouseOver = false;
        bool batteryFullMouseOver = false;

        bool sliderGammaIgnore = false;

        private void InitG513RMControls()
        {
            // Hide non-G513RM panels from base designer
            panelMatrix.Visible = false;
            panelAlly.Visible = false;
            panelPeripherals.Visible = false;
            panelRearLight.Visible = false;
            buttonOverlay.Visible = false;
            tableAMD.Visible = false;

            // 1. Live G513RM Hardware Telemetry Bar (6800H | RTX 3060 | 32GB DDR5 RAM + 1-click Purge)
            panelTelemetry = new Panel
            {
                AccessibleRole = AccessibleRole.Grouping,
                AccessibleName = "G513RM Live Hardware Telemetry",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                Margin = new Padding(0),
                Name = "panelTelemetry",
                Padding = new Padding(20, 14, 20, 0),
                Size = new Size(827, 96),
                TabIndex = 0
            };

            tableTelemetry = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 3,
                Dock = DockStyle.Top,
                Location = new Point(20, 14),
                Margin = new Padding(8, 4, 8, 4),
                Name = "tableTelemetry",
                RowCount = 1,
                Size = new Size(787, 82)
            };
            tableTelemetry.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            tableTelemetry.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            tableTelemetry.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));
            tableTelemetry.RowStyles.Add(new RowStyle(SizeType.Absolute, 82F));

            buttonTelemCPU = new RButton
            {
                AccessibleName = "Ryzen 7 6800H Telemetry",
                BorderColor = colorStandard,
                BorderRadius = 5,
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                Margin = new Padding(4),
                Name = "buttonTelemCPU",
                Secondary = true,
                Text = "6800H: Reading...\nFan: -- RPM",
                TextAlign = ContentAlignment.MiddleCenter,
                UseVisualStyleBackColor = false
            };
            buttonTelemCPU.FlatAppearance.BorderSize = 0;

            buttonTelemGPU = new RButton
            {
                AccessibleName = "RTX 3060 Telemetry",
                BorderColor = colorEco,
                BorderRadius = 5,
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                Margin = new Padding(4),
                Name = "buttonTelemGPU",
                Secondary = true,
                Text = "RTX 3060: Reading...\nFan: -- RPM",
                TextAlign = ContentAlignment.MiddleCenter,
                UseVisualStyleBackColor = false
            };
            buttonTelemGPU.FlatAppearance.BorderSize = 0;

            buttonTelemRAM = new RButton
            {
                AccessibleName = "32GB DDR5 RAM Telemetry and Standby Purge",
                BorderColor = colorCustom,
                BorderRadius = 5,
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                Margin = new Padding(4),
                Name = "buttonTelemRAM",
                Secondary = true,
                Text = "RAM: -- / 32 GB\n[Purge Standby]",
                TextAlign = ContentAlignment.MiddleCenter,
                UseVisualStyleBackColor = false
            };
            buttonTelemRAM.FlatAppearance.BorderSize = 0;

            tableTelemetry.Controls.Add(buttonTelemCPU, 0, 0);
            tableTelemetry.Controls.Add(buttonTelemGPU, 1, 0);
            tableTelemetry.Controls.Add(buttonTelemRAM, 2, 0);
            panelTelemetry.Controls.Add(tableTelemetry);

            // 2. Expand tablePerf to 5 columns to include Sweet Spot (Mode 3)
            buttonSweetSpot = new RButton
            {
                AccessibleName = "Sweet Spot 40W CPU and 140W RTX 3060 Mode",
                Activated = false,
                BackColor = SystemColors.ControlLightLight,
                BorderColor = colorCustom,
                BorderRadius = 5,
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                ForeColor = SystemColors.ControlText,
                Image = Properties.Resources.icons8_flash_48,
                ImageAlign = ContentAlignment.BottomCenter,
                Margin = new Padding(4),
                Name = "buttonSweetSpot",
                Secondary = false,
                Size = new Size(150, 120),
                TabIndex = 2,
                Text = "Sweet Spot",
                TextImageRelation = TextImageRelation.ImageAboveText,
                UseVisualStyleBackColor = false
            };
            buttonSweetSpot.FlatAppearance.BorderSize = 0;

            tablePerf.Controls.Clear();
            tablePerf.ColumnStyles.Clear();
            tablePerf.ColumnCount = 5;
            for (int i = 0; i < 5; i++)
                tablePerf.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));

            tablePerf.Controls.Add(buttonSilent, 0, 0);
            tablePerf.Controls.Add(buttonBalanced, 1, 0);
            tablePerf.Controls.Add(buttonSweetSpot, 2, 0);
            tablePerf.Controls.Add(buttonTurbo, 3, 0);
            tablePerf.Controls.Add(buttonFans, 4, 0);

            // Sub-row under tablePerf for Mode Spec summary + Auto Game Profile Switcher toggle
            panelPerfSub = new Panel
            {
                Dock = DockStyle.Top,
                Location = new Point(20, 192),
                Margin = new Padding(4, 6, 4, 2),
                Name = "panelPerfSub",
                Padding = new Padding(4, 6, 4, 2),
                Size = new Size(787, 64)
            };

            labelModeSpec = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 8F),
                Name = "labelModeSpec",
                Text = "G513RM Calibrated Preset",
                TextAlign = ContentAlignment.MiddleLeft
            };

            buttonAutoGame = new RButton
            {
                AccessibleName = "Auto Game Profile Switcher",
                Activated = false,
                BorderColor = colorCustom,
                BorderRadius = 3,
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 7.25F, FontStyle.Bold),
                Margin = new Padding(0),
                Name = "buttonAutoGame",
                Secondary = true,
                Size = new Size(210, 42),
                Text = "Auto Game: OFF",
                UseVisualStyleBackColor = false
            };
            buttonAutoGame.FlatAppearance.BorderSize = 0;

            panelPerfSub.Controls.Add(labelModeSpec);
            panelPerfSub.Controls.Add(buttonAutoGame);
            panelPerformance.Controls.Add(panelPerfSub);
            panelPerformance.Controls.SetChildIndex(panelPerfSub, 0);

            // 3. G513RM 4-Zone Keyboard & Flipped Lightbar Zone Color Pickers (Z1 - Z4)
            tableZones = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 4,
                Dock = DockStyle.Top,
                Location = new Point(20, 114),
                Margin = new Padding(8, 2, 8, 2),
                Name = "tableZones",
                RowCount = 1,
                Size = new Size(787, 50)
            };
            for (int i = 0; i < 4; i++)
                tableZones.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            tableZones.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));

            buttonZone1 = CreateZoneColorButton("buttonZone1", "Z1 Left", 1);
            buttonZone2 = CreateZoneColorButton("buttonZone2", "Z2 Mid-L", 2);
            buttonZone3 = CreateZoneColorButton("buttonZone3", "Z3 Mid-R", 3);
            buttonZone4 = CreateZoneColorButton("buttonZone4", "Z4 + Bar", 4);

            tableZones.Controls.Add(buttonZone1, 0, 0);
            tableZones.Controls.Add(buttonZone2, 1, 0);
            tableZones.Controls.Add(buttonZone3, 2, 0);
            tableZones.Controls.Add(buttonZone4, 3, 0);

            panelKeyboard.Controls.Add(tableZones);
            panelKeyboard.Controls.SetChildIndex(tableZones, 1);

            // 4. Battery 60% and 80% Quick-Cap Buttons alongside 100%
            sliderBattery.Size = new Size(528, 40);
            buttonBatteryFull.Location = new Point(718, 62);
            buttonBatteryFull.Size = new Size(88, 36);

            buttonBattery60 = new RButton
            {
                AccessibleName = "Set Battery Limit 60 Percent",
                Activated = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = SystemColors.ControlLight,
                BorderColor = Color.Transparent,
                Borderless = true,
                BorderRadius = 2,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 7.125F, FontStyle.Bold),
                ForeColor = SystemColors.ControlDark,
                Location = new Point(558, 62),
                Margin = new Padding(0),
                Name = "buttonBattery60",
                Secondary = true,
                Size = new Size(76, 36),
                Text = "60%",
                UseVisualStyleBackColor = false
            };
            buttonBattery60.FlatAppearance.BorderSize = 0;

            buttonBattery80 = new RButton
            {
                AccessibleName = "Set Battery Limit 80 Percent",
                Activated = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = SystemColors.ControlLight,
                BorderColor = Color.Transparent,
                Borderless = true,
                BorderRadius = 2,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 7.125F, FontStyle.Bold),
                ForeColor = SystemColors.ControlDark,
                Location = new Point(638, 62),
                Margin = new Padding(0),
                Name = "buttonBattery80",
                Secondary = true,
                Size = new Size(76, 36),
                Text = "80%",
                UseVisualStyleBackColor = false
            };
            buttonBattery80.FlatAppearance.BorderSize = 0;

            panelBattery.Controls.Add(buttonBattery60);
            panelBattery.Controls.Add(buttonBattery80);

            // 5. Repurpose buttonArmoury in panelVersion as live ASUS Services Blocker button
            buttonArmoury.Size = new Size(255, 40);
            buttonArmoury.Visible = true;
            buttonArmoury.Text = "ASUS Services: Checking...";
        }

        private RColorButton CreateZoneColorButton(string name, string text, int zone)
        {
            var btn = new RColorButton
            {
                AccessibleName = $"Keyboard Aura {text} Color",
                Activated = false,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = SystemColors.ButtonHighlight,
                BorderColor = Color.Transparent,
                BorderRadius = 2,
                Dock = DockStyle.Top,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8F),
                ForeColor = SystemColors.ControlText,
                Margin = new Padding(4),
                Name = name,
                Secondary = false,
                Size = new Size(188, 42),
                Tag = zone,
                Text = text,
                UseVisualStyleBackColor = false
            };
            return btn;
        }

        public SettingsForm()
        {
            InitializeComponent();
            InitTheme(true);

            gpuControl = new GPUModeControl(this);
            updateControl = new AutoUpdateControl(this);

            buttonSilent.Text = Properties.Strings.Silent;
            buttonBalanced.Text = Properties.Strings.Balanced;
            buttonTurbo.Text = Properties.Strings.Turbo;
            buttonSweetSpot.Text = "Sweet Spot";
            buttonFans.Text = Properties.Strings.FansPower;

            buttonEco.Text = Properties.Strings.EcoMode;
            buttonUltimate.Text = Properties.Strings.UltimateMode;
            buttonStandard.Text = Properties.Strings.StandardMode;
            buttonOptimized.Text = Properties.Strings.Optimized;
            buttonStopGPU.Text = Properties.Strings.StopGPUApps;

            buttonScreenAuto.Text = Properties.Strings.AutoMode;
            buttonMiniled.Text = Properties.Strings.Multizone;

            buttonKeyboardColor.Text = Properties.Strings.Color;
            buttonKeyboard.Text = Properties.Strings.Extra;

            labelPerf.Text = Properties.Strings.PerformanceMode;
            labelGPU.Text = Properties.Strings.GPUMode;
            labelSreen.Text = Properties.Strings.LaptopScreen;
            UpdateKeyboardLabel();
            labelBatteryTitle.Text = Properties.Strings.BatteryChargeLimit;

            checkStartup.Text = Properties.Strings.RunOnStartup;

            buttonQuit.Text = Properties.Strings.Quit;
            buttonUpdates.Text = Properties.Strings.Updates;
            buttonDonate.Text = Properties.Strings.Donate;

            // Accessible Labels
            sliderBattery.AccessibleName = Properties.Strings.BatteryChargeLimit;
            buttonQuit.AccessibleName = Properties.Strings.Quit;
            buttonUpdates.AccessibleName = Properties.Strings.BiosAndDriverUpdates;
            panelPerformance.AccessibleName = Properties.Strings.PerformanceMode;
            buttonSilent.AccessibleName = Properties.Strings.Silent;
            buttonBalanced.AccessibleName = Properties.Strings.Balanced;
            buttonTurbo.AccessibleName = Properties.Strings.Turbo;
            buttonFans.AccessibleName = Properties.Strings.FansAndPower;
            panelGPU.AccessibleName = Properties.Strings.GPUMode;
            buttonEco.AccessibleName = Properties.Strings.EcoMode;
            buttonStandard.AccessibleName = Properties.Strings.StandardMode;
            buttonOptimized.AccessibleName = Properties.Strings.Optimized;
            buttonUltimate.AccessibleName = Properties.Strings.UltimateMode;
            panelScreen.AccessibleName = Properties.Strings.LaptopScreen;

            buttonScreenAuto.AccessibleName = Properties.Strings.AutoMode;

            panelKeyboard.AccessibleName = Properties.Strings.LaptopKeyboard;
            buttonKeyboard.AccessibleName = Properties.Strings.ExtraSettings;
            buttonKeyboardColor.AccessibleName = Properties.Strings.LaptopKeyboard + " " + Properties.Strings.Color;
            comboKeyboard.AccessibleName = Properties.Strings.LaptopBacklight;

            FormClosing += SettingsForm_FormClosing;
            Deactivate += SettingsForm_LostFocus;

            buttonSilent.BorderColor = colorEco;
            buttonBalanced.BorderColor = colorStandard;
            buttonSweetSpot.BorderColor = colorCustom;
            buttonTurbo.BorderColor = colorTurbo;
            buttonFans.BorderColor = colorGray;

            buttonEco.BorderColor = colorEco;
            buttonStandard.BorderColor = colorStandard;
            buttonUltimate.BorderColor = colorTurbo;
            buttonOptimized.BorderColor = colorEco;
            buttonXGM.BorderColor = colorTurbo;

            button60Hz.BorderColor = colorGray;
            button120Hz.BorderColor = colorGray;
            buttonScreenAuto.BorderColor = colorGray;
            buttonMiniled.BorderColor = colorTurbo;

            buttonEnergySaver.BackColor = colorEco;
            buttonEnergySaver.ForeColor = SystemColors.ControlLightLight;
            buttonEnergySaver.Click += ButtonEnergySaver_Click;

            buttonAmdOled.BackColor = colorTurbo;
            buttonAmdOled.ForeColor = SystemColors.ControlLightLight;
            buttonAmdOled.Click += ButtonAmdOled_Click;

            buttonArmoury.BackColor = colorTurbo;
            buttonArmoury.ForeColor = SystemColors.ControlLightLight;
            buttonArmoury.Click += ButtonArmoury_Click;

            buttonSilent.Click += ButtonSilent_Click;
            buttonBalanced.Click += ButtonBalanced_Click;
            buttonSweetSpot.Click += ButtonSweetSpot_Click;
            buttonTurbo.Click += ButtonTurbo_Click;

            buttonTelemCPU.Click += (_, _) => FansToggle(0);
            buttonTelemGPU.Click += (_, _) => FansToggle(1);
            buttonTelemRAM.Click += ButtonTelemRAM_Click;

            buttonAutoGame.Click += ButtonAutoGame_Click;
            VisualiseAutoGame();

            buttonZone1.Click += ButtonZone_Click;
            buttonZone2.Click += ButtonZone_Click;
            buttonZone3.Click += ButtonZone_Click;
            buttonZone4.Click += ButtonZone_Click;

            buttonBattery60.Click += (_, _) => SetQuickBatteryLimit(60);
            buttonBattery80.Click += (_, _) => SetQuickBatteryLimit(80);

            buttonEco.Click += ButtonEco_Click;
            buttonStandard.Click += ButtonStandard_Click;
            buttonUltimate.Click += ButtonUltimate_Click;
            buttonOptimized.Click += ButtonOptimized_Click;
            buttonStopGPU.Click += ButtonStopGPU_Click;
            pictureGPU.Click += PictureGPU_Click;

            VisibleChanged += SettingsForm_VisibleChanged;

            button60Hz.Click += Button60Hz_Click;
            button120Hz.Click += Button120Hz_Click;
            buttonScreenAuto.Click += ButtonScreenAuto_Click;
            buttonMiniled.Click += ButtonMiniled_Click;
            buttonFHD.Click += ButtonFHD_Click;
            buttonHDRControl.Click += ButtonHDRControl_Click;

            buttonQuit.Click += ButtonQuit_Click;

            buttonKeyboardColor.Click += ButtonKeyboardColor_Click;
            buttonKeyboardColor.Swatch2Click += ButtonKeyboardColor2_Click;

            buttonFans.Click += ButtonFans_Click;
            buttonKeyboard.Click += ButtonKeyboard_Click;

            labelCPUFan.Click += LabelCPUFan_Click;
            labelGPUFan.Click += LabelCPUFan_Click;

            checkStartup.Checked = Startup.IsScheduled();
            checkStartup.CheckedChanged += CheckStartup_CheckedChanged;

            labelVersion.Click += LabelVersion_Click;
            labelVersion.ForeColor = colorGray;

            buttonOptimized.MouseMove += ButtonOptimized_MouseHover;
            buttonOptimized.MouseLeave += ButtonGPU_MouseLeave;

            buttonEco.MouseMove += ButtonEco_MouseHover;
            buttonEco.MouseLeave += ButtonGPU_MouseLeave;

            buttonStandard.MouseMove += ButtonStandard_MouseHover;
            buttonStandard.MouseLeave += ButtonGPU_MouseLeave;

            buttonUltimate.MouseMove += ButtonUltimate_MouseHover;
            buttonUltimate.MouseLeave += ButtonGPU_MouseLeave;

            buttonScreenAuto.MouseMove += ButtonScreenAuto_MouseHover;
            buttonScreenAuto.MouseLeave += ButtonScreen_MouseLeave;

            button60Hz.MouseMove += Button60Hz_MouseHover;
            button60Hz.MouseLeave += ButtonScreen_MouseLeave;

            button120Hz.MouseMove += Button120Hz_MouseHover;
            button120Hz.MouseLeave += ButtonScreen_MouseLeave;

            buttonFHD.MouseMove += ButtonFHD_MouseHover;
            buttonFHD.MouseLeave += ButtonScreen_MouseLeave;

            buttonUpdates.Click += ButtonUpdates_Click;

            sliderBattery.MouseUp += SliderBattery_MouseUp;
            sliderBattery.KeyUp += SliderBattery_KeyUp;
            sliderBattery.ValueChanged += SliderBattery_ValueChanged;
            batteryTimer.Tick += (_, _) =>
            {
                batteryTimer.Stop();
                BatteryControl.SetBatteryChargeLimit(sliderBattery.Value);
                VisualiseBatteryFull();
            };

            sensorTimer = new System.Timers.Timer(AppConfig.Get("sensor_timer", 1000));
            sensorTimer.Elapsed += OnTimedEvent;
            sensorTimer.Enabled = sensorsAlways;

            labelCharge.MouseEnter += PanelBattery_MouseEnter;
            labelCharge.MouseLeave += PanelBattery_MouseLeave;
            labelBattery.Click += LabelBattery_Click;

            buttonBatteryFull.MouseEnter += ButtonBatteryFull_MouseEnter;
            buttonBatteryFull.MouseLeave += ButtonBatteryFull_MouseLeave;
            buttonBatteryFull.Click += ButtonBatteryFull_Click;

            buttonKeyboard.SizeChanged += (s, e) => AlignFnLock();
            AlignFnLock();

            Text = "G-Helper G513RM Edition " + (ProcessHelper.IsUserAdministrator() ? "[Admin] - " : "- ") + AppConfig.GetModelShort();
            TopMost = AppConfig.Is("topmost");

            this.Resize += SettingsForm_Resize;

            VisualiseFnLock();
            buttonFnLock.Click += ButtonFnLock_Click;

            labelVisual.Click += LabelVisual_Click;
            labelCharge.Click += LabelCharge_Click;

            donateControl = new DonateControl(this, buttonDonate);
            donateControl.Init();

            labelBacklight.ForeColor = colorStandard;
            labelBacklight.Click += LabelBacklight_Click;

            toolTip.SetToolTip(buttonTelemCPU, "Ryzen 7 6800H temperature, package power, and CPU fan speed. Click to open CPU Fan & Power settings.");
            toolTip.SetToolTip(buttonTelemGPU, "NVIDIA RTX 3060 temperature, power draw, VRAM usage, and GPU fan speed. Click to open GPU Clock & Boost settings.");
            toolTip.SetToolTip(buttonTelemRAM, "32GB DDR5 RAM utilization. Click to purge Windows Standby List and background working sets.");
            toolTip.SetToolTip(buttonSweetSpot, "G513RM Sweet Spot: 40W CPU SPL / 55W sPPT, 83C cap, full 140W RTX 3060 (+120MHz Core / +250MHz Mem), low-noise gaming fan curve.");
            toolTip.SetToolTip(buttonAutoGame, "Automatically switches to Sweet Spot when a game or sustained RTX 3060 3D workload (>45W) runs, and restores your previous mode on exit.");

            panelPerformance.Focus();
            InitVisual();
            RefreshServicesBadgeAsync();
        }

        private void ButtonTelemRAM_Click(object? sender, EventArgs e)
        {
            buttonTelemRAM.Enabled = false;
            buttonTelemRAM.Text = "Purging RAM...\nPlease wait";
            Task.Run(() =>
            {
                string purgeStatus = G513Telemetry.PurgeStandbyAndWorkingSet();
                var snap = G513Telemetry.Capture();
                BeginInvoke(delegate
                {
                    buttonTelemRAM.Enabled = true;
                    buttonTelemRAM.Text = $"{snap.RamHeader}\n{purgeStatus}";
                    Program.toast?.RunToast($"RAM Purge: {purgeStatus} ({snap.RamHeader})", ToastIcon.Charger);
                });
            });
        }

        private void ButtonAutoGame_Click(object? sender, EventArgs e)
        {
            bool next = !ProcessProfileWatcher.IsEnabled;
            ProcessProfileWatcher.SetEnabled(next);
            VisualiseAutoGame();
            Program.toast?.RunToast("Auto Game Profile: " + (next ? "ON (Sweet Spot)" : "OFF"), ToastIcon.Controller);
        }

        public void VisualiseAutoGame()
        {
            if (InvokeRequired) { Invoke(VisualiseAutoGame); return; }
            bool enabled = ProcessProfileWatcher.IsEnabled;
            buttonAutoGame.Activated = enabled;
            buttonAutoGame.Text = enabled ? "Auto Game: ON" : "Auto Game: OFF";
            buttonAutoGame.BackColor = enabled ? colorCustom : buttonSecond;
            buttonAutoGame.ForeColor = enabled ? Color.Black : foreMain;
        }

        private void ButtonZone_Click(object? sender, EventArgs e)
        {
            if (sender is not RColorButton btn || btn.Tag is not int zone) return;

            Color initial = Aura.GetZoneColor(zone);
            RColorPicker colorDlg = new RColorPicker(initial, false);
            colorDlg.ColorChanged += c =>
            {
                Aura.SetZoneColor(zone, c);
                if (Aura.Mode != AuraMode.FOURZONE)
                {
                    Aura.Mode = AuraMode.FOURZONE;
                    comboKeyboard.SelectedValue = AuraMode.FOURZONE;
                }
                SetAura();
            };
            colorDlg.ShowDialog(this);
        }

        private void SetQuickBatteryLimit(int limit)
        {
            BatteryControl.SetBatteryChargeLimit(limit);
            VisualiseBattery(limit);
            Program.toast?.RunToast($"Battery Charge Limit: {limit}%", ToastIcon.Charger);
        }

        public void RefreshServicesBadgeAsync()
        {
            Task.Run(() =>
            {
                int count = AsusService.GetRunningCount();
                UpdateServicesStatus(count);
            });
        }

        public void UpdateServicesStatus(int runningCount)
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired)
            {
                BeginInvoke(() => UpdateServicesStatus(runningCount));
                return;
            }

            buttonArmoury.Visible = true;
            buttonArmoury.Enabled = true;
            if (runningCount > 0)
            {
                buttonArmoury.Text = $"ASUS Services: {runningCount} [Stop]";
                buttonArmoury.BackColor = colorTurbo;
                buttonArmoury.ForeColor = Color.White;
            }
            else
            {
                buttonArmoury.Text = "ASUS Services: 0 [Start]";
                buttonArmoury.BackColor = colorEco;
                buttonArmoury.ForeColor = Color.White;
            }
        }

        public void ToggleServicesFromMain()
        {
            buttonArmoury.Enabled = false;
            buttonArmoury.Text = "ASUS Services: Working...";
            Task.Run(() =>
            {
                int count = AsusService.GetRunningCount();
                if (count > 0)
                {
                    AsusService.StopAsusServices();
                    Program.inputDispatcher?.Init();
                }
                else
                {
                    AsusService.StartAsusServices();
                }
                int after = AsusService.GetRunningCount();
                UpdateServicesStatus(after);
            });
        }

        private void ButtonArmoury_Click(object? sender, EventArgs e)
        {
            if (ProcessHelper.IsUserAdministrator())
            {
                ToggleServicesFromMain();
            }
            else
            {
                ProcessHelper.RunAsAdmin("services_main");
            }
        }

        private void ButtonAmdOled_Click(object? sender, EventArgs e)
        {
            if (VisualControl.DisableOledPowerOptimization()) VisualiseAmdOled(false);
        }

        private void LabelBattery_Click(object? sender, EventArgs e)
        {
            HardwareControl.chargeWatt = !HardwareControl.chargeWatt;
            RefreshSensors(true);
        }

        private void ButtonEnergySaver_Click(object? sender, EventArgs e)
        {
            PowerNative.SetEnergySaver(false);
        }

        private void LabelBacklight_Click(object? sender, EventArgs e)
        {
            if (AppConfig.IsDynamicLighting() && DynamicLightingHelper.IsEnabled()) DynamicLightingHelper.OpenSettings();
        }

        private void ButtonFHD_Click(object? sender, EventArgs e)
        {
            ScreenControl.ToogleFHD();
        }

        private void ButtonHDRControl_Click(object? sender, EventArgs e)
        {
            ScreenControl.ToogleHDRControl();
        }

        private void SliderBattery_ValueChanged(object? sender, EventArgs e)
        {
            VisualiseBatteryTitle(sliderBattery.Value);
        }

        private void SliderBattery_KeyUp(object? sender, KeyEventArgs e)
        {
            batteryTimer.Stop();
            batteryTimer.Start();
        }

        private void SliderBattery_MouseUp(object? sender, MouseEventArgs e)
        {
            batteryTimer.Stop();
            batteryTimer.Start();
        }

        private void LabelCharge_Click(object? sender, EventArgs e)
        {
            BatteryControl.BatteryReport();
        }

        private void LabelVisual_Click(object? sender, EventArgs e)
        {
            labelVisual.Visible = false;
            VisualControl.forceVisual = true;
        }

        public void InitVisual()
        {
            if (AppConfig.Is("hide_visual")) return;

            labelGammaTitle.Text = Properties.Strings.VisualMode;

            var gamuts = VisualControl.GetGamutModes();

            if (gamuts.Count > 0)
            {
                tableVisual.ColumnCount = 3;
                buttonInstallColor.Visible = false;
            }
            else
            {
                if (ColorProfileHelper.ProfileExists())
                {
                    tableVisual.ColumnCount = 2;

                    buttonInstallColor.Text = Properties.Strings.DownloadColorProfiles;
                    buttonInstallColor.Visible = true;
                    buttonInstallColor.Click += ButtonInstallColorProfile_Click;

                    panelGamma.Visible = true;
                    tableVisual.Visible = true;
                }

                return;
            }

            panelGamma.Visible = true;
            tableVisual.Visible = true;

            comboVisual.SelectedValueChanged -= ComboVisual_SelectedValueChanged;
            comboColorTemp.SelectedValueChanged -= ComboVisual_SelectedValueChanged;
            comboGamut.SelectedValueChanged -= ComboGamut_SelectedValueChanged;

            var visualValue = (SplendidCommand)AppConfig.Get("visual", (int)VisualControl.GetDefaultVisualMode());
            var colorTempValue = AppConfig.Get("color_temp", VisualControl.DefaultColorTemp);

            comboVisual.DropDownStyle = ComboBoxStyle.DropDownList;
            comboVisual.DataSource = new BindingSource(VisualControl.GetVisualModes(), null);
            comboVisual.DisplayMember = "Value";
            comboVisual.ValueMember = "Key";
            comboVisual.SelectedValue = visualValue;

            comboColorTemp.DropDownStyle = ComboBoxStyle.DropDownList;
            comboColorTemp.DataSource = new BindingSource(VisualControl.GetTemperatures(), null);
            comboColorTemp.DisplayMember = "Value";
            comboColorTemp.ValueMember = "Key";
            comboColorTemp.SelectedValue = colorTempValue;

            VisualControl.SetVisual(visualValue, colorTempValue, true);

            comboVisual.SelectedValueChanged += ComboVisual_SelectedValueChanged;
            comboVisual.Visible = true;
            VisualiseDisabled();

            comboColorTemp.SelectedValueChanged += ComboVisual_SelectedValueChanged;
            comboColorTemp.Visible = true;

            if (gamuts.Count <= 1) return;

            comboGamut.DropDownStyle = ComboBoxStyle.DropDownList;
            comboGamut.DataSource = new BindingSource(gamuts, null);
            comboGamut.DisplayMember = "Value";
            comboGamut.ValueMember = "Key";
            comboGamut.SelectedValue = (SplendidGamut)AppConfig.Get("gamut", (int)VisualControl.GetDefaultGamut());

            comboGamut.SelectedValueChanged += ComboGamut_SelectedValueChanged;
            comboGamut.Visible = true;
        }

        public void CycleVisualMode(int delta)
        {
            if (comboVisual.Items.Count < 1) return;

            if (delta > 0)
            {
                if (comboVisual.SelectedIndex < comboVisual.Items.Count - 1)
                    comboVisual.SelectedIndex += 1;
                else
                    comboVisual.SelectedIndex = 0;
            }
            else
            {
                if (comboVisual.SelectedIndex > 0)
                    comboVisual.SelectedIndex -= 1;
                else
                    comboVisual.SelectedIndex = comboVisual.Items.Count - 1;
            }

            Program.toast.RunToast(comboVisual.GetItemText(comboVisual.SelectedItem), ToastIcon.BrightnessUp);
        }

        private async void ButtonInstallColorProfile_Click(object? sender, EventArgs e)
        {
            await ColorProfileHelper.InstallProfile();
            InitVisual();
        }

        private void ComboGamut_SelectedValueChanged(object? sender, EventArgs e)
        {
            if (comboGamut.SelectedValue is int val)
                VisualControl.SetGamut(val);
            else if (comboGamut.SelectedValue is SplendidGamut g)
                VisualControl.SetGamut((int)g);
        }

        private void ComboVisual_SelectedValueChanged(object? sender, EventArgs e)
        {
            if (comboVisual.SelectedValue is SplendidCommand cmd && comboColorTemp.SelectedValue is int temp)
            {
                VisualControl.SetVisual(cmd, temp);
                VisualiseDisabled();
            }
        }

        public void VisualiseBrightness()
        {
            if (InvokeRequired) { Invoke(VisualiseBrightness); return; }
            sliderGammaIgnore = true;
            sliderGamma.Value = VisualControl.GetBrightness();
            labelGamma.Text = sliderGamma.Value + "%";
            sliderGammaIgnore = false;
        }

        public void VisualiseAmdOled(bool status = false)
        {
            if (InvokeRequired) { Invoke(() => VisualiseAmdOled(status)); return; }
            buttonAmdOled.Visible = status;
        }

        public void VisualiseArmoury(bool status = false)
        {
            RefreshServicesBadgeAsync();
        }

        public void VisualiseDisabled()
        {
            comboGamut.Enabled = comboColorTemp.Enabled = (SplendidCommand)AppConfig.Get("visual") != SplendidCommand.Disabled;
        }

        public void VisualiseGamut()
        {
            if (InvokeRequired) { Invoke(VisualiseGamut); return; }
            if (comboGamut.Items.Count > 0) comboGamut.SelectedIndex = 0;
        }

        private void SliderGamma_ValueChanged(object? sender, EventArgs e)
        {
            if (sliderGammaIgnore) return;
            VisualControl.SetBrightness(sliderGamma.Value);
        }

        private void SettingsForm_LostFocus(object? sender, EventArgs e)
        {
            lastLostFocus = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        }

        private void ButtonBatteryFull_Click(object? sender, EventArgs e)
        {
            BatteryControl.ToggleBatteryLimitFull();
        }

        private void ButtonBatteryFull_MouseLeave(object? sender, EventArgs e)
        {
            batteryFullMouseOver = false;
            RefreshSensors(true);
        }

        private void ButtonBatteryFull_MouseEnter(object? sender, EventArgs e)
        {
            batteryFullMouseOver = true;
            labelCharge.Text = Properties.Strings.BatteryLimitFull;
        }

        private void SettingsForm_Resize(object? sender, EventArgs e)
        {
            if (WindowState != FormWindowState.Normal)
            {
                WindowState = FormWindowState.Normal;
                return;
            }

            var screen = Screen.FromControl(this);
            Left = screen.WorkingArea.Right - 10 - Width;
            Top = screen.WorkingArea.Top - screen.Bounds.Top > screen.Bounds.Bottom - screen.WorkingArea.Bottom ? screen.WorkingArea.Top + 10 : screen.WorkingArea.Bottom - 10 - Height;
        }

        private void PanelBattery_MouseEnter(object? sender, EventArgs e)
        {
            batteryMouseOver = true;
            ShowBatteryWear();
        }

        private void PanelBattery_MouseLeave(object? sender, EventArgs e)
        {
            batteryMouseOver = false;
            RefreshSensors(true);
        }

        private async void ShowBatteryWear()
        {
            if (lastBatteryRefresh == 0 || Math.Abs(DateTimeOffset.Now.ToUnixTimeMilliseconds() - lastBatteryRefresh) > 15 * 60_000)
            {
                lastBatteryRefresh = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                await Task.Run(HardwareControl.RefreshBatteryHealth);
            }

            if (batteryMouseOver && HardwareControl.batteryHealth != -1)
            {
                labelCharge.Text = Properties.Strings.BatteryHealth + ": " + Math.Round(HardwareControl.batteryHealth, 1) + "%";
            }
        }

        private void SettingsForm_VisibleChanged(object? sender, EventArgs e)
        {
            sensorTimer.Enabled = this.Visible || sensorsAlways;
            if (this.Visible)
            {
                RefreshSensors(true);
                RefreshServicesBadgeAsync();
                updateControl.CheckForUpdates();
            }
        }

        private void ButtonUpdates_Click(object? sender, EventArgs e)
        {
            if (updatesForm == null || updatesForm.Text == "")
            {
                updatesForm = new Updates();
                AddOwnedForm(updatesForm);
            }

            if (updatesForm.Visible)
            {
                updatesForm.Close();
            }
            else
            {
                updatesForm.Show();
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_POWERBROADCAST && m.WParam == (IntPtr)NativeMethods.PBT_APMSUSPEND)
            {
                Logger.WriteLine("System Suspend");
                GPUModeControl.suspended = true;
                Program.modeControl.SleepReset();
                m.Result = (IntPtr)1;
            }

            if (m.Msg == NativeMethods.WM_POWERBROADCAST && m.WParam == (IntPtr)NativeMethods.PBT_APMRESUMEAUTOMATIC)
            {
                Logger.WriteLine("System Resume");
                GPUModeControl.suspended = false;
                BatteryControl.AutoBattery();
                m.Result = (IntPtr)1;
            }

            if (m.Msg == NativeMethods.WM_POWERBROADCAST && m.WParam == (IntPtr)NativeMethods.PBT_POWERSETTINGCHANGE)
            {
                var settings = (NativeMethods.POWERBROADCAST_SETTING)m.GetLParam(typeof(NativeMethods.POWERBROADCAST_SETTING));
                if (settings.PowerSetting == NativeMethods.PowerSettingGuid.LIDSWITCH_STATE_CHANGE)
                {
                    switch (settings.Data)
                    {
                        case 0:
                            Logger.WriteLine("Lid Closed");
                            BatteryControl.AutoBattery();
                            InputDispatcher.lidClose = true;
                            Aura.ApplyBrightness(0, "Lid");
                            break;
                        case 1:
                            Logger.WriteLine("Lid Open");
                            InputDispatcher.InitFNLock();
                            InputDispatcher.lidClose = false;
                            Aura.ApplyBrightness(InputDispatcher.GetBacklight(), "Lid");
                            break;
                    }
                }
                else if (settings.PowerSetting == NativeMethods.PowerSettingGuid.EnergySaverStatus)
                {
                    Logger.WriteLine("Battery Saver: " + settings.Data);
                    buttonEnergySaver.Visible = settings.Data != 0;
                }
                else
                {
                    switch (settings.Data)
                    {
                        case 0:
                            Logger.WriteLine("Monitor Power Off");
                            Aura.SleepBrightness();
                            break;
                        case 1:
                            Logger.WriteLine("Monitor Power On");
                            GPUModeControl.suspended = false;
                            Thread.Sleep(1000);
                            if (!Program.SetAutoModes(wakeup: true)) BatteryControl.AutoBattery();
                            Task.Delay(2000).ContinueWith(_ => InputDispatcher.InitFNLock());
                            break;
                        case 2:
                            Logger.WriteLine("Monitor Dimmed");
                            break;
                    }
                }
                m.Result = (IntPtr)1;
            }

            if (m.Msg == Program.WM_TASKBARCREATED)
            {
                Logger.WriteLine("Taskbar created, re-creating tray icon");
                if (Program.trayIcon is not null) Program.trayIcon.Visible = true;
            }

            try
            {
                base.WndProc(ref m);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }

        public void SetContextMenu()
        {
            var currentMode = Modes.GetCurrent();

            foreach (ToolStripItem item in contextMenuStrip.Items.Cast<ToolStripItem>().ToList())
            {
                if (item is ToolStripMenuItem menuItem) menuItem.Dispose();
            }
            contextMenuStrip.Items.Clear();
            contextMenuStrip.ShowCheckMargin = true;
            contextMenuStrip.ImageScalingSize = new Size(16, 16);
            contextMenuStrip.ShowImageMargin = false;
            Padding padding = new Padding(5, 5, 5, 5);

            var title = new ToolStripMenuItem(Properties.Strings.PerformanceMode);
            title.Margin = padding;
            title.Enabled = false;
            contextMenuStrip.Items.Add(title);

            foreach (var mode in Modes.GetDictonary())
            {
                var menuMode = new ToolStripMenuItem(mode.Value);
                menuMode.Tag = mode.Key;
                menuMode.Click += (sender, args) => { Program.modeControl.SetPerformanceMode(mode.Key); };
                menuMode.Margin = padding;
                menuMode.Checked = (mode.Key == currentMode);
                contextMenuStrip.Items.Add(menuMode);
            }

            contextMenuStrip.Items.Add("-");

            if (isGpuSection)
            {
                var titleGPU = new ToolStripMenuItem(Properties.Strings.GPUMode);
                titleGPU.Margin = padding;
                titleGPU.Enabled = false;
                contextMenuStrip.Items.Add(titleGPU);

                menuEco = new ToolStripMenuItem(Properties.Strings.EcoMode);
                menuEco.Click += ButtonEco_Click;
                menuEco.Margin = padding;
                menuEco.Checked = buttonEco.Activated;
                contextMenuStrip.Items.Add(menuEco);

                menuStandard = new ToolStripMenuItem(Properties.Strings.StandardMode);
                menuStandard.Click += ButtonStandard_Click;
                menuStandard.Margin = padding;
                menuStandard.Checked = buttonStandard.Activated;
                contextMenuStrip.Items.Add(menuStandard);

                menuUltimate = new ToolStripMenuItem(Properties.Strings.UltimateMode);
                menuUltimate.Click += ButtonUltimate_Click;
                menuUltimate.Margin = padding;
                menuUltimate.Checked = buttonUltimate.Activated;
                menuUltimate.Visible = isMuxGpu;
                contextMenuStrip.Items.Add(menuUltimate);

                menuOptimized = new ToolStripMenuItem(Properties.Strings.Optimized);
                menuOptimized.Click += ButtonOptimized_Click;
                menuOptimized.Margin = padding;
                menuOptimized.Checked = buttonOptimized.Activated;
                contextMenuStrip.Items.Add(menuOptimized);

                contextMenuStrip.Items.Add("-");
            }

            var purgeItem = new ToolStripMenuItem("Purge Standby RAM (32GB)");
            purgeItem.Margin = padding;
            purgeItem.Click += ButtonTelemRAM_Click;
            contextMenuStrip.Items.Add(purgeItem);

            var bwIcon = new ToolStripMenuItem(Properties.Strings.BWTrayIcon);
            bwIcon.Margin = padding;
            bwIcon.Checked = AppConfig.IsBWIcon();
            bwIcon.Click += (sender, args) =>
            {
                bwIcon.Checked = !bwIcon.Checked;
                AppConfig.Set("bw_icon", bwIcon.Checked ? 1 : 0);
                VisualiseIcon();
            };
            contextMenuStrip.Items.Add(bwIcon);

            contextMenuStrip.Items.Add("-");

            var quit = new ToolStripMenuItem(Properties.Strings.Quit);
            quit.Click += ButtonQuit_Click;
            quit.Margin = padding;
            contextMenuStrip.Items.Add(quit);

            contextMenuStrip.Renderer = new CustomMenuRenderer();

            InitContextMenuTheme();

            if (Program.trayIcon is not null) Program.trayIcon.ContextMenuStrip = contextMenuStrip;
        }

        public void InitContextMenuTheme()
        {
            if (contextMenuStrip is not null)
            {
                contextMenuStrip.BackColor = this.BackColor;
                contextMenuStrip.ForeColor = this.ForeColor;
            }

            donateControl?.ApplyTheme();
        }

        public void SetVersionLabel(string label, bool update = false)
        {
            if (InvokeRequired)
                Invoke(delegate
                {
                    labelVersion.Text = label;
                    if (update) labelVersion.ForeColor = colorTurbo;
                });
            else
            {
                labelVersion.Text = label;
                if (update) labelVersion.ForeColor = colorTurbo;
            }
        }

        private void LabelVersion_Click(object? sender, EventArgs e)
        {
            updateControl.Update();
        }

        private static void OnTimedEvent(Object? source, ElapsedEventArgs? e)
        {
            Program.settingsForm.RefreshSensors();
        }

        private void ButtonFHD_MouseHover(object? sender, EventArgs e)
        {
            labelTipScreen.Text = "Switch to " + ((buttonFHD.Text == "FHD") ? "UHD" : "FHD") + " Mode";
        }

        private void Button120Hz_MouseHover(object? sender, EventArgs e)
        {
            labelTipScreen.Text = Properties.Strings.MaxRefreshTooltip;
        }

        private void Button60Hz_MouseHover(object? sender, EventArgs e)
        {
            labelTipScreen.Text = Properties.Strings.MinRefreshTooltip.Replace("60", ScreenControl.MIN_RATE.ToString());
        }

        private void ButtonScreen_MouseLeave(object? sender, EventArgs e)
        {
            labelTipScreen.Text = "";
        }

        private void ButtonScreenAuto_MouseHover(object? sender, EventArgs e)
        {
            labelTipScreen.Text = "Manual Refresh Mode (G513RM external monitor safe)";
        }

        private void ButtonUltimate_MouseHover(object? sender, EventArgs e)
        {
            labelTipGPU.Text = Properties.Strings.UltimateGPUTooltip;
        }

        private void ButtonStandard_MouseHover(object? sender, EventArgs e)
        {
            labelTipGPU.Text = Properties.Strings.StandardGPUTooltip;
        }

        private void ButtonEco_MouseHover(object? sender, EventArgs e)
        {
            labelTipGPU.Text = Properties.Strings.EcoGPUTooltip;
        }

        private void ButtonOptimized_MouseHover(object? sender, EventArgs e)
        {
            labelTipGPU.Text = Properties.Strings.OptimizedGPUTooltip;
        }

        private void ButtonGPU_MouseLeave(object? sender, EventArgs e)
        {
            labelTipGPU.Text = "";
        }

        private void ButtonScreenAuto_Click(object? sender, EventArgs e)
        {
            ScreenControl.SetAutoRefresh(1);
            ScreenControl.AutoScreen();
        }

        private void CheckStartup_CheckedChanged(object? sender, EventArgs e)
        {
            if (sender is null) return;
            CheckBox chk = (CheckBox)sender;

            if (chk.Checked)
                Startup.Schedule();
            else
                Startup.UnSchedule();
        }

        private void LabelCPUFan_Click(object? sender, EventArgs e)
        {
            FanSensorControl.fanRpm = !FanSensorControl.fanRpm;
            RefreshSensors(true);
        }

        private void ButtonKeyboardColor2_Click(object? sender, EventArgs e)
        {
            SetColorPicker("aura_color2", Aura.Color2);
        }

        private void ButtonKeyboard_Click(object? sender, EventArgs e)
        {
            if (extraForm == null || extraForm.Text == "")
            {
                extraForm = new Extra();
                AddOwnedForm(extraForm);
            }

            if (extraForm.Visible)
            {
                extraForm.Close();
            }
            else
            {
                extraForm.Show();
            }
        }

        public void FansInit()
        {
            if (fansForm == null || fansForm.Text == "") return;
            Invoke(fansForm.InitAll);
        }

        public void GPUInit()
        {
            if (fansForm == null || fansForm.Text == "") return;
            Invoke(fansForm.InitGPU);
        }

        public void FansToggle(int index = 0)
        {
            if (fansForm == null || fansForm.Text == "")
            {
                fansForm = new Fans();
                AddOwnedForm(fansForm);
            }

            if (fansForm.Visible)
            {
                fansForm.Close();
            }
            else
            {
                fansForm.FormPosition();
                fansForm.Show();
                fansForm.ToggleNavigation(index);
            }
        }

        private void ButtonFans_Click(object? sender, EventArgs e)
        {
            FansToggle();
        }

        private void SetColorPicker(string colorField, Color initial)
        {
            RColorPicker colorDlg = new RColorPicker(initial, colorField == "aura_color" && Aura.HasRandomColor());
            colorDlg.ColorChanged += c =>
            {
                AppConfig.Set(colorField, c.ToArgb());
                SetAura();
            };
            colorDlg.ShowDialog(this);
        }

        private void ButtonKeyboardColor_Click(object? sender, EventArgs e)
        {
            SetColorPicker("aura_color", Aura.Color1);
        }

        public void InitAura()
        {
            comboKeyboard.DropDownStyle = ComboBoxStyle.DropDownList;
            if (!Aura.IsBacklightDetected)
                Aura.Init();

            Aura.Mode = (AuraMode)AppConfig.Get("aura_mode");
            Aura.Speed = (AuraSpeed)AppConfig.Get("aura_speed");
            Aura.SetColor(AppConfig.Get("aura_color"));
            Aura.SetColor2(AppConfig.Get("aura_color2"));

            comboKeyboard.DataSource = new BindingSource(Aura.GetModes(), null);
            comboKeyboard.DisplayMember = "Value";
            comboKeyboard.ValueMember = "Key";
            comboKeyboard.SelectedValue = Aura.Mode;
            comboKeyboard.SelectedValueChanged += ComboKeyboard_SelectedValueChanged;

            VisualiseAura();
        }

        public void SetAura()
        {
            Task.Run(() =>
            {
                Aura.ApplyAura();
                VisualiseAura();
            });
        }

        private void _VisualiseAura()
        {
            buttonKeyboardColor.SwatchColor = Aura.Color1;
            buttonKeyboardColor.SwatchColor2 = Aura.HasSecondColor() ? Aura.Color2 : (Color?)null;

            buttonZone1.SwatchColor = Aura.GetZoneColor(1);
            buttonZone2.SwatchColor = Aura.GetZoneColor(2);
            buttonZone3.SwatchColor = Aura.GetZoneColor(3);
            buttonZone4.SwatchColor = Aura.GetZoneColor(4);

            bool isFourZone = Aura.Mode == AuraMode.FOURZONE;
            buttonZone1.Activated = isFourZone;
            buttonZone2.Activated = isFourZone;
            buttonZone3.Activated = isFourZone;
            buttonZone4.Activated = isFourZone;

            bool dynamic = AppConfig.IsDynamicLighting() && DynamicLightingHelper.IsEnabled() && !AppConfig.IsDynamicLightingOnly();

            if (dynamic)
            {
                labelBacklight.Cursor = Cursors.Hand;
                labelBacklight.Text = Strings.DisableDynamicLighting;
            }
            else if (Aura.Mode == AuraMode.AMBIENT)
            {
                labelBacklight.Cursor = Cursors.Default;
                labelBacklight.Text = Strings.AmbientModeResources;
            }
            else
            {
                labelBacklight.Cursor = Cursors.Default;
                labelBacklight.Text = "G513RM 4-Zone Keyboard + Flipped 6-LED Lightbar";
            }
        }

        public void VisualiseAura()
        {
            if (InvokeRequired)
                Invoke(_VisualiseAura);
            else
                _VisualiseAura();
        }

        public void CycleAuraMode(int delta)
        {
            if (delta > 0)
            {
                if (comboKeyboard.SelectedIndex < comboKeyboard.Items.Count - 1)
                    comboKeyboard.SelectedIndex += 1;
                else
                    comboKeyboard.SelectedIndex = 0;
            }
            else
            {
                if (comboKeyboard.SelectedIndex > 0)
                    comboKeyboard.SelectedIndex -= 1;
                else
                    comboKeyboard.SelectedIndex = comboKeyboard.Items.Count - 1;
            }

            Program.toast.RunToast(comboKeyboard.GetItemText(comboKeyboard.SelectedItem), ToastIcon.BacklightUp);
        }

        private void ComboKeyboard_SelectedValueChanged(object? sender, EventArgs e)
        {
            if (comboKeyboard.SelectedValue is AuraMode m)
                AppConfig.Set("aura_mode", (int)m);
            else if (comboKeyboard.SelectedValue is int val)
                AppConfig.Set("aura_mode", val);
            SetAura();
        }

        private void Button120Hz_Click(object? sender, EventArgs e)
        {
            ScreenControl.SetAutoRefresh(0);
            ScreenControl.SetScreen(ScreenControl.MAX_REFRESH, 1);
        }

        private void Button60Hz_Click(object? sender, EventArgs e)
        {
            ScreenControl.SetAutoRefresh(0);
            ScreenControl.SetScreen(ScreenControl.MIN_RATE, 0);
        }

        private void ButtonMiniled_Click(object? sender, EventArgs e)
        {
            ScreenControl.ToogleMiniled();
        }

        public void VisualiseScreen(bool screenEnabled, bool screenAuto, int frequency, int maxFrequency, int overdrive, bool overdriveSetting, int miniled1, int miniled2, bool hdr, bool acm, int fhd, int hdrControl)
        {
            bool advancedColor = hdr || acm;

            ButtonEnabled(button60Hz, screenEnabled);
            ButtonEnabled(button120Hz, screenEnabled);
            ButtonEnabled(buttonScreenAuto, screenEnabled);
            ButtonEnabled(buttonMiniled, screenEnabled);

            labelSreen.Text = screenEnabled
                ? Properties.Strings.LaptopScreen + ": " + frequency + "Hz" + ((overdrive == 1) ? " + " + Properties.Strings.Overdrive : "")
                : Properties.Strings.LaptopScreen + ": " + Properties.Strings.TurnedOff;

            panelScreen.AccessibleName = labelSreen.Text;

            button60Hz.Activated = false;
            button120Hz.Activated = false;
            buttonScreenAuto.Activated = false;

            if (screenAuto)
            {
                buttonScreenAuto.Activated = true;
            }
            else if (frequency == ScreenControl.MIN_RATE)
            {
                button60Hz.Activated = true;
            }
            else if (frequency > ScreenControl.MIN_RATE)
            {
                button120Hz.Activated = true;
            }

            button60Hz.Text = ScreenControl.MIN_RATE + "Hz";

            if (maxFrequency > ScreenControl.MIN_RATE)
            {
                button120Hz.Text = maxFrequency.ToString() + "Hz" + (overdriveSetting ? " + OD" : "");
                panelScreen.Visible = true;
                tableScreen.Visible = true;
            }
            else if (maxFrequency > 0)
            {
                tableScreen.Visible = false;
                panelScreen.Visible = AppConfig.NoGpu();
            }

            buttonFHD.Visible = false;
            buttonMiniled.Visible = false;
            buttonHDRControl.Visible = false;

            if (advancedColor) labelVisual.Text = Properties.Strings.VisualModesHDR;
            if (!screenEnabled) labelVisual.Text = Properties.Strings.VisualModesScreen;

            if (!screenEnabled || advancedColor)
            {
                labelVisual.Location = tableVisual.Location;
                labelVisual.Width = tableVisual.Width;
                labelVisual.Height = tableVisual.Height;
                labelVisual.Visible = true;
            }
            else
            {
                labelVisual.Visible = false;
            }
        }

        private void ButtonQuit_Click(object? sender, EventArgs e)
        {
            Close();
            Program.trayIcon.Visible = false;
            Application.Exit();
        }

        public void HideAll()
        {
            this.Hide();
            if (fansForm != null && fansForm.Text != "") fansForm.Close();
            if (extraForm != null && extraForm.Text != "") extraForm.Close();
            if (updatesForm != null && updatesForm.Text != "") updatesForm.Close();
            MemoryHelper.TrimAfter();
        }

        public void ShowAll()
        {
            this.Activate();
            this.TopMost = true;
            this.TopMost = AppConfig.Is("topmost");
            if (TopMost) User32.SetWindowPos(Handle, -1, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
        }

        public DialogResult ShowMessage(string text, string title = "", MessageBoxButtons buttons = MessageBoxButtons.OK)
        {
            DialogResult result = DialogResult.None;
            Invoke((MethodInvoker)delegate
            {
                result = MessageBox.Show(this, text, title, buttons);
            });
            return result;
        }

        public bool HasAnyFocus(bool lostFocusCheck = false)
        {
            return (fansForm != null && fansForm.ContainsFocus) ||
                   (extraForm != null && extraForm.ContainsFocus) ||
                   (updatesForm != null && updatesForm.ContainsFocus) ||
                   this.ContainsFocus ||
                   (lostFocusCheck && Math.Abs(DateTimeOffset.Now.ToUnixTimeMilliseconds() - lastLostFocus) < 300);
        }

        private void SettingsForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                HideAll();
            }
        }

        private void ButtonUltimate_Click(object? sender, EventArgs e)
        {
            gpuControl.SetGPUMode(AsusACPI.GPUModeUltimate);
        }

        private void ButtonStandard_Click(object? sender, EventArgs e)
        {
            gpuControl.SetGPUMode(AsusACPI.GPUModeStandard);
        }

        private void ButtonEco_Click(object? sender, EventArgs e)
        {
            gpuControl.SetGPUMode(AsusACPI.GPUModeEco);
        }

        private void ButtonOptimized_Click(object? sender, EventArgs e)
        {
            AppConfig.Set("gpu_auto", (AppConfig.Get("gpu_auto") == 1) ? 0 : 1);
            VisualiseGPUMode();
            gpuControl.AutoGPUMode(true);
        }

        private void ButtonStopGPU_Click(object? sender, EventArgs e)
        {
            gpuControl.KillGPUApps();
        }

        public async void RefreshSensors(bool force = false)
        {
            int throttle = (!Visible && sensorsAlways) ? 6000 : 1500;
            if (!force && Math.Abs(DateTimeOffset.Now.ToUnixTimeMilliseconds() - lastRefresh) < throttle) return;
            lastRefresh = DateTimeOffset.Now.ToUnixTimeMilliseconds();

            string cpuTemp = "";
            string gpuTemp = "";

            string cpuFan = "";
            string gpuFan = "";
            string midFan = "";

            string battery = "";
            string charge = "";

            G513Telemetry.TelemetrySnapshot snap = default;

            await Task.Run(() =>
            {
                HardwareControl.ReadSensors();
                snap = G513Telemetry.Capture();
            });

            if (HardwareControl.cpuTemp > 0)
                cpuTemp = ": " + TempHelper.FormatTemp((double)HardwareControl.cpuTemp);

            if (HardwareControl.batteryCapacity > 0)
            {
                charge = Properties.Strings.BatteryCharge + ": " + HardwareControl.batteryCharge;
            }

            if (HardwareControl.batteryRate < 0)
                battery = Properties.Strings.Discharging + ": " + Math.Round(-(decimal)HardwareControl.batteryRate, 1).ToString() + "W";
            else if (HardwareControl.batteryRate > 0)
                battery = Properties.Strings.Charging + ": " + Math.Round((decimal)HardwareControl.batteryRate, 1).ToString() + "W";

            if (HardwareControl.gpuTemp > 0)
            {
                gpuTemp = ": " + TempHelper.FormatTemp((double)HardwareControl.gpuTemp);
            }

            if (HardwareControl.cpuFan is not null) cpuFan = Strings.FanSpeed + ": " + HardwareControl.cpuFan;
            if (HardwareControl.gpuFan is not null) gpuFan = Strings.FanSpeed + ": " + HardwareControl.gpuFan;
            if (HardwareControl.midFan is not null) midFan = Strings.FanSpeed + ": " + HardwareControl.midFan;

            string trayTip = $"6800H{cpuTemp} {cpuFan}";
            if (gpuTemp.Length > 0) trayTip += $"\nRTX 3060{gpuTemp} {gpuFan}";
            if (!string.IsNullOrEmpty(snap.RamHeader)) trayTip += $"\n{snap.RamHeader}";
            if (battery.Length > 0) trayTip += "\n" + battery;

            if (Program.settingsForm.IsHandleCreated)
                Program.settingsForm.BeginInvoke(delegate
                {
                    labelCPUFan.Text = "CPU" + cpuTemp + "  " + cpuFan;
                    labelGPUFan.Text = "GPU" + gpuTemp + "  " + gpuFan;

                    if (HardwareControl.midFan is not null)
                        labelMidFan.Text = "Mid " + midFan;

                    labelBattery.Text = battery;
                    if (!batteryMouseOver && !batteryFullMouseOver) labelCharge.Text = charge;

                    if (!string.IsNullOrEmpty(snap.CpuHeader))
                    {
                        buttonTelemCPU.Text = $"{snap.CpuHeader}\n{snap.CpuDetail}";
                        buttonTelemGPU.Text = $"{snap.GpuHeader}\n{snap.GpuDetail}";

                        if (buttonTelemRAM.Enabled)
                        {
                            buttonTelemRAM.Text = $"{snap.RamHeader}\n{snap.RamDetail}";
                        }
                    }
                });

            if (Program.trayIcon is not null)
            {
                // NotifyIcon.Text max length is 127 chars on Windows
                Program.trayIcon.Text = trayTip.Length > 126 ? trayTip.Substring(0, 126) : trayTip;
            }
        }

        public void LabelFansResult(string text)
        {
            if (fansForm != null && !fansForm.IsDisposed && fansForm.Text != "")
                fansForm.LabelFansResult(text);
        }

        public void ShowMode(int mode)
        {
            if (InvokeRequired)
                Invoke(delegate
                {
                    VisualiseMode(mode);
                });
            else
                VisualiseMode(mode);
        }

        protected void VisualiseMode(int mode)
        {
            buttonSilent.Activated = false;
            buttonBalanced.Activated = false;
            buttonSweetSpot.Activated = false;
            buttonTurbo.Activated = false;
            buttonFans.Activated = false;
            labelModeSpec.ForeColor = foreMain;

            switch (mode)
            {
                case AsusACPI.PerformanceSilent:
                    buttonSilent.Activated = true;
                    labelModeSpec.Text = "Silent: 25W SPL / 35W sPPT | 75C Cap | 5W GPU Boost (Zero-RPM Light Idle)";
                    break;
                case AsusACPI.PerformanceBalanced:
                    buttonBalanced.Activated = true;
                    labelModeSpec.Text = "Balanced: 45W SPL / 65W sPPT | 85C Cap | 15W GPU Boost (+50 Core / +100 Mem)";
                    break;
                case G513Presets.ModeSweetSpot:
                    buttonSweetSpot.Activated = true;
                    labelModeSpec.Text = "Sweet Spot: 40W SPL / 55W sPPT | 83C Cap | Full 140W RTX 3060 (+120 / +250)";
                    break;
                case AsusACPI.PerformanceTurbo:
                    buttonTurbo.Activated = true;
                    labelModeSpec.Text = "Turbo: 70W SPL / 90W sPPT | 92C Cap | Full 140W RTX 3060 (+100 Core / +200 Mem)";
                    break;
                default:
                    buttonFans.Activated = true;
                    labelModeSpec.Text = "Custom G513RM Profile";
                    buttonFans.BorderColor = Modes.GetBase(mode) switch
                    {
                        AsusACPI.PerformanceSilent => colorEco,
                        AsusACPI.PerformanceTurbo => colorTurbo,
                        _ => colorStandard,
                    };
                    break;
            }

            foreach (var item in contextMenuStrip.Items)
            {
                if (item is ToolStripMenuItem menuItem && menuItem.Tag is not null)
                {
                    menuItem.Checked = ((int)menuItem.Tag == mode);
                }
            }
        }

        public void SetModeLabel(string modeText)
        {
            if (InvokeRequired)
            {
                Invoke(delegate
                {
                    labelPerf.Text = modeText;
                    panelPerformance.AccessibleName = labelPerf.Text;
                });
            }
            else
            {
                labelPerf.Text = modeText;
                panelPerformance.AccessibleName = labelPerf.Text;
            }
        }

        public void VisualiseGPUButtons(bool eco = true, bool ultimate = true)
        {
            if (InvokeRequired) { Invoke(() => VisualiseGPUButtons(eco, ultimate)); return; }
            isMuxGpu = ultimate;

            if (!eco)
            {
                menuEco.Visible = buttonEco.Visible = false;
                menuOptimized.Visible = buttonOptimized.Visible = false;
                buttonStopGPU.Visible = true;
                tableGPU.ColumnCount = 3;
                tableScreen.ColumnCount = 3;
            }
            else
            {
                buttonStopGPU.Visible = false;
            }

            if (!ultimate)
            {
                menuUltimate.Visible = buttonUltimate.Visible = false;
                tableGPU.ColumnCount = 3;
                tableScreen.ColumnCount = 3;
            }
        }

        public void HideGPUModes(bool gpuExists)
        {
            isGpuSection = false;

            buttonEco.Visible = false;
            buttonStandard.Visible = false;
            buttonUltimate.Visible = false;
            buttonOptimized.Visible = false;
            buttonStopGPU.Visible = true;

            tableGPU.ColumnCount = 0;

            SetContextMenu();

            panelGPU.Visible = gpuExists;
        }

        public void LockGPUModes(string? text = null)
        {
            if (InvokeRequired) { Invoke(() => LockGPUModes(text)); return; }
            if (text is null) text = Properties.Strings.GPUMode + ": " + Properties.Strings.GPUChanging + " ...";

            ButtonEnabled(buttonOptimized, false);
            ButtonEnabled(buttonEco, false);
            ButtonEnabled(buttonStandard, false);
            ButtonEnabled(buttonUltimate, false);

            labelGPU.Text = text;
        }

        public void VisualiseGPUMode(int GPUMode = -1)
        {
            if (InvokeRequired) { Invoke(() => VisualiseGPUMode(GPUMode)); return; }

            if (toolTip.GetToolTip(pictureGPU) != (GPUModeControl.gpuError ?? ""))
            {
                pictureGPU.BackgroundImage = GPUModeControl.gpuError is null ? Properties.Resources.icons8_video_card_32 : SystemIcons.Warning.ToBitmap();
                pictureGPU.Cursor = GPUModeControl.gpuError is null ? Cursors.Default : Cursors.Hand;
                toolTip.SetToolTip(pictureGPU, GPUModeControl.gpuError);
            }

            ButtonEnabled(buttonOptimized, true);
            ButtonEnabled(buttonEco, true);
            ButtonEnabled(buttonStandard, true);
            ButtonEnabled(buttonUltimate, true);

            if (GPUMode == -1)
                GPUMode = AppConfig.Get("gpu_mode");

            bool GPUAuto = AppConfig.Is("gpu_auto");

            buttonEco.Activated = false;
            buttonStandard.Activated = false;
            buttonUltimate.Activated = false;
            buttonOptimized.Activated = false;

            switch (GPUMode)
            {
                case AsusACPI.GPUModeEco:
                    buttonOptimized.BorderColor = colorEco;
                    buttonEco.Activated = !GPUAuto;
                    buttonOptimized.Activated = GPUAuto;
                    labelGPU.Text = Properties.Strings.GPUMode + ": " + Properties.Strings.GPUModeEco;
                    panelGPU.AccessibleName = Properties.Strings.GPUMode + " - " + (GPUAuto ? Properties.Strings.Optimized : Properties.Strings.EcoMode);
                    break;
                case AsusACPI.GPUModeUltimate:
                    buttonUltimate.Activated = true;
                    labelGPU.Text = Properties.Strings.GPUMode + ": " + Properties.Strings.GPUModeUltimate;
                    panelGPU.AccessibleName = Properties.Strings.GPUMode + " - " + Properties.Strings.UltimateMode;
                    break;
                default:
                    buttonOptimized.BorderColor = colorStandard;
                    buttonStandard.Activated = !GPUAuto;
                    buttonOptimized.Activated = GPUAuto;
                    labelGPU.Text = Properties.Strings.GPUMode + ": " + Properties.Strings.GPUModeStandard;
                    panelGPU.AccessibleName = Properties.Strings.GPUMode + " - " + (GPUAuto ? Properties.Strings.Optimized : Properties.Strings.StandardMode);
                    break;
            }

            VisualiseIcon();

            if (isGpuSection)
            {
                menuEco.Checked = buttonEco.Activated;
                menuStandard.Checked = buttonStandard.Activated;
                menuUltimate.Checked = buttonUltimate.Activated;
                menuOptimized.Checked = buttonOptimized.Activated;
            }

            if (Top < 0)
            {
                labelTipGPU.Visible = false;
                labelTipScreen.Visible = false;
                Top = 5;
            }
        }

        private (int, bool, bool)? lastIcon;
        private bool isDark = CheckSystemDarkModeStatus();

        public void VisualiseIcon(bool themeChange = false)
        {
            if (Program.trayIcon is null) return;
            if (themeChange) isDark = CheckSystemDarkModeStatus();

            int GPUMode = AppConfig.Get("gpu_mode");
            bool bw = AppConfig.IsBWIcon();

            if (lastIcon == (GPUMode, isDark, bw)) return;
            lastIcon = (GPUMode, isDark, bw);

            Icon newIcon = GPUMode switch
            {
                AsusACPI.GPUModeEco => bw ? (isDark ? Properties.Resources.light_eco : Properties.Resources.dark_eco) : Properties.Resources.eco,
                AsusACPI.GPUModeUltimate => bw ? (isDark ? Properties.Resources.light_standard : Properties.Resources.dark_standard) : Properties.Resources.ultimate,
                _ => bw ? (isDark ? Properties.Resources.light_standard : Properties.Resources.dark_standard) : Properties.Resources.standard,
            };

            Icon? oldIcon = Program.trayIcon.Icon;
            Program.trayIcon.Icon = newIcon;
            oldIcon?.Dispose();
        }

        private void PictureGPU_Click(object? sender, EventArgs e)
        {
            if (GPUModeControl.gpuError is null) return;
            GPUModeControl.CheckGpuError();
            Process.Start(new ProcessStartInfo("devmgmt.msc") { UseShellExecute = true });
        }

        private void ButtonSilent_Click(object? sender, EventArgs e)
        {
            Program.modeControl.SetPerformanceMode(AsusACPI.PerformanceSilent);
        }

        private void ButtonBalanced_Click(object? sender, EventArgs e)
        {
            Program.modeControl.SetPerformanceMode(AsusACPI.PerformanceBalanced);
        }

        private void ButtonSweetSpot_Click(object? sender, EventArgs e)
        {
            Program.modeControl.SetPerformanceMode(G513Presets.ModeSweetSpot);
        }

        private void ButtonTurbo_Click(object? sender, EventArgs e)
        {
            Program.modeControl.SetPerformanceMode(AsusACPI.PerformanceTurbo);
        }

        public void ButtonEnabled(RButton but, bool enabled)
        {
            but.Enabled = enabled;
            but.BackColor = but.Enabled ? Color.FromArgb(255, but.BackColor) : Color.FromArgb(100, but.BackColor);
        }

        public void VisualiseBatteryTitle(int limit)
        {
            labelBatteryTitle.Text = Properties.Strings.BatteryChargeLimit + ": " + limit.ToString() + "%";
        }

        public void VisualiseBattery(int limit)
        {
            if (InvokeRequired) { Invoke(() => VisualiseBattery(limit)); return; }
            VisualiseBatteryTitle(limit);
            sliderBattery.Value = limit;

            sliderBattery.AccessibleName = Properties.Strings.BatteryChargeLimit + ": " + limit.ToString() + "%";

            VisualiseBatteryFull();
        }

        public void VisualiseBatteryFull()
        {
            if (InvokeRequired) { Invoke(VisualiseBatteryFull); return; }

            int currentLimit = AppConfig.Get("charge_limit", 100);
            bool is60 = !BatteryControl.chargeFull && currentLimit == 60;
            bool is80 = !BatteryControl.chargeFull && currentLimit == 80;

            buttonBattery60.BackColor = is60 ? colorEco : buttonSecond;
            buttonBattery60.ForeColor = is60 ? Color.White : colorGray;

            buttonBattery80.BackColor = is80 ? colorStandard : buttonSecond;
            buttonBattery80.ForeColor = is80 ? Color.White : colorGray;

            if (BatteryControl.chargeFull || currentLimit == 100)
            {
                buttonBatteryFull.BackColor = colorCustom;
                buttonBatteryFull.ForeColor = Color.Black;
                buttonBatteryFull.AccessibleName = Properties.Strings.BatteryChargeLimit + " 100% on";
            }
            else
            {
                buttonBatteryFull.BackColor = buttonSecond;
                buttonBatteryFull.ForeColor = colorGray;
                buttonBatteryFull.AccessibleName = Properties.Strings.BatteryChargeLimit + " 100% off";
            }
        }

        public void UpdateKeyboardLabel()
        {
            labelKeyboard.Text = Properties.Strings.LaptopKeyboard;
        }

        private void AlignFnLock()
        {
            buttonFnLock.Width = (buttonKeyboard.Width - 8) / 2;
        }

        public void VisualiseFnLock()
        {
            if (AppConfig.Is("fn_lock"))
            {
                buttonFnLock.BackColor = colorStandard;
                buttonFnLock.ForeColor = SystemColors.ControlLightLight;
                buttonFnLock.AccessibleName = "Fn-Lock on";
            }
            else
            {
                buttonFnLock.BackColor = buttonSecond;
                buttonFnLock.ForeColor = colorGray;
                buttonFnLock.AccessibleName = "Fn-Lock off";
            }
        }

        private void ButtonFnLock_Click(object? sender, EventArgs e)
        {
            InputDispatcher.ToggleFnLock();
        }
    }
}
