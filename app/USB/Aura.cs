using GHelper.Gpu;
using GHelper.Helpers;
using GHelper.Input;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using static GHelper.Helpers.DynamicLightingHelper;

namespace GHelper.USB
{
    public class AuraPower
    {
        public bool BootLogo;
        public bool BootKeyb;
        public bool AwakeLogo;
        public bool AwakeKeyb;
        public bool SleepLogo;
        public bool SleepKeyb;
        public bool ShutdownLogo;
        public bool ShutdownKeyb;

        public bool BootBar;
        public bool AwakeBar;
        public bool SleepBar;
        public bool ShutdownBar;

        public bool BootLid;
        public bool AwakeLid;
        public bool SleepLid;
        public bool ShutdownLid;

        public bool BootRear;
        public bool AwakeRear;
        public bool SleepRear;
        public bool ShutdownRear;
    }

    public enum AuraMode : int
    {
        AuraStatic = 0,
        AuraBreathe = 1,
        AuraColorCycle = 2,
        AuraRainbow = 3,
        Star = 4,
        Rain = 5,
        Highlight = 6,
        Laser = 7,
        Ripple = 8,
        AuraStrobe = 10,
        Comet = 11,
        Flash = 12,
        HEATMAP = 20,
        GPUMODE = 21,
        AMBIENT = 22,
        BATTERY = 23,
        GRADIENT = 24,
        ZONETEST = 25,
        AUDIO = 26,
        AUDIOPULSE = 27,
        FOURZONE = 28,
    }

    public enum AuraSpeed : int
    {
        Slow = 0,
        Normal = 1,
        Fast = 2,
    }

    public enum AuraBacklightType : byte
    {
        Unknown = 0x00,
        MultiZone = 0x02,
        PerKey = 0x03,
        SingleZone = 0x04,
    }

    public static class Aura
    {
        static byte[] MESSAGE_APPLY = { AsusHid.AURA_ID, 0xb4 };
        static byte[] MESSAGE_SET = { AsusHid.AURA_ID, 0xb5, 0, 0, 0 };

        static readonly int AURA_ZONES = 8;

        private static AuraMode mode = AuraMode.AuraStatic;
        private static AuraSpeed speed = AuraSpeed.Normal;

        private static bool backlight = false;
        private static bool initDirect = false;
        public static bool sessionLock = false;

        public static Color Color1 = Color.White;
        public static Color Color2 = Color.Black;

        public static Color RearColor = Color.White;
        private static AuraMode rearMode = AuraMode.AuraStatic;
        public static AuraMode RearMode
        {
            get { return rearMode; }
            set { rearMode = GetRearModes().ContainsKey(value) ? value : AuraMode.AuraStatic; }
        }

        static bool isACPI = false;

        static bool isStrix => BacklightType == AuraBacklightType.MultiZone || BacklightType == AuraBacklightType.PerKey;
        public static bool IsBacklightDetected => BacklightType != AuraBacklightType.Unknown;

        static bool isStrix4Zone = true;
        static bool isStrixNumpad = false;
        static bool isStrix4ZoneFlipped = true; // G513RM uses flipped 4-zone wire order

        static public bool isWhite = AppConfig.IsWhite();

        public static AuraBacklightType BacklightType { get; private set; } = AuraBacklightType.MultiZone;

        public static bool HasLogo { get; private set; } = true;
        public static bool HasLightbar { get; private set; } = true;
        public static bool HasRearglow { get; private set; } = false;
        static bool hasRearStrip = false;
        public static bool IsOldStrix { get; private set; } = false;

        static System.Timers.Timer timer = new System.Timers.Timer(1000);

        static readonly List<double> audioMaxes = new List<double>();
        static long lastAudioPresent;
        static double envBrightness;
        static double smoothedHue;
        static readonly double audioDecay = AppConfig.Get("audio_decay", 70) / 100.0;

        static Aura()
        {
            timer.Elapsed += Timer_Elapsed;
        }

        public static Color GetZoneColor(int zone)
        {
            int defaultColor = zone switch
            {
                0 => Color.FromArgb(255, 6, 180, 138).ToArgb(),
                1 => Color.FromArgb(255, 58, 174, 239).ToArgb(),
                2 => Color.FromArgb(255, 245, 158, 11).ToArgb(),
                _ => Color.FromArgb(255, 255, 59, 48).ToArgb()
            };
            return Color.FromArgb(255, Color.FromArgb(AppConfig.Get($"aura_zone_{zone}", defaultColor)));
        }

        public static void SetZoneColor(int zone, Color color)
        {
            AppConfig.Set($"aura_zone_{zone}", color.ToArgb());
        }

        public static Dictionary<AuraSpeed, string> GetSpeeds()
        {
            return new Dictionary<AuraSpeed, string>
            {
                { AuraSpeed.Slow, Properties.Strings.AuraSlow },
                { AuraSpeed.Normal, Properties.Strings.AuraNormal },
                { AuraSpeed.Fast, Properties.Strings.AuraFast }
            };
        }

