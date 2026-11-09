using GHelper.UI;
using GHelper.Helpers;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text;

namespace GHelper.Power
{
    public class PowerTrackerForm : RForm
    {
        [DllImport("uxtheme.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string? pszSubIdList);

        private readonly System.Windows.Forms.Timer _uiTimer = new() { Interval = 1500 };
        private readonly ComboBox _comboPeriod = new();
        private readonly RButton _buttonReset = new();
        private readonly RButton _buttonExport = new();
        private readonly TextBox _textFilter = new();

        private readonly Label _labelCurrentVal = new();
        private readonly Label _labelCurrentSub = new();
        private readonly Label _labelAvgVal = new();
        private readonly Label _labelAvgSub = new();
        private readonly Label _labelPeakVal = new();
        private readonly Label _labelPeakSub = new();
        private readonly Label _labelEnergyVal = new();
        private readonly Label _labelEnergySub = new();

        private readonly Panel _chartPanel = new();
        private readonly ListView _listView = new();

        private int _sortColumn = 4; // Default sort by Energy Wh
        private bool _sortAscending = false;
        private List<(DateTime Time, float TotalWatts, float CpuWatts, float GpuWatts)> _trendHistory = [];

        public PowerTrackerForm()
        {
            InitializeComponent();
            InitTheme(true);
            ApplyStyling();

            _uiTimer.Tick += (s, e) => RefreshTelemetry();
            VisibleChanged += (s, e) =>
            {
                if (Visible)
                {
                    RefreshTelemetry();
                    _uiTimer.Start();
                }
                else
                {
                    _uiTimer.Stop();
                }
            };
        }

        private void InitializeComponent()
        {
            SuspendLayout();

            Text = "Power & Energy Tracker - ROG Strix G513RM";
            Size = new Size(820, 680);
            MinimumSize = new Size(760, 560);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            Icon = Properties.Resources.standard;

            // 1. Top Control Bar Panel
            var panelTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 48,
                Padding = new Padding(12, 8, 12, 6)
            };

            var labelTitle = new Label
            {
                Text = "⚡ Power Draw Tracker",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold, GraphicsUnit.Point),
                AutoSize = true,
                Location = new Point(12, 12)
            };

            _comboPeriod.DropDownStyle = ComboBoxStyle.DropDownList;
            _comboPeriod.Items.AddRange([
                "Last 1 Minute",
                "Last 5 Minutes",
                "Last 15 Minutes",
                "Last 30 Minutes",
                "Last 1 Hour",
                "Full Session"
            ]);
            _comboPeriod.SelectedIndex = 1; // Default: Last 5 Minutes
            _comboPeriod.Size = new Size(130, 28);
            _comboPeriod.Location = new Point(220, 10);
            _comboPeriod.SelectedIndexChanged += (s, e) => RefreshTelemetry();

            _textFilter.PlaceholderText = "Search app...";
            _textFilter.Size = new Size(130, 26);
            _textFilter.Location = new Point(360, 11);
            _textFilter.TextChanged += (s, e) => RefreshTelemetry();

            _buttonReset.Text = "Reset";
            _buttonReset.Size = new Size(70, 28);
            _buttonReset.Location = new Point(500, 10);
            _buttonReset.Click += ButtonReset_Click;

            _buttonExport.Text = "Export CSV";
            _buttonExport.Size = new Size(95, 28);
            _buttonExport.Location = new Point(580, 10);
            _buttonExport.Click += ButtonExport_Click;

            panelTop.Controls.Add(labelTitle);
            panelTop.Controls.Add(_comboPeriod);
            panelTop.Controls.Add(_textFilter);
            panelTop.Controls.Add(_buttonReset);
            panelTop.Controls.Add(_buttonExport);

            // 2. Metrics Cards Panel (TableLayout 4 cols)
            var tableMetrics = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 84,
                ColumnCount = 4,
                RowCount = 1,
                Padding = new Padding(12, 0, 12, 6)
            };
            tableMetrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            tableMetrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            tableMetrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            tableMetrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));

            tableMetrics.Controls.Add(CreateMetricCard("CURRENT DRAW", _labelCurrentVal, _labelCurrentSub), 0, 0);
            tableMetrics.Controls.Add(CreateMetricCard("AVERAGE DRAW", _labelAvgVal, _labelAvgSub), 1, 0);
            tableMetrics.Controls.Add(CreateMetricCard("PEAK DRAW", _labelPeakVal, _labelPeakSub), 2, 0);
            tableMetrics.Controls.Add(CreateMetricCard("TOTAL ENERGY", _labelEnergyVal, _labelEnergySub), 3, 0);

            // 3. Mini Trend Graph Panel
            _chartPanel.Dock = DockStyle.Top;
            _chartPanel.Height = 110;
            _chartPanel.Padding = new Padding(12, 4, 12, 4);
            _chartPanel.Paint += ChartPanel_Paint;
            EnableDoubleBuffering(_chartPanel);

            // 4. Per-App Breakdown ListView
            _listView.Dock = DockStyle.Fill;
            _listView.View = System.Windows.Forms.View.Details;
            _listView.FullRowSelect = true;
            _listView.GridLines = false;
            _listView.BorderStyle = BorderStyle.None;
            _listView.OwnerDraw = true;
            _listView.DrawColumnHeader += (s, e) =>
            {
                using var b = new SolidBrush(buttonSecond);
                e.Graphics.FillRectangle(b, e.Bounds);
                using var pen = new Pen(borderMain);
                e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
                e.Graphics.DrawLine(pen, e.Bounds.Right - 1, e.Bounds.Top, e.Bounds.Right - 1, e.Bounds.Bottom - 1);

                var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
                if (e.Header.TextAlign == HorizontalAlignment.Right) flags |= TextFormatFlags.Right;
                else if (e.Header.TextAlign == HorizontalAlignment.Center) flags |= TextFormatFlags.HorizontalCenter;
                else flags |= TextFormatFlags.Left;

                var textRect = new Rectangle(e.Bounds.Left + 4, e.Bounds.Top, e.Bounds.Width - 8, e.Bounds.Height);
                TextRenderer.DrawText(e.Graphics, e.Header.Text, e.Font ?? Font, textRect, foreMain, flags);
            };
            _listView.DrawItem += (s, e) => e.DrawDefault = true;
            _listView.DrawSubItem += (s, e) => e.DrawDefault = true;
            EnableDoubleBuffering(_listView);

            _listView.Columns.Add("Application / Process", 185, HorizontalAlignment.Left);
            _listView.Columns.Add("Current (W)", 90, HorizontalAlignment.Right);
            _listView.Columns.Add("Average (W)", 90, HorizontalAlignment.Right);
            _listView.Columns.Add("Peak (W)", 85, HorizontalAlignment.Right);
            _listView.Columns.Add("Energy (Wh)", 95, HorizontalAlignment.Right);
            _listView.Columns.Add("Share (%)", 75, HorizontalAlignment.Right);
            _listView.Columns.Add("Engine", 95, HorizontalAlignment.Center);
            _listView.Columns.Add("Status", 65, HorizontalAlignment.Center);

            _listView.ColumnClick += ListView_ColumnClick;
            _listView.ContextMenuStrip = CreateContextMenu();

            var panelListContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = formBack,
                Padding = new Padding(12, 6, 12, 12)
            };
            panelListContainer.Controls.Add(_listView);

            // Add all components in reverse dock order
            Controls.Add(panelListContainer);
            Controls.Add(_chartPanel);
            Controls.Add(tableMetrics);
            Controls.Add(panelTop);

            ResumeLayout(false);
        }

        private Panel CreateMetricCard(string title, Label valLabel, Label subLabel)
        {
            var card = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(4),
                Padding = new Padding(8, 6, 8, 6),
                BackColor = buttonSecond
            };

            var lblTitle = new Label
            {
                Text = title,
                Dock = DockStyle.Top,
                Height = 16,
                Font = new Font("Segoe UI", 7.5F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = colorGray
            };

            valLabel.Text = "-- W";
            valLabel.Dock = DockStyle.Top;
            valLabel.Height = 28;
            valLabel.Font = new Font("Segoe UI", 13F, FontStyle.Bold, GraphicsUnit.Point);
            valLabel.ForeColor = foreMain;

            subLabel.Text = "Reading...";
            subLabel.Dock = DockStyle.Fill;
            subLabel.Font = new Font("Segoe UI", 7.5F, FontStyle.Regular, GraphicsUnit.Point);
            subLabel.ForeColor = colorGray;

            card.Controls.Add(subLabel);
            card.Controls.Add(valLabel);
            card.Controls.Add(lblTitle);
            return card;
        }

        private void ApplyStyling()
        {
            BackColor = formBack;
            ForeColor = foreMain;

            ControlHelper.Adjust(this);

            try
            {
                SetWindowTheme(_listView.Handle, darkTheme ? "DarkMode_Explorer" : "Explorer", null);
                _listView.BackColor = buttonSecond;
                _listView.ForeColor = foreMain;
            }
            catch { }
        }

        private ContextMenuStrip CreateContextMenu()
        {
            var menu = new CustomContextMenu();
            var itemKill = new ToolStripMenuItem("Kill Application / Process");
            var itemLocate = new ToolStripMenuItem("Open File Location");
            var itemCopy = new ToolStripMenuItem("Copy Row Details");

            itemKill.Click += (s, e) =>
            {
                if (_listView.SelectedItems.Count == 0) return;
                string appName = _listView.SelectedItems[0].Text.Split(' ')[0];
                if (appName.Equals("System / Idle", StringComparison.OrdinalIgnoreCase)) return;

                DialogResult res = MessageBox.Show($"Terminate process '{appName}'?", "Kill Process", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (res == DialogResult.Yes)
                {
                    ProcessHelper.KillByName(appName);
                    RefreshTelemetry();
                }
            };

            itemLocate.Click += (s, e) =>
            {
                if (_listView.SelectedItems.Count == 0) return;
                string appName = _listView.SelectedItems[0].Text.Split(' ')[0];
                try
                {
                    Process[] procs = Process.GetProcessesByName(appName);
                    if (procs.Length > 0 && procs[0].MainModule != null)
                    {
                        string path = procs[0].MainModule!.FileName;
                        Process.Start("explorer.exe", $"/select,\"{path}\"");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Unable to locate executable: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            itemCopy.Click += (s, e) =>
            {
                if (_listView.SelectedItems.Count == 0) return;
                var item = _listView.SelectedItems[0];
                var sb = new StringBuilder();
                foreach (ListViewItem.ListViewSubItem sub in item.SubItems)
                {
                    sb.Append(sub.Text).Append('\t');
                }
                Clipboard.SetText(sb.ToString().TrimEnd('\t'));
            };

            menu.Items.Add(itemKill);
            menu.Items.Add(itemLocate);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(itemCopy);
            return menu;
        }

        private TimeSpan GetSelectedPeriod()
        {
            return _comboPeriod.SelectedIndex switch
            {
                0 => TimeSpan.FromMinutes(1),
                1 => TimeSpan.FromMinutes(5),
                2 => TimeSpan.FromMinutes(15),
                3 => TimeSpan.FromMinutes(30),
                4 => TimeSpan.FromHours(1),
                _ => TimeSpan.Zero // Full Session
            };
        }

        private void RefreshTelemetry()
        {
            if (InvokeRequired)
            {
                BeginInvoke(RefreshTelemetry);
                return;
            }

            TimeSpan period = GetSelectedPeriod();
            PowerTrackerSummary summary = PowerTrackerEngine.GetSummary(period);

            // 1. Metric Cards
            _labelCurrentVal.Text = $"{summary.CurrentTotalWatts:F1} W";
            _labelCurrentSub.Text = $"CPU: {summary.CurrentCpuWatts:F0}W | dGPU: {summary.CurrentGpuWatts:F0}W";
            if (summary.IsOnBattery && summary.CurrentBatteryWatts > 0)
                _labelCurrentSub.Text += $" | DC: {summary.CurrentBatteryWatts:F0}W";

            _labelAvgVal.Text = $"{summary.AverageTotalWatts:F1} W";
            _labelAvgSub.Text = summary.SampleCount > 0 ? $"Over {summary.SampleCount * 2}s ({summary.SampleCount} samples)" : "Awaiting data...";

            _labelPeakVal.Text = $"{summary.PeakTotalWatts:F1} W";
            _labelPeakSub.Text = summary.PeakTimestamp != DateTime.MinValue ? $"Peak at {summary.PeakTimestamp.ToLocalTime():HH:mm:ss}" : "No peaks";

            _labelEnergyVal.Text = $"{summary.TotalEnergyWh:F2} Wh";
            string kjStr = $"{summary.TotalEnergyWh * 3.6f:F1} kJ";
            if (summary.IsOnBattery && summary.EstimatedBatteryMinutes.HasValue)
            {
                int hours = (int)(summary.EstimatedBatteryMinutes.Value / 60);
                int mins = (int)(summary.EstimatedBatteryMinutes.Value % 60);
                _labelEnergySub.Text = $"{kjStr} | Est: {hours}h {mins}m left";
            }
            else
            {
                _labelEnergySub.Text = $"{kjStr} | CPU {summary.CpuEnergyWh:F2} / GPU {summary.GpuEnergyWh:F2} Wh";
            }

            // 2. Trend Data for Chart
            _trendHistory = summary.TrendData;
            _chartPanel.Invalidate();

            // 3. Process Table
            string filter = _textFilter.Text.Trim();
            var filtered = string.IsNullOrEmpty(filter)
                ? summary.TopProcesses
                : summary.TopProcesses.Where(p => p.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

            // Sort
            filtered = _sortColumn switch
            {
                0 => _sortAscending ? filtered.OrderBy(p => p.Name).ToList() : filtered.OrderByDescending(p => p.Name).ToList(),
                1 => _sortAscending ? filtered.OrderBy(p => p.CurrentWatts).ToList() : filtered.OrderByDescending(p => p.CurrentWatts).ToList(),
                2 => _sortAscending ? filtered.OrderBy(p => p.AverageWatts).ToList() : filtered.OrderByDescending(p => p.AverageWatts).ToList(),
                3 => _sortAscending ? filtered.OrderBy(p => p.PeakWatts).ToList() : filtered.OrderByDescending(p => p.PeakWatts).ToList(),
                4 => _sortAscending ? filtered.OrderBy(p => p.TotalEnergyWh).ToList() : filtered.OrderByDescending(p => p.TotalEnergyWh).ToList(),
                5 => _sortAscending ? filtered.OrderBy(p => p.PercentShare).ToList() : filtered.OrderByDescending(p => p.PercentShare).ToList(),
                6 => _sortAscending ? filtered.OrderBy(p => p.EngineTag).ToList() : filtered.OrderByDescending(p => p.EngineTag).ToList(),
                7 => _sortAscending ? filtered.OrderBy(p => p.IsActive).ToList() : filtered.OrderByDescending(p => p.IsActive).ToList(),
                _ => filtered
            };

            _listView.BeginUpdate();
            _listView.Items.Clear();

            foreach (var p in filtered)
            {
                string displayName = p.ProcessCount > 1 ? $"{p.Name} ({p.ProcessCount})" : p.Name;
                var item = new ListViewItem(displayName);
                item.SubItems.Add($"{p.CurrentWatts:F1}");
                item.SubItems.Add($"{p.AverageWatts:F1}");
                item.SubItems.Add($"{p.PeakWatts:F1}");
                item.SubItems.Add($"{p.TotalEnergyWh:F3}");
                item.SubItems.Add($"{p.PercentShare:F1}%");
                item.SubItems.Add(p.EngineTag);
                item.SubItems.Add(p.IsActive ? "Active" : "Exited");

                if (!p.IsActive) item.ForeColor = colorGray;
                else if (p.EngineTag.Contains("dGPU")) item.ForeColor = colorTurbo;
                else item.ForeColor = foreMain;

                _listView.Items.Add(item);
            }

            _listView.EndUpdate();
        }

        private void ChartPanel_Paint(object? sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int w = _chartPanel.ClientSize.Width - 24;
            int h = _chartPanel.ClientSize.Height - 16;
            if (w <= 50 || h <= 30) return;

            int left = 12;
            int top = 8;

            // Background canvas
            using (var brush = new SolidBrush(chartMain))
            {
                g.FillRectangle(brush, left, top, w, h);
            }
            using (var penGrid = new Pen(chartGrid, 1f))
            {
                g.DrawRectangle(penGrid, left, top, w, h);
                g.DrawLine(penGrid, left, top + h / 2, left + w, top + h / 2);
            }

            // Legend
            using (var f = new Font("Segoe UI", 7.5F, FontStyle.Regular))
            {
                using var bTotal = new SolidBrush(colorStandard);
                using var bCpu = new SolidBrush(colorCustom);
                using var bGpu = new SolidBrush(colorTurbo);

                g.DrawString("■ Total (W)", f, bTotal, left + 8, top + 4);
                g.DrawString("■ 6800H CPU", f, bCpu, left + 85, top + 4);
                g.DrawString("■ RTX 3060", f, bGpu, left + 175, top + 4);
            }

            if (_trendHistory.Count < 2)
            {
                using var bGray = new SolidBrush(colorGray);
                using var f = new Font("Segoe UI", 8F, FontStyle.Italic);
                g.DrawString("Sampling real-time power trend...", f, bGray, left + w / 2 - 80, top + h / 2 - 6);
                return;
            }

            // Max scale calculation
            float maxWatts = Math.Max(40f, _trendHistory.Max(p => Math.Max(p.TotalWatts, Math.Max(p.CpuWatts, p.GpuWatts))));
            maxWatts = (float)Math.Ceiling(maxWatts / 10.0) * 10f; // Round up to nearest 10W

            // Max label
            using (var bGray = new SolidBrush(colorGray))
            using (var f = new Font("Segoe UI", 7F))
            {
                g.DrawString($"{maxWatts:F0}W", f, bGray, left + w - 35, top + 4);
                g.DrawString("0W", f, bGray, left + w - 22, top + h - 14);
            }

            // Draw line plots
            int count = _trendHistory.Count;
            PointF[] totalPoints = new PointF[count];
            PointF[] cpuPoints = new PointF[count];
            PointF[] gpuPoints = new PointF[count];

            for (int i = 0; i < count; i++)
            {
                float x = left + ((float)i / (count - 1)) * w;
                float yTotal = top + h - (_trendHistory[i].TotalWatts / maxWatts) * h;
                float yCpu = top + h - (_trendHistory[i].CpuWatts / maxWatts) * h;
                float yGpu = top + h - (_trendHistory[i].GpuWatts / maxWatts) * h;

                totalPoints[i] = new PointF(x, Math.Clamp(yTotal, top, top + h));
                cpuPoints[i] = new PointF(x, Math.Clamp(yCpu, top, top + h));
                gpuPoints[i] = new PointF(x, Math.Clamp(yGpu, top, top + h));
            }

            using (var penCpu = new Pen(colorCustom, 1.5f)) g.DrawLines(penCpu, cpuPoints);
            using (var penGpu = new Pen(colorTurbo, 1.5f)) g.DrawLines(penGpu, gpuPoints);
            using (var penTotal = new Pen(colorStandard, 2f)) g.DrawLines(penTotal, totalPoints);
        }

        private void ListView_ColumnClick(object? sender, ColumnClickEventArgs e)
        {
            if (e.Column == _sortColumn)
            {
                _sortAscending = !_sortAscending;
            }
            else
            {
                _sortColumn = e.Column;
                _sortAscending = false;
            }
            RefreshTelemetry();
        }

        private void ButtonReset_Click(object? sender, EventArgs e)
        {
            PowerTrackerEngine.ResetSession();
            RefreshTelemetry();
        }

        private void ButtonExport_Click(object? sender, EventArgs e)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Application,Current_Watts,Average_Watts,Peak_Watts,Energy_Wh,Share_Percent,Engine,Status");
            foreach (ListViewItem item in _listView.Items)
            {
                sb.AppendLine($"\"{item.SubItems[0].Text}\",{item.SubItems[1].Text},{item.SubItems[2].Text},{item.SubItems[3].Text},{item.SubItems[4].Text},{item.SubItems[5].Text.Replace("%", "")},\"{item.SubItems[6].Text}\",\"{item.SubItems[7].Text}\"");
            }

            try
            {
                Clipboard.SetText(sb.ToString());
                MessageBox.Show("Power table copied to clipboard in CSV format!", "Export Successful", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Export failed: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void EnableDoubleBuffering(Control control)
        {
            typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                .SetValue(control, true, null);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            InitTheme();
            try
            {
                SetWindowTheme(_listView.Handle, darkTheme ? "DarkMode_Explorer" : "Explorer", null);
            }
            catch { }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _uiTimer.Stop();
            base.OnFormClosing(e);
        }
    }
}
