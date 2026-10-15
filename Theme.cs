using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace HSUIKit
{
    // Every visual knob of the kit in one place. Backed by Plugins/HSUIKit/theme.txt, which is
    // only polled (one mtime check per second) while a KitWindow is visible, and only re-parsed
    // when that mtime actually changes - so the idle cost is zero and the open-window cost is
    // one stat call per second.
    public static class Theme
    {
        public const string FilePath = "Plugins/HSUIKit/theme.txt";

        public static Color Accent;
        public static Color Background;
        public static Color Surface;
        public static Color SurfaceHover;
        public static Color SurfaceActive;
        public static Color Text;
        public static Color TextDim;
        public static Color Border;
        public static Color Success;
        public static Color Warning;
        public static Color Error;
        // Added for the Candy presets (1.3.0). Glass Dark keeps the old look: no title bar fill,
        // cards = Surface, tracks = SurfaceActive, Positive = Success, Warm = Warning.
        public static Color TitleBar;      // window title bar fill (transparent = none)
        public static Color TitleText;     // title + title-bar icons
        public static Color Card;          // KitCard background
        public static Color Track;         // slider / progress / switch track
        public static Color Positive;      // ButtonStyle.Positive (e.g. "Equip", "Apply")
        public static Color Warm;          // ButtonStyle.Warm (e.g. "Search", highlights)
        public static bool PillButtons;    // fully round buttons
        public static bool PillTabs;       // KitTabs as filled pills instead of an underline
        public static bool TitleCentered;
        public static string Preset;       // "glass-dark", "candy-light", "candy-dark"
        public static readonly string[] Presets = { "glass-dark", "candy-light", "candy-dark" };
        public static readonly string[] PresetNames = { "Glass Dark", "Candy Light", "Candy Dark" };

        public static int Radius;
        public static int RadiusSmall;
        public static int FontSize;
        public static int TitleSize;
        public static int SmallSize;
        public static int ShadowSize;
        public static float ShadowAlpha;
        public static float UIScale;
        public static float AnimSpeed;
        public static string FontName;

        public static event VoidHandler Changed;

        // ---- derived colours for custom-drawn canvases (timelines, graphs, previews) ----
        // Light theme = light window background. Lines and dimming must flip with it: white lines vanish on a
        // light canvas and black overlays turn it muddy grey.
        public static bool IsLight { get { return Background.r * 0.3f + Background.g * 0.59f + Background.b * 0.11f > 0.5f; } }

        // Background of a drawn area: a recessed well, darker than the panel on both kinds of theme.
        public static Color Well { get { return IsLight ? Kit.Shade(Background, -0.07f) : Kit.Shade(Surface, -0.28f); } }

        // Background of a list box (bone trees, favourites, editors): a light card on light themes, a recessed
        // dark box on dark ones. (Shade(Surface, -0.12) turned muddy grey on Candy Light.)
        public static Color ListBg { get { return IsLight ? Card : Kit.Shade(Surface, -0.12f); } }

        // A grid line at the given strength (the alpha it has on a dark theme).
        public static Color GridLine(float alpha) { return IsLight ? Kit.Alpha(Text, alpha * 1.3f) : new Color(1f, 1f, 1f, alpha); }

        // Overlay that dims part of a drawn area (out of range, disabled rows) at the given dark-theme strength.
        public static Color DimOverlay(float alpha) { return IsLight ? Kit.Alpha(Text, alpha * 0.35f) : new Color(0f, 0f, 0f, alpha); }

        private static int _version;
        private static bool _loaded;
        private static DateTime _fileTime;
        private static float _nextPoll;

        public static int Version { get { return _version; } }

        static Theme()
        {
            SetDefaults();
        }

        public static void SetDefaults() { SetDefaults(Preset ?? "glass-dark"); }

        // Fresh values for a preset; theme.txt keys are applied on top of these.
        public static void SetDefaults(string preset)
        {
            Preset = Array.IndexOf(Presets, preset) >= 0 ? preset : "glass-dark";
            GlassDark();
            if (Preset == "candy-light") CandyLight();
            else if (Preset == "candy-dark") CandyDark();
        }

        private static void CandyLight()
        {
            // Muted "paper" greys rather than pure white: full white panels were glaring over a dark scene.
            Accent = Hex("#2FA4AC"); Background = Hex("#D3DAE3FA"); Surface = Hex("#E8EDF2FF");
            SurfaceHover = Hex("#DEE4EBFF"); SurfaceActive = Hex("#CDD5DFFF");
            Text = Hex("#262D38FF"); TextDim = Hex("#555F6EFF"); Border = Hex("#0000001A");
            Success = Hex("#58C772FF"); Warning = Hex("#F7A440FF"); Error = Hex("#F2668BFF");
            TitleBar = Hex("#2FA4ACFF"); TitleText = Hex("#FFFFFFFF"); Card = Hex("#E8EDF2FF"); Track = Hex("#C3CCD7FF");
            Positive = Hex("#58C772FF"); Warm = Hex("#F7A440FF");
            Radius = 16; RadiusSmall = 12; ShadowSize = 18; ShadowAlpha = 0.18f;
            PillButtons = true; PillTabs = true; TitleCentered = true;
            FontName = "Arial";   // measured crisper than Segoe UI in Unity 5.3's text renderer at 14 px
        }

        private static void CandyDark()
        {
            Accent = Hex("#3AA7AE"); Background = Hex("#1F2430F5"); Surface = Hex("#2B3240FF");
            SurfaceHover = Hex("#333B4BFF"); SurfaceActive = Hex("#3A4252FF");
            Text = Hex("#F0F3F8FF"); TextDim = Hex("#B8C0CEFF"); Border = Hex("#FFFFFF14");
            Success = Hex("#4DB866FF"); Warning = Hex("#E9953AFF"); Error = Hex("#E35A7EFF");
            TitleBar = Hex("#3AA7AEFF"); TitleText = Hex("#FFFFFFFF"); Card = Hex("#2B3240FF"); Track = Hex("#3A4252FF");
            Positive = Hex("#4DB866FF"); Warm = Hex("#E9953AFF");
            Radius = 16; RadiusSmall = 12; ShadowSize = 20; ShadowAlpha = 0.45f;
            PillButtons = true; PillTabs = true; TitleCentered = true;
            FontName = "Arial";   // measured crisper than Segoe UI in Unity 5.3's text renderer at 14 px
        }

        private static void GlassDark()
        {
            Accent = Hex("#5B8CFF");
            Background = Hex("#15171DEB");
            Surface = Hex("#232730FF");
            SurfaceHover = Hex("#2C313CFF");
            SurfaceActive = Hex("#373D4AFF");
            Text = Hex("#E9ECF2FF");
            TextDim = Hex("#979DABFF");
            Border = Hex("#FFFFFF1A");
            Success = Hex("#3DD68CFF");
            Warning = Hex("#F5B74EFF");
            Error = Hex("#F0616DFF");
            Radius = 10;
            RadiusSmall = 6;
            FontSize = 14;
            TitleSize = 16;
            SmallSize = 12;
            ShadowSize = 20;
            ShadowAlpha = 0.5f;
            UIScale = 1f;
            AnimSpeed = 1f;
            FontName = "Segoe UI";
            TitleBar = Color.clear; TitleText = Text; Card = Surface; Track = SurfaceActive;
            Positive = Success; Warm = Warning;
            PillButtons = false; PillTabs = false; TitleCentered = false;
        }

        // Switches every kit window to a preset, live, and saves it. Hand edits in theme.txt are reset to
        // the new preset's values (only the preset line is kept).
        public static void SetPreset(string preset)
        {
            SetDefaults(preset);
            Save();
            NotifyChanged();
        }

        public static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            if (File.Exists(FilePath)) Load();
            else Save();
        }

        // Re-reads the file on top of fresh defaults, so a key deleted from the file falls back
        // to its default instead of keeping a stale value.
        public static bool Load()
        {
            try
            {
                string[] lines = File.ReadAllLines(FilePath);
                // The preset comes first (whatever line it is on); every other key is a tweak on top of it.
                string preset = "glass-dark";
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    int eq = line.IndexOf('=');
                    if (eq > 0 && line[0] != '#' && line.Substring(0, eq).Trim().ToLowerInvariant() == "preset") preset = line.Substring(eq + 1).Trim().ToLowerInvariant();
                }
                SetDefaults(preset);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.Length == 0 || line[0] == '#') continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    Apply(line.Substring(0, eq).Trim().ToLowerInvariant(), line.Substring(eq + 1).Trim());
                }
                _fileTime = File.GetLastWriteTime(FilePath);
                Log.Write("Theme.Load: loaded " + FilePath);
                return true;
            }
            catch (Exception ex)
            {
                Log.Write("Theme.Load: failed: " + ex.Message);
                return false;
            }
        }

        // Writes the preset plus only the values that differ from it, so a later preset switch is not
        // pinned down by stale lines. An old full theme.txt (no preset line, Glass Dark values) loads the same.
        public static bool Save()
        {
            try
            {
                Dictionary<string, string> mine = Values();
                Dictionary<string, string> preset;
                {
                    Dictionary<string, string> keep = mine;
                    SetDefaults(Preset);
                    preset = Values();
                    foreach (KeyValuePair<string, string> kv in keep) Apply(kv.Key.ToLowerInvariant(), kv.Value);
                }
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# HSUIKit theme - edit and save; open HSUIKit windows restyle live.");
                sb.AppendLine("# preset = glass-dark | candy-light | candy-dark. Lines below it are tweaks on top of the");
                sb.AppendLine("# preset (colors #RRGGBB or #RRGGBBAA). Delete a line to get the preset's value back.");
                sb.AppendLine();
                sb.AppendLine("preset = " + Preset);
                foreach (KeyValuePair<string, string> kv in mine)
                    if (preset[kv.Key] != kv.Value) sb.AppendLine(kv.Key + " = " + kv.Value);

                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, sb.ToString());
                _fileTime = File.GetLastWriteTime(FilePath);
                Log.Write("Theme.Save: wrote " + FilePath);
                return true;
            }
            catch (Exception ex)
            {
                Log.Write("Theme.Save: failed: " + ex.Message);
                return false;
            }
        }

        // Every saved key and its current value, in file order.
        private static Dictionary<string, string> Values()
        {
            var d = new Dictionary<string, string>();
            d["accent"] = ToHex(Accent); d["background"] = ToHex(Background); d["surface"] = ToHex(Surface);
            d["surfaceHover"] = ToHex(SurfaceHover); d["surfaceActive"] = ToHex(SurfaceActive);
            d["text"] = ToHex(Text); d["textDim"] = ToHex(TextDim); d["border"] = ToHex(Border);
            d["success"] = ToHex(Success); d["warning"] = ToHex(Warning); d["error"] = ToHex(Error);
            d["titleBar"] = ToHex(TitleBar); d["titleText"] = ToHex(TitleText); d["card"] = ToHex(Card); d["track"] = ToHex(Track);
            d["positive"] = ToHex(Positive); d["warm"] = ToHex(Warm);
            d["pillButtons"] = PillButtons ? "1" : "0"; d["pillTabs"] = PillTabs ? "1" : "0"; d["titleCentered"] = TitleCentered ? "1" : "0";
            d["radius"] = Radius.ToString(CultureInfo.InvariantCulture); d["radiusSmall"] = RadiusSmall.ToString(CultureInfo.InvariantCulture);
            d["fontName"] = FontName; d["fontSize"] = FontSize.ToString(CultureInfo.InvariantCulture);
            d["titleSize"] = TitleSize.ToString(CultureInfo.InvariantCulture); d["smallSize"] = SmallSize.ToString(CultureInfo.InvariantCulture);
            d["shadowSize"] = ShadowSize.ToString(CultureInfo.InvariantCulture); d["shadowAlpha"] = F(ShadowAlpha);
            d["animSpeed"] = F(AnimSpeed); d["uiScale"] = F(UIScale);
            return d;
        }
        // Called by the kit runner while a window is visible.
        internal static void Poll()
        {
            if (Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + 1f;
            try
            {
                if (!File.Exists(FilePath)) return;
                DateTime t = File.GetLastWriteTime(FilePath);
                if (t == _fileTime) return;
                _fileTime = t;
                if (Load()) NotifyChanged();
            }
            catch { }
        }

        // Call after changing any field from code - restyles every live kit element.
        // Candy themes draw button/tab/segment labels bold: thin regular text at 14 px read as blurry
        // against the light pills (same glyph quality as NeoUIManager's text, just less contrast).
        public static FontStyle LabelStyle { get { return PillButtons ? FontStyle.Bold : FontStyle.Normal; } }

        public static void NotifyChanged()
        {
            _version++;
            Kit.OnThemeChanged();
            KitThemeColor.ApplyAll();   // first: kit widgets and plugin handlers then apply state colours on top
            KitElement.ApplyAll();
            if (Changed != null)
            {
                try { Changed(); }
                catch (Exception ex) { Log.Write("Theme.Changed handler threw: " + ex); }
            }
        }

        private static void Apply(string key, string value)
        {
            switch (key)
            {
                case "accent": Accent = ParseColor(value, Accent); break;
                case "background": Background = ParseColor(value, Background); break;
                case "surface": Surface = ParseColor(value, Surface); break;
                case "surfacehover": SurfaceHover = ParseColor(value, SurfaceHover); break;
                case "surfaceactive": SurfaceActive = ParseColor(value, SurfaceActive); break;
                case "text": Text = ParseColor(value, Text); break;
                case "textdim": TextDim = ParseColor(value, TextDim); break;
                case "border": Border = ParseColor(value, Border); break;
                case "success": Success = ParseColor(value, Success); break;
                case "warning": Warning = ParseColor(value, Warning); break;
                case "error": Error = ParseColor(value, Error); break;
                case "radius": Radius = Mathf.Clamp(ParseInt(value, Radius), 0, 32); break;
                case "radiussmall": RadiusSmall = Mathf.Clamp(ParseInt(value, RadiusSmall), 0, 32); break;
                case "fontname": if (value.Length > 0) FontName = value; break;
                case "fontsize": FontSize = Mathf.Clamp(ParseInt(value, FontSize), 8, 40); break;
                case "titlesize": TitleSize = Mathf.Clamp(ParseInt(value, TitleSize), 8, 48); break;
                case "smallsize": SmallSize = Mathf.Clamp(ParseInt(value, SmallSize), 8, 32); break;
                case "shadowsize": ShadowSize = Mathf.Clamp(ParseInt(value, ShadowSize), 0, 64); break;
                case "shadowalpha": ShadowAlpha = Mathf.Clamp01(ParseFloat(value, ShadowAlpha)); break;
                case "animspeed": AnimSpeed = Mathf.Clamp(ParseFloat(value, AnimSpeed), 0.05f, 10f); break;
                case "uiscale": UIScale = Mathf.Clamp(ParseFloat(value, UIScale), 0.5f, 3f); break;
                case "titlebar": TitleBar = ParseColor(value, TitleBar); break;
                case "titletext": TitleText = ParseColor(value, TitleText); break;
                case "card": Card = ParseColor(value, Card); break;
                case "track": Track = ParseColor(value, Track); break;
                case "positive": Positive = ParseColor(value, Positive); break;
                case "warm": Warm = ParseColor(value, Warm); break;
                case "pillbuttons": PillButtons = value == "1" || value.ToLowerInvariant() == "true"; break;
                case "pilltabs": PillTabs = value == "1" || value.ToLowerInvariant() == "true"; break;
                case "titlecentered": TitleCentered = value == "1" || value.ToLowerInvariant() == "true"; break;
            }
        }

        public static Color Hex(string s)
        {
            return ParseColor(s, Color.magenta);
        }

        public static string ToHex(Color color)
        {
            Color32 c = color;
            return string.Format("#{0:X2}{1:X2}{2:X2}{3:X2}", c.r, c.g, c.b, c.a);
        }

        private static Color ParseColor(string s, Color fallback)
        {
            s = s.Trim().TrimStart('#');
            if (s.Length == 6) s += "FF";
            if (s.Length != 8) return fallback;
            uint v;
            if (!uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v)) return fallback;
            return new Color32((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v);
        }

        private static int ParseInt(string s, int fallback)
        {
            int v;
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : fallback;
        }

        private static float ParseFloat(string s, float fallback)
        {
            float v;
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : fallback;
        }

        private static string F(float v)
        {
            return v.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static void Line(StringBuilder sb, string key, Color c)
        {
            sb.AppendLine(key + " = " + ToHex(c));
        }
    }
}
