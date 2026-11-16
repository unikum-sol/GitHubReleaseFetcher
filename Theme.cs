using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace GitHubReleaseFetcher;

/// <summary>Semantic color tokens. No control uses hard-coded colors; everything comes from here.</summary>
public sealed class Palette
{
    public bool IsDark { get; init; }
    public bool IsHighContrast { get; init; }

    public Color Bg { get; init; }            // window background
    public Color Surface { get; init; }       // cards, header, panels
    public Color Raised { get; init; }        // inputs, hover areas
    public Color Line { get; init; }          // 1px borders / separators
    public Color LineStrong { get; init; }    // input borders
    public Color Text { get; init; }
    public Color TextSecondary { get; init; }
    public Color TextMuted { get; init; }

    public Color Accent { get; init; }
    public Color OnAccent { get; init; }
    public Color AccentHover { get; init; }
    public Color Selection { get; init; }     // selected table row
    public Color Hover { get; init; }         // hovered row / ghost button

    public Color Ok { get; init; }
    public Color Warn { get; init; }
    public Color Error { get; init; }

    public Color ConsoleBg { get; init; }
    public Color ConsoleText { get; init; }

    public static readonly Palette Dark = new()
    {
        IsDark = true,
        Bg = Hex("#12151C"),
        Surface = Hex("#181C25"),
        Raised = Hex("#202532"),
        Line = Hex("#2C3341"),
        LineStrong = Hex("#6E788A"),    // 3.8:1 on Surface (control borders, WCAG 1.4.11)
        Text = Hex("#E4E7EC"),
        TextSecondary = Hex("#A3ACBA"),
        TextMuted = Hex("#8E97A6"),     // >= 5.1:1 on Raised/Surface/Console
        Accent = Hex("#4CC2C4"),
        OnAccent = Hex("#0B1F21"),
        AccentHover = Hex("#6AD3D4"),
        Selection = Hex("#1D3A3E"),
        Hover = Hex("#1E2330"),
        Ok = Hex("#5BC489"),
        Warn = Hex("#E8A33D"),
        Error = Hex("#EF6F6C"),
        ConsoleBg = Hex("#0F1218"),
        ConsoleText = Hex("#C9CED6"),
    };

    public static readonly Palette Light = new()
    {
        IsDark = false,
        Bg = Hex("#F2F0EB"),
        Surface = Hex("#FAF9F6"),
        Raised = Hex("#FDFCFA"),        // no pure white
        Line = Hex("#DEDAD1"),
        LineStrong = Hex("#80858F"),    // 3.5:1 on Surface, 3.6:1 on Raised
        Text = Hex("#1C2028"),
        TextSecondary = Hex("#4F5664"),
        TextMuted = Hex("#5F6672"),     // >= 4.9:1 on Bg/Surface/Console
        Accent = Hex("#0E7C86"),
        OnAccent = Hex("#FAF9F6"),      // 4.7:1 on Accent
        AccentHover = Hex("#0B6870"),
        Selection = Hex("#D5EAEA"),
        Hover = Hex("#EEECE6"),
        Ok = Hex("#1A7046"),            // >= 5.1:1 on Surface/Console
        Warn = Hex("#8F5300"),          // >= 5.2:1 on Surface/Console
        Error = Hex("#B83A36"),
        ConsoleBg = Hex("#EEECE6"),
        ConsoleText = Hex("#2A2F38"),
    };

    /// <summary>Windows high-contrast mode: use the system colors so the user's contrast theme is respected.</summary>
    public static Palette HighContrast() => new()
    {
        IsDark = SystemColors.Window.GetBrightness() < 0.5f,
        IsHighContrast = true,
        Bg = SystemColors.Window,
        Surface = SystemColors.Window,
        Raised = SystemColors.Window,
        Line = SystemColors.WindowText,
        LineStrong = SystemColors.WindowText,
        Text = SystemColors.WindowText,
        TextSecondary = SystemColors.WindowText,
        TextMuted = SystemColors.GrayText,
        Accent = SystemColors.Highlight,
        OnAccent = SystemColors.HighlightText,
        AccentHover = SystemColors.Highlight,
        Selection = SystemColors.Highlight,
        Hover = SystemColors.Window,
        Ok = SystemColors.WindowText,
        Warn = SystemColors.WindowText,
        Error = SystemColors.WindowText,
        ConsoleBg = SystemColors.Window,
        ConsoleText = SystemColors.WindowText,
    };

    private static Color Hex(string h) => ColorTranslator.FromHtml(h);

    /// <summary>Text color on a selected row (high contrast needs HighlightText).</summary>
    public Color SelectionText => IsHighContrast ? SystemColors.HighlightText : Text;
}

public enum ThemeMode { System, Light, Dark }