        public static Dictionary<AuraMode, string> GetModes()
        {
            var modes = new Dictionary<AuraMode, string>();

            if (isWhite)
            {
                modes[AuraMode.AuraStatic] = Properties.Strings.AuraStatic;
                modes[AuraMode.AuraBreathe] = Properties.Strings.AuraBreathe;
                modes[AuraMode.AuraStrobe] = Properties.Strings.AuraStrobe;
                return modes;
            }

            bool perKey = BacklightType == AuraBacklightType.PerKey;
            bool multiZone = BacklightType == AuraBacklightType.MultiZone;
            bool isStrixKb = perKey || multiZone;

            modes[AuraMode.AuraStatic] = Properties.Strings.AuraStatic;
            modes[AuraMode.FOURZONE] = "4-Zone Custom (G513RM)";
            modes[AuraMode.GRADIENT] = "Gradient (4-Zone + Bar)";
            modes[AuraMode.AuraBreathe] = Properties.Strings.AuraBreathe;
            modes[AuraMode.AuraColorCycle] = Properties.Strings.AuraColorCycle;
            if (isStrixKb) modes[AuraMode.AuraRainbow] = Properties.Strings.AuraRainbow;

            if (perKey)
            {
                modes[AuraMode.Star] = "Star";
                modes[AuraMode.Rain] = "Rain";
                modes[AuraMode.Highlight] = "Highlight";
                modes[AuraMode.Laser] = "Laser";
                modes[AuraMode.Ripple] = "Ripple";
            }

            modes[AuraMode.AuraStrobe] = Properties.Strings.AuraStrobe;

            if (perKey)
            {
                modes[AuraMode.Comet] = "Comet";
                modes[AuraMode.Flash] = "Flash";
            }

            modes[AuraMode.HEATMAP] = "CPU Temp Heatmap";
            modes[AuraMode.GPUMODE] = "GPU Mode Indicator";
            modes[AuraMode.AMBIENT] = "Screen Ambient";
            modes[AuraMode.BATTERY] = "Battery Level";
            modes[AuraMode.AUDIO] = "Audio Spectrum";
            modes[AuraMode.AUDIOPULSE] = "Audio Pulse";

            if (isStrixKb)
            {
                modes[AuraMode.ZONETEST] = "Zone Test";
            }

            return modes;
        }

        private static Dictionary<AuraMode, string> _modesRear = new Dictionary<AuraMode, string>
        {
            { AuraMode.AuraStatic, Properties.Strings.AuraStatic },
            { AuraMode.AuraBreathe, Properties.Strings.AuraBreathe },
            { AuraMode.AuraColorCycle, Properties.Strings.AuraColorCycle },
            { AuraMode.AuraRainbow, Properties.Strings.AuraRainbow },
            { AuraMode.AuraStrobe, Properties.Strings.AuraStrobe },
        };

        public static Dictionary<AuraMode, string> GetRearModes()
        {
            return _modesRear;
        }

        public static AuraMode Mode
        {
            get { return mode; }
            set
            {
                mode = GetModes().ContainsKey(value) ? value : AuraMode.AuraStatic;
            }
        }

        public static AuraSpeed Speed
        {
            get { return speed; }
            set
            {
                speed = GetSpeeds().ContainsKey(value) ? value : AuraSpeed.Normal;
            }
        }

        public static void SetColor(int colorCode)
        {
            Color1 = Color.FromArgb(colorCode);
        }

        public static void SetColor2(int colorCode)
        {
            Color2 = Color.FromArgb(colorCode);
        }

        public static void SetRearColor(int colorCode)
        {
            RearColor = Color.FromArgb(colorCode);
        }

        public static bool HasSecondColor()
        {
            return mode == AuraMode.AuraBreathe || mode == AuraMode.GRADIENT;
        }

        public static bool HasRandomColor()
        {
            return mode == AuraMode.Star || mode == AuraMode.Highlight || mode == AuraMode.Laser || mode == AuraMode.Ripple;
        }

        private static void Timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            if (!InputDispatcher.backlightActivity)
                return;

            if (Mode == AuraMode.HEATMAP)
            {
                CustomRGB.ApplyHeatmap();
            }
            else if (Mode == AuraMode.BATTERY)
            {
                CustomRGB.ApplyBattery();
            }
            else if (Mode == AuraMode.AMBIENT)
            {
                CustomRGB.ApplyAmbient();
            }
        }

        public static byte[] AuraMessage(AuraMode mode, Color color, Color color2, int speed)
        {
            byte[] msg = new byte[17];
            msg[0] = AsusHid.AURA_ID;
            msg[1] = 0xB3;
            msg[2] = 0x00;
            msg[3] = (byte)mode;
            msg[4] = color.R;
            msg[5] = isWhite ? (byte)0 : color.G;
            msg[6] = isWhite ? (byte)0 : color.B;
            msg[7] = (byte)speed;
            msg[8] = 0x00;
            msg[9] = (color.R == 0 && color.G == 0 && color.B == 0) ? (byte)0xFF : (mode == AuraMode.AuraBreathe ? (byte)0x01 : (byte)0x00);
            msg[10] = color2.R;
            msg[11] = isWhite ? (byte)0 : color2.G;
            msg[12] = isWhite ? (byte)0 : color2.B;
            return msg;
        }

        private static void DetectBacklightType()
        {
            var response = AsusHid.AuraProbe(true);

            if (response is null || response.Length < 18) return;

            byte typeByte = response[9];
            byte feat1 = response[13];
            byte feat2 = response[14];

            const byte FEAT1_LOGO = 0x01;
            const byte FEAT1_LIGHTBAR = 0x02;
            const byte FEAT1_VCUT = 0x10;
            const byte FEAT1_REARGLOW = 0x80;
            const byte FEAT2_ONE_ZONE_RED_EFFECT = 0x10;

            BacklightType = typeByte switch
            {
                (byte)AuraBacklightType.MultiZone => AuraBacklightType.MultiZone,
                (byte)AuraBacklightType.PerKey => AuraBacklightType.PerKey,
                (byte)AuraBacklightType.SingleZone => AuraBacklightType.SingleZone,
                0x00 => AuraBacklightType.SingleZone,
                _ => AuraBacklightType.MultiZone
            };

            AppConfig.Set("backlight_type", typeByte);

            IsOldStrix = feat1 == 0;
            if (IsOldStrix) feat1 = FEAT1_LOGO | FEAT1_LIGHTBAR;
            HasLogo = (feat1 & FEAT1_LOGO) != 0;
            HasLightbar = (feat1 & FEAT1_LIGHTBAR) != 0;
            HasRearglow = (feat1 & (FEAT1_REARGLOW | FEAT1_VCUT)) != 0;
            hasRearStrip = (feat1 & FEAT1_REARGLOW) != 0;

            isStrix4Zone = BacklightType == AuraBacklightType.MultiZone;

            if (typeByte != 0x00 && (feat2 & FEAT2_ONE_ZONE_RED_EFFECT) != 0) isWhite = true;
        }

