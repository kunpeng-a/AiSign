using System;
using System.Windows;
using System.Windows.Media;

namespace AiSign.UI
{
    internal sealed class Palette
    {
        public bool Dark;
        public Color Bg;
        public Color Surface;
        public Color SurfaceAlt;
        public Color Sidebar;
        public Color Border;
        public Color Text;
        public Color Muted;
        public Color Accent;
        public Color AccentHover;
        public Color Success;
        public Color Warning;
        public Color Danger;
        public Color Input;
    }

    internal static class Theme
    {
        public static Palette Current { get; private set; }
        public static event Action Changed;

        static Theme()
        {
            Current = Dark();
        }

        public static Palette Dark()
        {
            Palette p = new Palette();
            p.Dark = true;
            p.Bg = C(0x0F, 0x11, 0x15);
            p.Surface = C(0x17, 0x1A, 0x21);
            p.SurfaceAlt = C(0x1E, 0x22, 0x2B);
            p.Sidebar = C(0x12, 0x15, 0x1B);
            p.Border = C(0x26, 0x2B, 0x36);
            p.Text = C(0xE8, 0xEA, 0xF0);
            p.Muted = C(0x9A, 0xA3, 0xB2);
            p.Accent = C(0x8B, 0x6D, 0xFF);
            p.AccentHover = C(0x9B, 0x80, 0xFF);
            p.Success = C(0x34, 0xD3, 0x99);
            p.Warning = C(0xFB, 0xBF, 0x24);
            p.Danger = C(0xF8, 0x71, 0x71);
            p.Input = C(0x11, 0x14, 0x1A);
            return p;
        }

        public static Palette Light()
        {
            Palette p = new Palette();
            p.Dark = false;
            p.Bg = C(0xF6, 0xF7, 0xFB);
            p.Surface = C(0xFF, 0xFF, 0xFF);
            p.SurfaceAlt = C(0xF1, 0xF3, 0xF8);
            p.Sidebar = C(0xFF, 0xFF, 0xFF);
            p.Border = C(0xE3, 0xE6, 0xEE);
            p.Text = C(0x1B, 0x1D, 0x23);
            p.Muted = C(0x6B, 0x72, 0x80);
            p.Accent = C(0x7C, 0x5C, 0xFF);
            p.AccentHover = C(0x6A, 0x4A, 0xF0);
            p.Success = C(0x16, 0xA3, 0x4A);
            p.Warning = C(0xD9, 0x77, 0x06);
            p.Danger = C(0xDC, 0x26, 0x26);
            p.Input = C(0xF6, 0xF7, 0xFB);
            return p;
        }

        private static Color C(byte r, byte g, byte b)
        {
            return Color.FromRgb(r, g, b);
        }

        public static SolidColorBrush Brush(Color c)
        {
            SolidColorBrush b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        public static SolidColorBrush Soft(Color c, byte alpha)
        {
            SolidColorBrush b = new SolidColorBrush(Color.FromArgb(alpha, c.R, c.G, c.B));
            b.Freeze();
            return b;
        }

        public static ResourceDictionary Build(Palette p)
        {
            ResourceDictionary d = new ResourceDictionary();
            d["BgBrush"] = Brush(p.Bg);
            d["SurfaceBrush"] = Brush(p.Surface);
            d["SurfaceAltBrush"] = Brush(p.SurfaceAlt);
            d["SidebarBrush"] = Brush(p.Sidebar);
            d["BorderBrush"] = Brush(p.Border);
            d["TextBrush"] = Brush(p.Text);
            d["MutedBrush"] = Brush(p.Muted);
            d["AccentBrush"] = Brush(p.Accent);
            d["AccentHoverBrush"] = Brush(p.AccentHover);
            d["SuccessBrush"] = Brush(p.Success);
            d["WarningBrush"] = Brush(p.Warning);
            d["DangerBrush"] = Brush(p.Danger);
            d["InputBrush"] = Brush(p.Input);
            d["WhiteBrush"] = Brushes.White;
            d["TransparentBrush"] = Brushes.Transparent;
            d["AccentSoftBrush"] = Soft(p.Accent, 34);
            d["SuccessSoftBrush"] = Soft(p.Success, 30);
            d["WarningSoftBrush"] = Soft(p.Warning, 30);
            d["DangerSoftBrush"] = Soft(p.Danger, 30);
            d["MutedSoftBrush"] = Soft(p.Muted, 24);
            Color trae = Color.FromRgb(0x3B, 0x82, 0xF6);
            Color wb = Color.FromRgb(0x14, 0xB8, 0xA6);
            Color qoder = Color.FromRgb(0x8B, 0x5C, 0xF6);
            d["TraeBrush"] = Brush(trae);
            d["WorkBuddyBrush"] = Brush(wb);
            d["QoderBrush"] = Brush(qoder);
            d["TraeSoftBrush"] = Soft(trae, 36);
            d["WorkBuddySoftBrush"] = Soft(wb, 36);
            d["QoderSoftBrush"] = Soft(qoder, 36);
            d["BodyFont"] = new FontFamily("Microsoft YaHei UI, Segoe UI");
            d["CardRadius"] = new CornerRadius(12);
            d["ButtonRadius"] = new CornerRadius(8);
            return d;
        }

        public static void Install(Application app)
        {
            app.Resources.MergedDictionaries.Clear();
            app.Resources.MergedDictionaries.Add(Build(Current));
            Styles.Install(app);
        }

        public static void Apply(string theme)
        {
            string t = string.IsNullOrEmpty(theme) ? "dark" : theme.ToLowerInvariant();
            Palette next;
            if (t == "system")
            {
                bool light = false;
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object v = key == null ? null : key.GetValue("AppsUseLightTheme");
                    light = v != null && Convert.ToInt32(v) == 1;
                }
                next = light ? Light() : Dark();
            }
            else next = t == "light" ? Light() : Dark();
            Current = next;

            Application app = Application.Current;
            if (app != null)
            {
                app.Resources.MergedDictionaries.Clear();
                app.Resources.MergedDictionaries.Add(Build(Current));
            }
            Action h = Changed;
            if (h != null) h();
        }
    }
}