/// <summary>Central theme state: resolves the palette, fonts and native window styling.</summary>
public static class Theme
{
    public static ThemeMode Mode { get; private set; } = ThemeMode.System;
    public static Palette Current { get; private set; } = Palette.Light;
    public static bool IsDark => Current.IsDark;

    /// <summary>Raised on the UI thread after the palette changed.</summary>
    public static event Action? Changed;

    public static ThemeMode ParseMode(string s) => s switch
    {
        "light" => ThemeMode.Light,
        "dark" => ThemeMode.Dark,
        _ => ThemeMode.System
    };

    public static string ModeToString(ThemeMode m) => m switch
    {
        ThemeMode.Light => "light",
        ThemeMode.Dark => "dark",
        _ => "system"
    };

    /// <summary>Sets the mode and re-resolves the palette. Raises <see cref="Changed"/> if anything changed.</summary>
    public static void SetMode(ThemeMode mode)
    {
        Mode = mode;
        Refresh();
    }

    /// <summary>Re-reads the Windows setting (for ThemeMode.System / high contrast) and updates the palette.</summary>
    public static void Refresh()
    {
        Palette next;
        if (SystemInformation.HighContrast) next = Palette.HighContrast();
        else
        {
            bool dark = Mode switch
            {
                ThemeMode.Dark => true,
                ThemeMode.Light => false,
                _ => SystemPrefersDark()
            };
            next = dark ? Palette.Dark : Palette.Light;
        }

        bool changed = next.IsHighContrast || Current.IsHighContrast
            ? Signature(next) != Signature(Current)
            : !ReferenceEquals(next, Current);
        Current = next;
        if (changed) Changed?.Invoke();
    }

    private static (bool, Color, Color, Color, Color) Signature(Palette p) => (p.IsHighContrast, p.Bg, p.Text, p.Accent, p.OnAccent);

