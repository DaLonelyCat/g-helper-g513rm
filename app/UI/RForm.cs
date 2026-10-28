using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace GHelper.UI
{
    public class RForm : Form
    {
        // Calibrated Strix Industrial Semantic Accents (DESIGN.md WCAG AA verified)
        public static Color colorEco = Color.FromArgb(255, 6, 180, 138);
        public static Color colorStandard = Color.FromArgb(255, 58, 174, 239);
        public static Color colorTurbo = Color.FromArgb(255, 255, 59, 48);
        public static Color colorCustom = Color.FromArgb(255, 245, 158, 11);
        public static Color colorGray = Color.FromArgb(255, 155, 166, 178);

        public static Color buttonMain;
        public static Color buttonSecond;

        public static Color formBack;
        public static Color foreMain;
        public static Color borderMain;
        public static Color borderSecond;
        public static Color chartMain;
        public static Color chartGrid;

        public static bool flatTheme = false;

        [DllImport("UXTheme.dll", SetLastError = true, EntryPoint = "#138")]
        public static extern bool CheckSystemDarkModeStatus();

        [DllImport("UXTheme.dll", SetLastError = true, EntryPoint = "#135")]
        private static extern int SetPreferredAppMode(int preferredAppMode);

        [DllImport("UXTheme.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(nint hWnd, string pszSubAppName, string? pszSubIdList);

        [DllImport("DwmApi")]
        private static extern int DwmSetWindowAttribute(nint hwnd, int attr, int[] attrValue, int attrSize);

        public bool darkTheme = false;
        private bool themeInitialized = false;

        protected override CreateParams CreateParams
        {
            get
            {
                var parms = base.CreateParams;
                parms.Style &= ~0x02000000;  // Turn off WS_CLIPCHILDREN
                parms.ClassStyle &= ~0x00020000;
                return parms;
            }
        }

        public static void InitColors(bool darkTheme)
        {
            flatTheme = AppConfig.GetString("theme")?.ToLower() == "flat";

            if (darkTheme)
            {
                // Strix Industrial Dark Palette
                buttonMain = Color.FromArgb(255, 37, 42, 50);     // #252A32 Interactive Control Surface
                buttonSecond = Color.FromArgb(255, 28, 32, 38);   // #1C2026 Elevated Card Surface

                formBack = Color.FromArgb(255, 18, 20, 23);       // #121417 Primary Canvas
                foreMain = Color.FromArgb(255, 240, 244, 248);    // #F0F4F8 Primary Text (15.4:1 contrast)
                borderMain = Color.FromArgb(255, 48, 55, 66);     // #303742 Primary Border
                borderSecond = Color.FromArgb(255, 38, 44, 53);   // #262C35 Secondary Hairline Border

                chartMain = Color.FromArgb(255, 24, 28, 34);      // #181C22 Chart Canvas
                chartGrid = Color.FromArgb(255, 52, 60, 71);      // #343C47 Chart Grid
                colorGray = Color.FromArgb(255, 155, 166, 178);   // #9BA6B2 Secondary Text (6.3:1 contrast)
            }
            else
            {
                // Strix Daylight Palette
                buttonMain = Color.FromArgb(255, 232, 236, 242);   // #E8ECF2 Interactive Control Surface
                buttonSecond = Color.FromArgb(255, 255, 255, 255); // #FFFFFF Elevated Card Surface

                formBack = Color.FromArgb(255, 243, 245, 248);     // #F3F5F8 Primary Canvas
                foreMain = Color.FromArgb(255, 17, 21, 28);        // #11151C Primary Text (16.1:1 contrast)
                borderMain = Color.FromArgb(255, 203, 210, 220);   // #CBD2DC Primary Border
                borderSecond = Color.FromArgb(255, 216, 222, 232); // #D8DEE8 Secondary Hairline Border

                chartMain = Color.FromArgb(255, 255, 255, 255);    // #FFFFFF Chart Canvas
                chartGrid = Color.FromArgb(255, 203, 210, 220);    // #CBD2DC Chart Grid
                colorGray = Color.FromArgb(255, 74, 85, 104);      // #4A5568 Secondary Text (6.8:1 contrast)
            }
        }

        private static bool IsDarkTheme()
        {
            string? uiMode = AppConfig.GetString("ui_mode");

            if (uiMode is not null && uiMode.ToLower() == "dark")
            {
                return true;
            }

            if (uiMode is not null && uiMode.ToLower() == "light")
            {
                return false;
            }

            if (uiMode is not null && uiMode.ToLower() == "windows")
            {
                return CheckSystemDarkModeStatus();
            }

            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var registryValueObject = key?.GetValue("AppsUseLightTheme");

            if (registryValueObject == null) return true;
            return (int)registryValueObject <= 0;
        }

        public bool InitTheme(bool setDPI = false)
        {
            bool newDarkTheme = IsDarkTheme();
            bool changed = darkTheme != newDarkTheme;
            bool firstInit = !themeInitialized;
            darkTheme = newDarkTheme;
            themeInitialized = true;

            InitColors(darkTheme);

            if (setDPI)
                ControlHelper.Resize(this);

            if (changed || firstInit)
            {
                DwmSetWindowAttribute(Handle, 20, new[] { darkTheme ? 1 : 0 }, 4);
                SetPreferredAppMode(darkTheme ? 1 : 0);
                SetWindowTheme(Handle, darkTheme ? "DarkMode_Explorer" : "Explorer", null);
                ControlHelper.Adjust(this, changed);
                this.Invalidate();
            }

            return changed;
        }
    }
}