        public static void Init()
        {
            DetectBacklightType();
            InputDispatcher.InitFNLock();
        }

        public static void SleepBrightness()
        {
            if (!AppConfig.Is("keyboard_sleep")) ApplyBrightness(0, "Sleep");
        }

        public static void ApplyBrightness(int brightness, string log = "Backlight")
        {
            if (brightness == 0) backlight = false;

            DirectBrightness(brightness, log);

            if (brightness > 0)
            {
                if (!backlight) initDirect = true;
                backlight = true;
                if (Mode == AuraMode.GRADIENT || Mode == AuraMode.FOURZONE) ApplyAura();
            }
        }

        public static void DirectBrightness(int brightness, string log)
        {
            AsusHid.WriteInput([AsusHid.INPUT_ID, 0xBA, 0xC5, 0xC4, (byte)brightness], log);
        }

        static byte[] AuraPowerMessage(AuraPower flags)
        {
            byte keyb = 0, bar = 0, lid = 0, rear = 0;

            if (flags.BootLogo) keyb |= 1 << 0;
            if (flags.BootKeyb) keyb |= 1 << 1;
            if (flags.AwakeLogo) keyb |= 1 << 2;
            if (flags.AwakeKeyb) keyb |= 1 << 3;
            if (flags.SleepLogo) keyb |= 1 << 4;
            if (flags.SleepKeyb) keyb |= 1 << 5;
            if (flags.ShutdownLogo) keyb |= 1 << 6;
            if (flags.ShutdownKeyb) keyb |= 1 << 7;

            if (flags.AwakeBar) bar |= 1 << 0;
            if (flags.BootBar) bar |= 1 << 1;
            if (flags.AwakeBar) bar |= 1 << 2;
            if (flags.SleepBar) bar |= 1 << 3;
            if (flags.ShutdownBar) bar |= 1 << 4;

            if (flags.BootLid) lid |= 1 << 0;
            if (flags.AwakeLid) lid |= 1 << 1;
            if (flags.SleepLid) lid |= 1 << 2;
            if (flags.ShutdownLid) lid |= 1 << 3;

            if (flags.BootLid) lid |= 1 << 4;
            if (flags.AwakeLid) lid |= 1 << 5;
            if (flags.SleepLid) lid |= 1 << 6;
            if (flags.ShutdownLid) lid |= 1 << 7;

            if (flags.BootRear) rear |= 1 << 0;
            if (flags.AwakeRear) rear |= 1 << 1;
            if (flags.SleepRear) rear |= 1 << 2;
            if (flags.ShutdownRear) rear |= 1 << 3;

            if (flags.BootRear) rear |= 1 << 4;
            if (flags.AwakeRear) rear |= 1 << 5;
            if (flags.SleepRear) rear |= 1 << 6;
            if (flags.ShutdownRear) rear |= 1 << 7;

            return new byte[] { AsusHid.AURA_ID, 0xBD, 0x01, keyb, bar, lid, rear, 0xFF };
        }

        public static void ApplyPowerOff()
        {
            AsusHid.Write(AuraPowerMessage(new AuraPower()));
        }

        public static void ApplyPower()
        {
            bool backlightBattery = SystemInformation.PowerStatus.PowerLineStatus != PowerLineStatus.Online;

            AuraPower flags = new()
            {
                AwakeKeyb = backlightBattery ? AppConfig.IsOnBattery("keyboard_awake") : AppConfig.IsNotFalse("keyboard_awake"),
                BootKeyb = AppConfig.IsNotFalse("keyboard_boot"),
                SleepKeyb = AppConfig.IsNotFalse("keyboard_sleep"),
                ShutdownKeyb = AppConfig.IsNotFalse("keyboard_shutdown"),

                AwakeLogo = backlightBattery ? AppConfig.IsOnBattery("keyboard_awake_logo") : AppConfig.IsNotFalse("keyboard_awake_logo"),
                BootLogo = AppConfig.IsNotFalse("keyboard_boot_logo"),
                SleepLogo = AppConfig.IsNotFalse("keyboard_sleep_logo"),
                ShutdownLogo = AppConfig.IsNotFalse("keyboard_shutdown_logo"),

                AwakeBar = backlightBattery ? AppConfig.IsOnBattery("keyboard_awake_bar") : AppConfig.IsNotFalse("keyboard_awake_bar"),
                BootBar = AppConfig.IsNotFalse("keyboard_boot_bar"),
                SleepBar = AppConfig.IsNotFalse("keyboard_sleep_bar"),
                ShutdownBar = AppConfig.IsNotFalse("keyboard_shutdown_bar"),

                AwakeLid = backlightBattery ? AppConfig.IsOnBattery("keyboard_awake_lid") : AppConfig.IsNotFalse("keyboard_awake_lid"),
                BootLid = AppConfig.IsNotFalse("keyboard_boot_lid"),
                SleepLid = AppConfig.IsNotFalse("keyboard_sleep_lid"),
                ShutdownLid = AppConfig.IsNotFalse("keyboard_shutdown_lid"),

                AwakeRear = backlightBattery ? AppConfig.IsOnBattery("keyboard_awake_lid") : AppConfig.IsNotFalse("keyboard_awake_lid"),
                BootRear = AppConfig.IsNotFalse("keyboard_boot_lid"),
                SleepRear = AppConfig.IsNotFalse("keyboard_sleep_lid"),
                ShutdownRear = AppConfig.IsNotFalse("keyboard_shutdown_lid")
            };

            AsusHid.Write(AuraPowerMessage(flags));
        }