    /// <summary>HKCU\...\Themes\Personalize\AppsUseLightTheme (0 = dark). Missing value = light.</summary>
    public static bool SystemPrefersDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            object? v = key?.GetValue("AppsUseLightTheme");
            return v is int i && i == 0;
        }
        catch
        {
            return false;
        }
    }

    // ------------------------------------------------------------------ fonts
    private static readonly HashSet<string> InstalledFamilies = LoadFamilies();

    private static HashSet<string> LoadFamilies()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var f in FontFamily.Families) set.Add(f.Name);
        }
        catch { /* ignore */ }
        return set;
    }

    private static string Pick(params string[] names)
    {
        foreach (var n in names)
            if (InstalledFamilies.Contains(n)) return n;
        return SystemFonts.MessageBoxFont?.FontFamily.Name ?? "Segoe UI";
    }

    private static readonly string UiFamily = Pick("Segoe UI Variable Text", "Segoe UI");
    private static readonly string UiSemiFamily = Pick("Segoe UI Semibold", "Segoe UI Variable Text Semibold", "Segoe UI");
    private static readonly string MonoFamily = Pick("Cascadia Mono", "Consolas", "Courier New");
    // Bahnschrift (DIN-like, Windows 10 1709+) gives the retro-futuristic "instrument" touch for captions and numbers only.
    private static readonly string DisplayFamily = Pick("Bahnschrift SemiBold", "Bahnschrift", "Segoe UI Semibold", "Segoe UI");
    private static readonly bool DisplayIsBold = !DisplayFamily.Contains("SemiBold", StringComparison.OrdinalIgnoreCase) &&
                                                 !DisplayFamily.Contains("Semibold", StringComparison.OrdinalIgnoreCase);

    private static readonly string IconFamilyName = Pick("Segoe Fluent Icons", "Segoe MDL2 Assets");
    public static bool HasIconFont => IconFamilyName is "Segoe Fluent Icons" or "Segoe MDL2 Assets";

    public static readonly Font Ui = new(UiFamily, 9.75f, FontStyle.Regular);
    public static readonly Font UiSemi = new(UiSemiFamily, 9.75f, UiSemiFamily == UiFamily ? FontStyle.Bold : FontStyle.Regular);
    public static readonly Font UiSmall = new(UiFamily, 9f, FontStyle.Regular);
    public static readonly Font Mono = new(MonoFamily, 9.75f, FontStyle.Regular);
    public static readonly Font MonoSmall = new(MonoFamily, 9f, FontStyle.Regular);
    /// <summary>Small caps captions (section titles, tile labels, column headers).</summary>
    public static readonly Font Caption = new(DisplayFamily, 9f, DisplayIsBold ? FontStyle.Bold : FontStyle.Regular);
    /// <summary>Card section titles ("01 // REPOSITORIES"): one step above captions.</summary>
    public static readonly Font SectionTitle = new(DisplayFamily, 11f, DisplayIsBold ? FontStyle.Bold : FontStyle.Regular);
    /// <summary>Table column headers (Segoe semibold, deliberately not the display font).</summary>
    public static readonly Font ColumnHeader = new(UiSemiFamily, 9f, UiSemiFamily == UiFamily ? FontStyle.Bold : FontStyle.Regular);
    /// <summary>App title in the header.</summary>
    public static readonly Font Title = new(DisplayFamily, 12f, DisplayIsBold ? FontStyle.Bold : FontStyle.Regular);
    /// <summary>Large values in the instrument tiles.</summary>
    public static readonly Font Metric = new(DisplayFamily, 17f, DisplayIsBold ? FontStyle.Bold : FontStyle.Regular);
    public static readonly Font Icon = new(IconFamilyName, 10f, FontStyle.Regular);
    public static readonly Font IconSmall = new(IconFamilyName, 8f, FontStyle.Regular);

    // Segoe MDL2 / Fluent icon code points (identical in both fonts)
    public const string IcoSettings = "";
    public const string IcoSync = "";
    public const string IcoFolder = "";
    public const string IcoOpen = "";
    public const string IcoChevronDown = "";
    public const string IcoChevronUp = "";
    public const string IcoClose = "";
    public const string IcoAdd = "";
    public const string IcoDelete = "";
    public const string IcoSave = "";
    public const string IcoDocument = "";
    public const string IcoMinus = "";

    // ------------------------------------------------------------------ native styling
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hwnd, string? appName, string? idList);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);

    private const int WM_SETREDRAW = 0x000B;
    private const uint RDW_INVALIDATE = 0x0001, RDW_ERASE = 0x0004, RDW_ALLCHILDREN = 0x0080, RDW_FRAME = 0x0400;

    private static int ToColorRef(Color c) => c.R | (c.G << 8) | (c.B << 16);

    /// <summary>Dark/light title bar (Windows 10 2004+ / Windows 11) and caption colors (Windows 11). Errors are ignored.</summary>
    public static void ApplyTitleBar(Form f)
    {
        if (!f.IsHandleCreated || !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763)) return;
        try
        {
            int dark = IsDark ? 1 : 0;
            int attr = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 18985) ? 20 : 19; // DWMWA_USE_IMMERSIVE_DARK_MODE
            DwmSetWindowAttribute(f.Handle, attr, ref dark, sizeof(int));

            if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) && f.Visible)
            {
                // Windows 10 repaints the frame only on activation changes: toggle WM_NCACTIVATE once
                const int WM_NCACTIVATE = 0x0086;
                bool active = Form.ActiveForm == f;
                SendMessage(f.Handle, WM_NCACTIVATE, active ? IntPtr.Zero : (IntPtr)1, IntPtr.Zero);
                SendMessage(f.Handle, WM_NCACTIVATE, active ? (IntPtr)1 : IntPtr.Zero, IntPtr.Zero);
            }

            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) && !Current.IsHighContrast)
            {
                int caption = ToColorRef(Current.Surface);
                int text = ToColorRef(Current.Text);
                int border = ToColorRef(Current.Line);
                DwmSetWindowAttribute(f.Handle, 35, ref caption, sizeof(int)); // DWMWA_CAPTION_COLOR
                DwmSetWindowAttribute(f.Handle, 36, ref text, sizeof(int));    // DWMWA_TEXT_COLOR
                DwmSetWindowAttribute(f.Handle, 34, ref border, sizeof(int));  // DWMWA_BORDER_COLOR
            }
        }
        catch { /* not supported */ }
    }

    /// <summary>Dark scrollbars for native controls (Windows 10 1809+). Errors are ignored.</summary>
    public static void ApplyScrollbars(Control c)
    {
        if (!c.IsHandleCreated) return;
        try { SetWindowTheme(c.Handle, IsDark && !Current.IsHighContrast ? "DarkMode_Explorer" : "Explorer", null); }
        catch { /* not supported */ }
    }

    /// <summary>Runs an update without intermediate repaints (avoids flicker while re-theming).</summary>
    public static void WithoutRedraw(Control root, Action update)
    {
        // WM_SETREDRAW(TRUE) sets WS_VISIBLE: only use it on visible windows (window may be hidden in the tray)
        bool handle = root.IsHandleCreated && root.Visible;
        if (handle) SendMessage(root.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
        root.SuspendLayout();
        try { update(); }
        finally
        {
            root.ResumeLayout(true);
            if (handle)
            {
                SendMessage(root.Handle, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
                RedrawWindow(root.Handle, IntPtr.Zero, IntPtr.Zero, RDW_INVALIDATE | RDW_ERASE | RDW_ALLCHILDREN | RDW_FRAME);
            }
        }
    }
}