        static byte[] packetMap = new byte[]
        {
                    /* VDN   VUP   MICM  HPFN  ARMC  */
                         2,    3,    4,    5,    6,
        /* ESC          F1    F2    F3    F4    F5    F6    F7    F8    F9   F10   F11   F12              DEL15 DEL17  PAUS  PRT   HOME  */
            21,         23,   24,   25,   26,   28,   29,   30,   31,   33,   34,   35,   36,               37,   38,   39,   40,   41,
        /* BKTK    1     2     3     4     5     6     7     8     9     0     -     =   BSPC  BSPC  BSPC PLY15  NMLK  NMDV  NMTM  NMMI  */
            42,   43,   44,   45,   46,   47,   48,   49,   50,   51,   52,   53,   54,   55,   56,   57,   58,   59,   60,   61,   62,
        /* TAB     Q     W     E     R     T     Y     U     I     O     P     [     ]     \              STP15  NM7   NM8   NM9   NMPL  */
            63,   64,   65,   66,   67,   68,   69,   70,   71,   72,   73,   74,   75,   76,               79,   80,   81,   82,   83,
        /* CPLK    A     S     D     F     G     H     J     K     L     ;     "     #   ENTR  ENTR  ENTR PRV15  NM4   NM5   NM6   NMPL  */
            84,   85,   86,   87,   88,   89,   90,   91,   92,   93,   94,   95,   96,   97,   98,   99,  100,  101,  102,  103,  104,
        /* LSFT  ISO\    Z     X     C     V     B     N     M     ,     .     /   RSFT  RSFT  RSFT  ARWU NXT15  NM1   NM2   NM3   NMER  */
           105,  106,  107,  108,  109,  110,  111,  112,  113,  114,  115,  116,  117,  118,  119,  139,  121,  122,  123,  124,  125,
        /* LCTL  LFNC  LWIN  LALT              SPC               RALT  RFNC  RCTL        ARWL  ARWD  ARWR PRT15        NM0   NMPD  NMER  */
           126,  127,  128,  129,              131,              135,  136,  137,  138,  159,  160,  161,  142,        144,  145,  146,
        /* LB1   LB2   LB3                                                               ARW?  ARWL? ARWD? ARWR?       LB4   LB5   LB6   */
           174,  173,  172,                                                              120,  140,  141,  143,        171,  170,  169,
        /* KSTN  LOGO  LIDL  LIDR  */
             0,  167,  176,  177,
        };

        static byte[] packetZone = new byte[]
        {
                    /* VDN   VUP   MICM  HPFN  ARMC  */
                         0,    0,    1,    1,    1,
        /* ESC          F1    F2    F3    F4    F5    F6    F7    F8    F9   F10   F11   F12              DEL15 DEL17  PAUS  PRT   HOM   */
             0,          0,    0,    1,    1,    1,    1,    2,    2,    2,    2,    3,    3,                3,   3,    3,    3,    3,
        /* BKTK    1     2     3     4     5     6     7     8     9     0     -     =   BSPC  BSPC  BSPC PLY15  NMLK  NMDV  NMTM  NMMI  */
             0,    0,    0,    0,    1,    1,    1,    1,    2,    2,    2,    2,    3,    3,    3,    3,    3,    3,    3,    3,    3,
        /* TAB     Q     W     E     R     T     Y     U     I     O     P     [     ]     \              STP15  NM7   NM8   NM9   NMPL  */
             0,    0,    0,    0,    1,    1,    1,    1,    2,    2,    2,    2,    3,    3,                3,    3,    3,    3,    3,
        /* CPLK    A     S     D     F     G     H     J     K     L     ;     "     #   ENTR  ENTR  ENTR PRV15  NM4   NM5   NM6   NMPL  */
             0,    0,    0,    0,    1,    1,    1,    1,    2,    2,    2,    2,    3,    3,    3,    3,    3,    3,    3,    3,    3,
        /* LSFT  ISO\    Z     X     C     V     B     N     M     ,     .     /   RSFT  RSFT  RSFT  ARWU NXT15  NM1   NM2   NM3   NMER  */
             0,    0,    0,    0,    1,    1,    1,    1,    2,    2,    2,    2,    3,    3,    3,    3,    3,    3,    3,    3,    3,
        /* LCTL  LFNC  LWIN  LALT              SPC               RALT  RFNC  RCTL        ARWL  ARWD  ARWR PRT15        NM0   NMPD  NMER  */
             0,    0,    0,    0,              1,                  2,    2,    2,    2,    3,    3,    3,    3,          3,    3,    3,
        /* LB1   LB1   LB3                                                               ARW?  ARW?  ARW?  ARW?        LB4   LB5   LB6   */
             5,    5,    4,                                                                3,    3,    3,    3,          6,    7,    7,
        /* KSTN  LOGO  LIDL  LIDR  */
             3,    0,    0,    3,
        };

        static byte[] packet4Zone = new byte[]
        {
            0,  1,  2,  3,  0,  0,
            7,  7,  6,  5,  4,  4,
        };

        static byte[] packet4ZoneFlipped = new byte[]
        {
            0,  1,  2,  3,  0,  0,
            4,  4,  5,  6,  7,  7,
        };

        public static void ApplyDirect(Color[] color, bool init = false)
        {
            if (!backlight) return;

            const byte keySet = 167;
            const byte ledCount = 178;
            const ushort mapSize = 3 * ledCount;
            const byte ledsPerPacket = 16;

            byte[] buffer = new byte[64];
            byte[] keyBuf = new byte[mapSize];

            buffer[0] = AsusHid.AURA_ID;
            buffer[1] = 0xBC;
            buffer[2] = 0;
            buffer[3] = 1;
            buffer[4] = 1;
            buffer[5] = 1;
            buffer[6] = 0;
            buffer[7] = 0x10;

            if (init || initDirect)
            {
                initDirect = false;
                AsusHid.SetFeatureAura(new byte[] { AsusHid.AURA_ID, 0xBC, (byte)(IsOldStrix ? 0 : 1) });
                Thread.Sleep(50);
            }

            Array.Clear(keyBuf, 0, keyBuf.Length);

            if (!isStrix4Zone)
            {
                for (int ledIndex = 0; ledIndex < packetMap.Count(); ledIndex++)
                {
                    ushort offset = (ushort)(3 * packetMap[ledIndex]);
                    byte zone = packetZone[ledIndex];

                    keyBuf[offset] = color[zone].R;
                    keyBuf[offset + 1] = color[zone].G;
                    keyBuf[offset + 2] = color[zone].B;
                }

                for (int i = 0; i < keySet; i += ledsPerPacket)
                {
                    byte ledsRemaining = (byte)(keySet - i);

                    if (ledsRemaining < ledsPerPacket)
                    {
                        buffer[7] = ledsRemaining;
                    }

                    buffer[6] = (byte)i;
                    Buffer.BlockCopy(keyBuf, 3 * i, buffer, 9, 3 * buffer[7]);
                    AsusHid.SetFeatureAura(buffer);
                    Thread.Sleep(1);
                }
            }

            buffer[4] = 0x04;
            buffer[5] = 0x00;
            buffer[6] = 0x00;
            buffer[7] = 0x00;

            if (isStrix4Zone)
            {
                var map = isStrix4ZoneFlipped ? packet4ZoneFlipped : packet4Zone;
                var leds_4_zone = map.Count();
                for (int ledIndex = 0; ledIndex < leds_4_zone; ledIndex++)
                {
                    byte zone = map[ledIndex];
                    keyBuf[ledIndex * 3] = color[zone].R;
                    keyBuf[ledIndex * 3 + 1] = color[zone].G;
                    keyBuf[ledIndex * 3 + 2] = color[zone].B;
                }
                Buffer.BlockCopy(keyBuf, 0, buffer, 9, 3 * leds_4_zone);
                AsusHid.SetFeatureAura(buffer);
                Thread.Sleep(1);
                return;
            }

            Buffer.BlockCopy(keyBuf, 3 * keySet, buffer, 9, 3 * (ledCount - keySet));
            AsusHid.SetFeatureAura(buffer);
        }

        public static void ApplyDirectLightbar(Color[] color)
        {
            var map = isStrix4ZoneFlipped ? packet4ZoneFlipped : packet4Zone;
            byte[] buffer = new byte[64];
            buffer[0] = AsusHid.AURA_ID;
            buffer[1] = 0xBC;
            buffer[2] = 0;
            buffer[3] = 1;
            buffer[4] = 0x04;

            for (int i = 0; i < map.Length; i++)
            {
                byte zone = map[i];
                int o = 9 + i * 3;
                buffer[o] = color[zone].R;
                buffer[o + 1] = color[zone].G;
                buffer[o + 2] = color[zone].B;
            }

            AsusHid.SetFeatureAura(buffer);
        }

        public static void ApplyDirect(Color color, bool init = false)
        {
            if (!backlight) return;

            if (isStrix)
            {
                ApplyDirect(Enumerable.Repeat(color, AURA_ZONES).ToArray(), init);
                return;
            }

            if (init || initDirect)
            {
                initDirect = false;
                AsusHid.SetFeatureAura(new byte[] { AsusHid.AURA_ID, 0xBC, 1 });
                Thread.Sleep(50);
            }

            byte[] buffer = new byte[12];
            buffer[0] = AsusHid.AURA_ID;
            buffer[1] = 0xbc;
            buffer[2] = 1;
            buffer[3] = 1;
            buffer[9] = color.R;
            buffer[10] = color.G;
            buffer[11] = color.B;

            AsusHid.SetFeatureAura(buffer);
        }

        public static Color ColorDim(Color Color, double colorDim = 1)
        {
            switch (InputDispatcher.GetBacklight())
            {
                case 1: colorDim = 0.1; break;
                case 2: colorDim = 0.3; break;
            }
            return Color.FromArgb((int)(Color.R * colorDim), (int)(Color.G * colorDim), (int)(Color.B * colorDim));
        }

        public static void ApplyRearLight()
        {
        }

        public static void ApplyAura()
        {
            Mode = (AuraMode)AppConfig.Get("aura_mode");
            Speed = (AuraSpeed)AppConfig.Get("aura_speed");
            SetColor(AppConfig.Get("aura_color"));
            SetColor2(AppConfig.Get("aura_color2"));

            Color _Color1 = Color1;
            Color _Color2 = Color2;

            timer.Stop();
            if (Mode != AuraMode.AUDIO && Mode != AuraMode.AUDIOPULSE) StopAudio();

            Logger.WriteLine($"AuraMode: {Mode}");

            if (Mode == AuraMode.AUDIO || Mode == AuraMode.AUDIOPULSE)
            {
                StartAudio();
                return;
            }

            if (Mode == AuraMode.HEATMAP)
            {
                CustomRGB.ApplyHeatmap(true);
                timer.Interval = 2000;
                timer.Start();
                return;
            }

            if (Mode == AuraMode.BATTERY)
            {
                CustomRGB.ApplyBattery();
                timer.Interval = 30000;
                timer.Start();
                return;
            }

            if (Mode == AuraMode.AMBIENT)
            {
                CustomRGB.ApplyAmbient(true);
                timer.Interval = AppConfig.Get("aura_refresh", 100);
                timer.Start();
                return;
            }

            if (Mode == AuraMode.FOURZONE)
            {
                CustomRGB.ApplyFourZone();
                return;
            }

            if (Mode == AuraMode.GRADIENT)
            {
                CustomRGB.ApplyGradient();
                return;
            }

            if (Mode == AuraMode.ZONETEST)
            {
                CustomRGB.ApplyZoneTest();
                return;
            }

            if (Mode == AuraMode.GPUMODE)
            {
                CustomRGB.ApplyGPUColor();
                return;
            }

            int _speed = (Speed == AuraSpeed.Normal) ? 0xeb : (Speed == AuraSpeed.Fast) ? 0xf5 : 0xe1;

            AsusHid.Write(new List<byte[]> { AuraMessage(Mode, _Color1, _Color2, _speed), MESSAGE_SET, MESSAGE_APPLY }, "Aura", AsusHid.MAIN_AURA_PIDS);
        }

        public static void StopAudio()
        {
            AudioVisualizer.Shared.Unsubscribe(OnAudioSpectrum);
        }

        public static void StartAudio()
        {
            if (!backlight) return;

            initDirect = true;
            audioMaxes.Clear();
            lastAudioPresent = 0;
            envBrightness = 0;
            smoothedHue = 0;

            AudioVisualizer.Shared.Subscribe(OnAudioSpectrum);
        }

        private static void OnAudioSpectrum(double[] fftMag)
        {
            if (!backlight || sessionLock) return;
            if (Mode != AuraMode.AUDIO && Mode != AuraMode.AUDIOPULSE) return;

            long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            if (Math.Abs(now - lastAudioPresent) < 50) return;
            lastAudioPresent = now;

            int bands = AURA_ZONES;
            if (fftMag.Length < bands) return;

            double[] bars = new double[bands];
            double max = 0;
            for (int i = 0; i < bands; i++)
            {
                bars[i] = Math.Sqrt(fftMag[i] * 10000);
                if (bars[i] > max) max = bars[i];
            }

            audioMaxes.Add(max);
            if (audioMaxes.Count > 100) audioMaxes.RemoveAt(0);
            double maxAvg = audioMaxes.OrderByDescending(x => x).ElementAt(audioMaxes.Count / 10);
            if (maxAvg < 1) maxAvg = 1;

            envBrightness = Math.Max(envBrightness * audioDecay, max);
            double brightness = Math.Min(1.0, envBrightness / maxAvg);

            Color c1 = Color1;
            double curvedBrightness = brightness * brightness * brightness;

            try
            {
                if (Mode == AuraMode.AUDIOPULSE)
                {
                    Color dimmed = Color.FromArgb(
                        (byte)(c1.R * curvedBrightness),
                        (byte)(c1.G * curvedBrightness),
                        (byte)(c1.B * curvedBrightness));
                    if (isStrix) ApplyDirect(Enumerable.Repeat(dimmed, AURA_ZONES).ToArray());
                    else ApplyDirect(dimmed);
                    return;
                }

                double baseHue = ColorUtils.HSV.ToHSV(c1).Hue;

                if (isStrix)
                {
                    Color[] colors = new Color[AURA_ZONES];
                    for (int i = 0; i < AURA_ZONES; i++)
                    {
                        double hue = (baseHue + (double)i / (AURA_ZONES - 1) * (2.0 / 3.0)) % 1.0;
                        double ratio = Math.Min(1.0, bars[i] / maxAvg);
                        double v = ratio * ratio * ratio;
                        colors[i] = new ColorUtils.HSV { Hue = hue, Saturation = 1.0, Value = v }.ToRGB();
                    }
                    ApplyDirect(colors);
                }
                else
                {
                    int dominant = 1;
                    double dominantWeighted = bars[1];
                    for (int i = 2; i < bands; i++)
                    {
                        double w = bars[i] * (1 + (i - 1) * 0.15);
                        if (w > dominantWeighted) { dominantWeighted = w; dominant = i; }
                    }
                    if (max > maxAvg * 0.3)
                    {
                        double targetHue = (baseHue + (dominant - 1) / (double)(bands - 2) * (2.0 / 3.0)) % 1.0;
                        smoothedHue = smoothedHue * 0.6 + targetHue * 0.4;
                    }

                    ApplyDirect(new ColorUtils.HSV { Hue = smoothedHue, Saturation = 1.0, Value = curvedBrightness }.ToRGB());
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Aura audio: " + ex.Message);
            }
        }

        public static class CustomRGB
        {
            static int tempFreeze = AppConfig.Get("temp_freeze", 35);
            static int tempCold = AppConfig.Get("temp_cold", 50);
            static int tempWarm = AppConfig.Get("temp_warm", 72);
            static int tempHot = AppConfig.Get("temp_hot", 88);

            static Color colorFreeze = ColorTranslator.FromHtml(AppConfig.GetString("color_freeze", "#06B48A")!);
            static Color colorCold = ColorTranslator.FromHtml(AppConfig.GetString("color_cold", "#3AAEEF")!);
            static Color colorWarm = ColorTranslator.FromHtml(AppConfig.GetString("color_warm", "#F59E0B")!);
            static Color colorHot = ColorTranslator.FromHtml(AppConfig.GetString("color_hot", "#FF3B30")!);

            static float battLow = 20f;
            static float battMid = 60f;
            static float battHigh = 100f;

            static Color colorLow = Color.FromArgb(255, 59, 48);
            static Color colorMid = Color.FromArgb(245, 158, 11);
            static Color colorHigh = Color.FromArgb(6, 180, 138);

            static Color colorUltimate = ColorTranslator.FromHtml(AppConfig.GetString("color_ultimate", "#FF3B30")!);
            static Color colorStandard = ColorTranslator.FromHtml(AppConfig.GetString("color_standard", "#3AAEEF")!);
            static Color colorEco = ColorTranslator.FromHtml(AppConfig.GetString("color_eco", "#06B48A")!);

            public static void ApplyFourZone()
            {
                Color[] colors = new Color[AURA_ZONES];
                for (int z = 0; z < 4; z++)
                {
                    colors[z] = GetZoneColor(z);
                }

                // Map Lightbar zones (4..7) to match the 4 physical keyboard zones (left to right on G513RM)
                int[] lightbarOrder = new int[] { 7, 6, 4, 5 };
                for (int i = 0; i < 4; i++)
                {
                    colors[lightbarOrder[i]] = colors[i];
                }

                ApplyDirect(colors, true);
                ApplyDirect(colors);
            }

            public static void ApplyGradient()
            {
                Color[] colors = new Color[AURA_ZONES];

                for (int z = 0; z < 4; z++)
                {
                    float t = z / 3f;
                    colors[z] = ColorUtils.GetWeightedAverage(Aura.Color2, Aura.Color1, t);
                }

                int[] lightbarOrder = new int[] { 7, 6, 4, 5 };
                for (int i = 0; i < lightbarOrder.Length; i++)
                {
                    float t = i / 3f;
                    colors[lightbarOrder[i]] = ColorUtils.GetWeightedAverage(Aura.Color2, Aura.Color1, t);
                }

                ApplyDirect(colors, true);
                ApplyDirect(colors);
            }

            public static void ApplyZoneTest()
            {
                Color[] colors = new Color[]
                {
                    Color.FromArgb(0xFF, 0x00, 0x00),
                    Color.FromArgb(0xFF, 0x80, 0x00),
                    Color.FromArgb(0xFF, 0xFF, 0x00),
                    Color.FromArgb(0x00, 0xFF, 0x00),
                    Color.FromArgb(0x00, 0xFF, 0xFF),
                    Color.FromArgb(0x00, 0x00, 0xFF),
                    Color.FromArgb(0xFF, 0x00, 0xFF),
                    Color.FromArgb(0xFF, 0xFF, 0xFF),
                };

                ApplyDirect(colors, true);
                ApplyDirectLightbar(colors);
            }

            public static void ApplyGPUColor(int gpuMode = -1)
            {
                if ((AuraMode)AppConfig.Get("aura_mode") != AuraMode.GPUMODE) return;

                Color color;

                if (gpuMode < 0) gpuMode = GPUModeControl.gpuMode;
                switch (gpuMode)
                {
                    case AsusACPI.GPUModeUltimate:
                        color = colorUltimate;
                        break;
                    case AsusACPI.GPUModeEco:
                        color = colorEco;
                        break;
                    default:
                        color = colorStandard;
                        break;
                }

                AsusHid.Write(new List<byte[]> { AuraMessage(AuraMode.AuraStatic, color, color, 0xeb), MESSAGE_APPLY, MESSAGE_SET });
            }

            public static void ApplyHeatmap(bool init = false)
            {
                float cpuTemp = (float)(HardwareControl.GetCPUTemp() ?? 50f);
                Color color = colorFreeze;

                if (cpuTemp < tempCold) color = ColorUtils.GetWeightedAverage(colorFreeze, colorCold, (cpuTemp - tempFreeze) / (tempCold - tempFreeze));
                else if (cpuTemp < tempWarm) color = ColorUtils.GetWeightedAverage(colorCold, colorWarm, (cpuTemp - tempCold) / (tempWarm - tempCold));
                else if (cpuTemp < tempHot) color = ColorUtils.GetWeightedAverage(colorWarm, colorHot, (cpuTemp - tempWarm) / (tempHot - tempWarm));
                else color = colorHot;

                ApplyDirect(color, init);
            }

            public static void ApplyBattery()
            {
                float battery = (float)HardwareControl.GetBatteryChargePercentage();
                Color color = colorLow;

                if (battery < battLow)
                {
                    color = colorLow;
                }
                else if (battery < battMid)
                {
                    float t = (battery - battLow) / (battMid - battLow);
                    color = ColorUtils.GetWeightedAverage(colorLow, colorMid, t);
                }
                else if (battery < battHigh)
                {
                    float t = (battery - battMid) / (battHigh - battMid);
                    color = ColorUtils.GetWeightedAverage(colorMid, colorHigh, t);
                }
                else
                {
                    color = colorHigh;
                }

                AsusHid.Write(new List<byte[]> { AuraMessage(AuraMode.AuraStatic, color, color, 0xeb), MESSAGE_APPLY, MESSAGE_SET });
            }

            public static void ApplyAmbient(bool init = false)
            {
                if (!backlight || sessionLock) return;
                if (AmbientData.IsMoveSize()) return;

                var bound = Screen.GetBounds(Point.Empty);
                bound.Y += bound.Height / 3;
                bound.Height -= (int)Math.Round(bound.Height * (0.33f + 0.022f));

                Bitmap screen_low = AmbientData.CamptureScreen(bound, 512, 288);
                Bitmap screeb_pxl = AmbientData.ResizeImage(screen_low, 4, 2);

                int zones = AURA_ZONES;

                if (isStrix)
                {
                    var mid_left = ColorUtils.GetMidColor(screeb_pxl.GetPixel(0, 1), screeb_pxl.GetPixel(1, 1));
                    var mid_right = ColorUtils.GetMidColor(screeb_pxl.GetPixel(2, 1), screeb_pxl.GetPixel(3, 1));

                    AmbientData.Colors[4].RGB = ColorUtils.HSV.UpSaturation(screeb_pxl.GetPixel(1, 1));
                    AmbientData.Colors[5].RGB = ColorUtils.HSV.UpSaturation(mid_left);
                    AmbientData.Colors[6].RGB = ColorUtils.HSV.UpSaturation(mid_right);
                    AmbientData.Colors[7].RGB = ColorUtils.HSV.UpSaturation(screeb_pxl.GetPixel(3, 1));

                    for (int i = 0; i < 4; i++)
                        AmbientData.Colors[i].RGB = ColorUtils.HSV.UpSaturation(screeb_pxl.GetPixel(i, 0));
                }
                else
                {
                    zones = 1;
                    AmbientData.Colors[0].RGB = ColorUtils.HSV.UpSaturation(ColorUtils.GetDominantColor(screeb_pxl), (float)0.3);
                }

                screen_low.Dispose();
                screeb_pxl.Dispose();

                bool is_fresh = init;

                for (int i = 0; i < zones; i++)
                {
                    if (AmbientData.result[i].ToArgb() != AmbientData.Colors[i].RGB.ToArgb()) is_fresh = true;
                    AmbientData.result[i] = AmbientData.Colors[i].RGB;
                }

                if (is_fresh)
                {
                    if (isStrix) ApplyDirect(AmbientData.result, init);
                    else ApplyDirect(AmbientData.result[0], init);
                }
            }

            static class AmbientData
            {
                [DllImport("user32.dll")]
                private static extern bool GetGUIThreadInfo(uint idThread, int[] gui);

                public static bool IsMoveSize()
                {
                    int[] gui = new int[18];
                    gui[0] = 72;
                    return GetGUIThreadInfo(0, gui) && (gui[1] & 0x2) != 0;
                }

                public enum StretchMode
                {
                    STRETCH_ANDSCANS = 1,
                    STRETCH_ORSCANS = 2,
                    STRETCH_DELETESCANS = 3,
                    STRETCH_HALFTONE = 4,
                }

                [DllImport("user32.dll")]
                private static extern IntPtr GetDesktopWindow();

                [DllImport("user32.dll")]
                private static extern IntPtr GetWindowDC(IntPtr hWnd);

                [DllImport("gdi32.dll")]
                private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

                [DllImport("gdi32.dll")]
                private static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth, int nHeight);

                [DllImport("gdi32.dll")]
                private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

                [DllImport("user32.dll")]
                private static extern bool ReleaseDC(IntPtr hWnd, IntPtr hDC);

                [DllImport("gdi32.dll")]
                private static extern bool DeleteDC(IntPtr hdc);

                [DllImport("gdi32.dll")]
                private static extern bool DeleteObject(IntPtr hObject);

                [DllImport("gdi32.dll")]
                private static extern bool StretchBlt(IntPtr hdcDest, int nXOriginDest, int nYOriginDest,
                int nWidthDest, int nHeightDest,
                IntPtr hdcSrc, int nXOriginSrc, int nYOriginSrc, int nWidthSrc, int nHeightSrc, Int32 dwRop);

                [DllImport("gdi32.dll")]
                static extern bool SetStretchBltMode(IntPtr hdc, StretchMode iStretchMode);

                public static Bitmap CamptureScreen(Rectangle rec, int out_w, int out_h)
                {
                    IntPtr desktop = GetDesktopWindow();
                    IntPtr hdc = GetWindowDC(desktop);
                    IntPtr hdcMem = CreateCompatibleDC(hdc);

                    IntPtr hBitmap = CreateCompatibleBitmap(hdc, out_w, out_h);
                    IntPtr hOld = SelectObject(hdcMem, hBitmap);
                    SetStretchBltMode(hdcMem, StretchMode.STRETCH_DELETESCANS);
                    StretchBlt(hdcMem, 0, 0, out_w, out_h, hdc, rec.X, rec.Y, rec.Width, rec.Height, 0x00CC0020);
                    SelectObject(hdcMem, hOld);

                    DeleteDC(hdcMem);
                    ReleaseDC(desktop, hdc);
                    var result = Image.FromHbitmap(hBitmap, IntPtr.Zero);
                    DeleteObject(hBitmap);
                    return result;
                }

                public static Bitmap ResizeImage(Image image, int width, int height)
                {
                    var destRect = new Rectangle(0, 0, width, height);
                    var destImage = new Bitmap(width, height);

                    destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

                    using (var graphics = Graphics.FromImage(destImage))
                    {
                        graphics.CompositingMode = CompositingMode.SourceCopy;
                        graphics.CompositingQuality = CompositingQuality.HighQuality;
                        graphics.InterpolationMode = InterpolationMode.Bicubic;
                        graphics.SmoothingMode = SmoothingMode.None;
                        graphics.PixelOffsetMode = PixelOffsetMode.None;

                        using (var wrapMode = new ImageAttributes())
                        {
                            wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                            graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                        }
                    }

                    return destImage;
                }

                static public Color[] result = new Color[AURA_ZONES];
                static public ColorUtils.SmoothColor[] Colors = Enumerable.Repeat(0, AURA_ZONES).
                    Select(h => new ColorUtils.SmoothColor()).ToArray();
            }
        }
    }
}
