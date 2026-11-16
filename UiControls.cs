using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace GitHubReleaseFetcher;

/// <summary>Controls that re-apply colors themselves when the theme changes.</summary>
public interface IThemed
{
    void ApplyTheme();
}

public enum Tone { Neutral, Ok, Warn, Error, Accent, Muted }

/// <summary>Shared drawing and scaling helpers.</summary>
public static class Ui
{
    /// <summary>System DPI scale (the app runs SystemAware).</summary>
    public static readonly float Scale = GetScale();

    private static float GetScale()
    {
        try
        {
            using var g = Graphics.FromHwnd(IntPtr.Zero);
            return Math.Max(1f, g.DpiX / 96f);
        }
        catch { return 1f; }
    }

    /// <summary>Logical (96 dpi) pixels to device pixels.</summary>
    public static int S(int px) => (int)Math.Round(px * Scale);
    public static float Sf(float px) => px * Scale;

    public static Color ToneColor(Tone t) => t switch
    {
        Tone.Ok => Theme.Current.Ok,
        Tone.Warn => Theme.Current.Warn,
        Tone.Error => Theme.Current.Error,
        Tone.Accent => Theme.Current.Accent,
        Tone.Muted => Theme.Current.TextMuted,
        _ => Theme.Current.Text
    };

    public static GraphicsPath RoundRect(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 0.5f) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    /// <summary>Fills a rounded rectangle with an optional 1px border (anti-aliased, pixel aligned).</summary>
    public static void FillRound(Graphics g, Rectangle r, int radius, Color fill, Color? border)
    {
        var old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var rf = new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1f, r.Height - 1f);
        using (var path = RoundRect(rf, radius))
        {
            using (var b = new SolidBrush(fill)) g.FillPath(b, path);
            if (border.HasValue)
                using (var pen = new Pen(border.Value, 1f)) g.DrawPath(pen, path);
        }
        g.SmoothingMode = old;
    }

    public static Color ParentBack(Control c)
    {
        for (var p = c.Parent; p != null; p = p.Parent)
            if (p.BackColor.A == 255) return p.BackColor;
        return Theme.Current.Bg;
    }

    /// <summary>Status glyph: shape differs per state, so the state is never encoded by color alone.</summary>
    public static void DrawStateGlyph(Graphics g, RepoState state, Rectangle box)
    {
        var pal = Theme.Current;
        int d = Math.Min(box.Width, box.Height);
        var r = new RectangleF(box.X + (box.Width - d) / 2f + 0.5f, box.Y + (box.Height - d) / 2f + 0.5f, d - 1f, d - 1f);
        var old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float w = Math.Max(1.5f, Sf(1.6f));
        switch (state)
        {
            case RepoState.Ok:
                using (var b = new SolidBrush(pal.Ok)) g.FillEllipse(b, r);
                break;
            case RepoState.NoReleases:
                using (var p = new Pen(pal.Ok, w)) g.DrawEllipse(p, Inflate(r, -w / 2));
                break;
            case RepoState.NotReachable:
                using (var p = new Pen(pal.Error, w + 0.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    var q = Inflate(r, -d * 0.12f);
                    g.DrawLine(p, q.Left, q.Top, q.Right, q.Bottom);
                    g.DrawLine(p, q.Right, q.Top, q.Left, q.Bottom);
                }
                break;
            case RepoState.RateLimited:
                using (var b = new SolidBrush(pal.Warn))
                {
                    // pause symbol
                    float bw = r.Width * 0.3f;
                    g.FillRectangle(b, r.Left + r.Width * 0.1f, r.Top + r.Height * 0.08f, bw, r.Height * 0.84f);
                    g.FillRectangle(b, r.Right - r.Width * 0.1f - bw, r.Top + r.Height * 0.08f, bw, r.Height * 0.84f);
                }
                break;
            case RepoState.Checking:
                using (var p = new Pen(pal.Accent, w)) g.DrawEllipse(p, Inflate(r, -w / 2));
                using (var b = new SolidBrush(pal.Accent)) g.FillEllipse(b, Inflate(r, -d * 0.3f));
                break;
            default: // pending
                using (var p = new Pen(pal.TextMuted, w) { DashStyle = DashStyle.Dot }) g.DrawEllipse(p, Inflate(r, -w / 2));
                break;
        }
        g.SmoothingMode = old;
    }

    private static RectangleF Inflate(RectangleF r, float by)
    {
        r.Inflate(by, by);
        return r;
    }

    public static Tone StateTone(RepoState s) => s switch
    {
        RepoState.Ok or RepoState.NoReleases => Tone.Ok,
        RepoState.NotReachable => Tone.Error,
        RepoState.RateLimited => Tone.Warn,
        RepoState.Checking => Tone.Accent,
        _ => Tone.Muted
    };

    /// <summary>Walks a control tree and lets every <see cref="IThemed"/> control re-apply its colors.</summary>
    public static void ApplyThemeTree(Control root)
    {
        if (root is IThemed t) t.ApplyTheme();
        foreach (Control c in root.Controls) ApplyThemeTree(c);
        root.Invalidate();
    }

    public static TextFormatFlags Left => TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
                                         TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
}

// =====================================================================
// Containers
// =====================================================================

/// <summary>Double-buffered panel whose background follows a theme role.</summary>
public class ThemedPanel : Panel, IThemed
{
    public enum Role { Bg, Surface, Console }
    private Role _role;

    [DefaultValue(Role.Bg)]
    public Role BackRole { get => _role; set { _role = value; ApplyTheme(); } }

    public ThemedPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        ApplyTheme();
    }

    public virtual void ApplyTheme()
    {
        BackColor = _role switch
        {
            Role.Surface => Theme.Current.Surface,
            Role.Console => Theme.Current.ConsoleBg,
            _ => Theme.Current.Bg
        };
    }
}

/// <summary>Rounded surface card with a 1px border and an optional caption row ("01 // TITLE ....... right text").</summary>
public sealed class CardPanel : ThemedPanel
{
    private string _number = "";
    private string _title = "";
    private string _rightText = "";

    public static int CaptionHeight => Ui.S(40);

    public CardPanel()
    {
        BackRole = Role.Surface;
        Padding = new Padding(Ui.S(16), Ui.S(16), Ui.S(16), Ui.S(16));
    }

    public string Number { get => _number; set { _number = value; Invalidate(); } }
    public string Title { get => _title; set { _title = value; UpdatePadding(); Invalidate(); } }
    public string RightText { get => _rightText; set { _rightText = value; Invalidate(); } }

    private void UpdatePadding()
    {
        int top = _title.Length > 0 ? CaptionHeight : Ui.S(16);
        if (Padding.Top != top) Padding = new Padding(Ui.S(16), top, Ui.S(16), Ui.S(16));
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var pal = Theme.Current;
        using (var b = new SolidBrush(Ui.ParentBack(this))) e.Graphics.FillRectangle(b, ClientRectangle);
        Ui.FillRound(e.Graphics, ClientRectangle, Ui.S(6), pal.Surface, pal.Line);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (_title.Length == 0) return;
        var pal = Theme.Current;
        var g = e.Graphics;
        int x = Ui.S(16);
        var row = new Rectangle(x, 0, Width - 2 * x, CaptionHeight);
        if (_number.Length > 0)
        {
            string num = _number + " //";
            TextRenderer.DrawText(g, num, Theme.Caption, row, pal.Accent, Ui.Left);
            int w = TextRenderer.MeasureText(g, num, Theme.Caption, row.Size, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
            row = new Rectangle(row.X + w + Ui.S(8), row.Y, row.Width - w - Ui.S(8), row.Height);
        }
        TextRenderer.DrawText(g, _title.ToUpperInvariant(), Theme.Caption, row, pal.TextSecondary, Ui.Left);
        if (_rightText.Length > 0)
            TextRenderer.DrawText(g, _rightText, Theme.MonoSmall, row, pal.TextMuted,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        // hairline under caption
        using var pen = new Pen(pal.Line);
        g.DrawLine(pen, Ui.S(1), CaptionHeight - 1, Width - Ui.S(2), CaptionHeight - 1);
    }
}

// =====================================================================
// Labels
// =====================================================================

/// <summary>Label whose color follows a theme role.</summary>
public sealed class ThemedLabel : Label, IThemed
{
    private Tone _tone = Tone.Neutral;
    private bool _secondary;

    public ThemedLabel()
    {
        AutoSize = true;
        BackColor = Color.Transparent;
        Font = Theme.Ui;
        UseMnemonic = false;
        ApplyTheme();
    }

    public Tone Tone { get => _tone; set { _tone = value; ApplyTheme(); } }
    /// <summary>Use the secondary text color (only for <see cref="Tone.Neutral"/>).</summary>
    public bool Secondary { get => _secondary; set { _secondary = value; ApplyTheme(); } }

    public void ApplyTheme()
    {
        ForeColor = _tone == Tone.Neutral
            ? (_secondary ? Theme.Current.TextSecondary : Theme.Current.Text)
            : Ui.ToneColor(_tone);
    }
}

/// <summary>Small caption in display font caps (settings section titles).</summary>
public sealed class CaptionLabel : Control, IThemed
{
    public CaptionLabel()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                 ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent;
        Height = Ui.S(22);
        TabStop = false;
    }

    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

    public void ApplyTheme() => Invalidate();

    protected override void OnPaint(PaintEventArgs e) =>
        TextRenderer.DrawText(e.Graphics, Text.ToUpperInvariant(), Theme.Caption, ClientRectangle, Theme.Current.TextSecondary,
            TextFormatFlags.Left | TextFormatFlags.Bottom | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
}

/// <summary>Inline feedback line: tone glyph + text (replaces message boxes for non-critical feedback).</summary>
public sealed class NoticeLine : Control, IThemed
{
    private Tone _tone = Tone.Muted;

    public NoticeLine()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                 ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent;
        Height = Ui.S(24);
        TabStop = false;
    }

    public Tone Tone { get => _tone; set { _tone = value; Invalidate(); } }
    /// <summary>Hide the control while it has no text (used in stacked layouts to avoid empty gaps).</summary>
    public bool AutoHide { get; set; }

    public void Show(string text, Tone tone)
    {
        Text = text;
        _tone = tone;
        AccessibleName = text;
        if (AutoHide) Visible = true;
        Invalidate();
    }

    public void Clear()
    {
        Text = "";
        if (AutoHide) Visible = false;
        Invalidate();
    }

    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

    public void ApplyTheme() => Invalidate();

    protected override void OnPaint(PaintEventArgs e)
    {
        if (string.IsNullOrEmpty(Text)) return;
        var g = e.Graphics;
        Color c = Ui.ToneColor(_tone);
        string prefix = _tone switch { Tone.Error => "✕", Tone.Warn => "!", Tone.Ok => "✓", _ => "›" };
        int gw = Ui.S(16);
        var gr = new Rectangle(0, 0, gw, Height);
        TextRenderer.DrawText(g, prefix, Theme.UiSemi, gr, c, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        var tr = new Rectangle(gw + Ui.S(4), 0, Width - gw - Ui.S(4), Height);
        Color tc = _tone is Tone.Muted or Tone.Neutral ? Theme.Current.TextSecondary : c;
        TextRenderer.DrawText(g, Text, Theme.UiSmall, tr, tc, Ui.Left);
    }
}

// =====================================================================
// Buttons
// =====================================================================

public enum ButtonKind { Primary, Secondary, Ghost }

/// <summary>Icons drawn as vectors (no font dependency).</summary>
public enum VectorIcon { None, ChevronUp, ChevronDown, Close }

/// <summary>Fully owner-drawn button (primary / outline / ghost) with optional icon glyph and shortcut hint.</summary>
public sealed class FlatButton : Button, IThemed
{
    private bool _hover, _down;
    private ButtonKind _kind = ButtonKind.Secondary;
    private string _icon = "";
    private string _hint = "";

    public FlatButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Font = Theme.UiSemi;
        Height = Ui.S(34);
        Cursor = Cursors.Hand;
        UseMnemonic = false;
    }

    public ButtonKind Kind { get => _kind; set { _kind = value; Invalidate(); } }
    /// <summary>Segoe MDL2/Fluent glyph (see Theme.Ico*); ignored if the icon font is missing.</summary>
    public string IconGlyph { get => _icon; set { _icon = value; Invalidate(); } }
    /// <summary>Muted text drawn after the label, e.g. a keyboard shortcut.</summary>
    public string Hint { get => _hint; set { _hint = value; Invalidate(); } }
    /// <summary>Square icon-only button (text is used as accessible name).</summary>
    public bool IconOnly { get; set; }
    /// <summary>Plain-text glyph for icon-only buttons when no icon font is installed.</summary>
    public string FallbackGlyph { get; set; } = "";
    private VectorIcon _vector;
    /// <summary>Vector icon for icon-only buttons (takes precedence over glyphs).</summary>
    public VectorIcon Vector { get => _vector; set { _vector = value; Invalidate(); } }

    public void ApplyTheme() => Invalidate();

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        AccessibleName = Text;
        Invalidate();
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        if (IconOnly) return new Size(Height, Height);
        int pad = Ui.S(14);
        int w = pad * 2 + TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding).Width;
        if (_icon.Length > 0 && Theme.HasIconFont) w += Ui.S(16) + Ui.S(8);
        if (_hint.Length > 0) w += Ui.S(10) + TextRenderer.MeasureText(_hint, Theme.MonoSmall, Size.Empty, TextFormatFlags.NoPadding).Width;
        return new Size(w, Height);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _down = true; Invalidate(); } base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var pal = Theme.Current;
        var g = e.Graphics;
        Color parent = Ui.ParentBack(this);
        using (var b = new SolidBrush(parent)) g.FillRectangle(b, ClientRectangle);

        Color fill, text, border;
        bool enabled = Enabled;
        switch (_kind)
        {
            case ButtonKind.Primary:
                fill = !enabled ? pal.Raised : (_down ? pal.AccentHover : (_hover ? pal.AccentHover : pal.Accent));
                text = enabled ? pal.OnAccent : pal.TextMuted;
                border = enabled ? fill : pal.Line;
                break;
            case ButtonKind.Ghost:
                fill = enabled && (_hover || _down) ? pal.Hover : parent;
                text = enabled ? pal.Text : pal.TextMuted;
                border = fill;
                break;
            default:
                fill = enabled && (_hover || _down) ? pal.Hover : pal.Raised;
                text = enabled ? pal.Text : pal.TextMuted;
                border = enabled && _hover ? pal.TextMuted : pal.LineStrong;
                break;
        }

        var rect = new Rectangle(0, 0, Width, Height);
        Ui.FillRound(g, rect, Ui.S(4), fill, border);

        bool showIcon = _icon.Length > 0 && Theme.HasIconFont;
        if (IconOnly && _vector != VectorIcon.None)
        {
            DrawVector(g, rect, text);
        }
        else if (IconOnly)
        {
            string glyph = showIcon ? _icon : (FallbackGlyph.Length > 0 ? FallbackGlyph : Text);
            TextRenderer.DrawText(g, glyph, showIcon ? Theme.Icon : Font, rect, text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
        else
        {
            int contentW = GetPreferredSize(Size.Empty).Width - Ui.S(28);
            int x = Math.Max(Ui.S(10), (Width - contentW) / 2);
            if (showIcon)
            {
                TextRenderer.DrawText(g, _icon, Theme.Icon, new Rectangle(x, 0, Ui.S(16), Height), text,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                x += Ui.S(16) + Ui.S(8);
            }
            int tw = TextRenderer.MeasureText(g, Text, Font, Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding).Width;
            TextRenderer.DrawText(g, Text, Font, new Rectangle(x, 0, Width - x - Ui.S(6), Height), text, Ui.Left | TextFormatFlags.NoPadding);
            if (_hint.Length > 0)
            {
                Color hc = _kind == ButtonKind.Primary && enabled ? Color.FromArgb(170, pal.OnAccent) : pal.TextMuted;
                TextRenderer.DrawText(g, _hint, Theme.MonoSmall, new Rectangle(x + tw + Ui.S(10), 0, Width, Height), hc, Ui.Left | TextFormatFlags.NoPadding);
            }
        }

        if (Focused && ShowFocusCues)
        {
            var fr = new Rectangle(Ui.S(2), Ui.S(2), Width - Ui.S(4), Height - Ui.S(4));
            using var p = new Pen(_kind == ButtonKind.Primary ? pal.OnAccent : pal.Accent, 1f) { DashStyle = DashStyle.Solid };
            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = Ui.RoundRect(new RectangleF(fr.X + 0.5f, fr.Y + 0.5f, fr.Width - 1, fr.Height - 1), Ui.S(3));
            g.DrawPath(p, path);
            g.SmoothingMode = old;
        }
    }

    private void DrawVector(Graphics g, Rectangle r, Color c)
    {
        var old = g.SmoothingMode;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f, a = Ui.Sf(4.5f);
        using var p = new Pen(c, Math.Max(1.5f, Ui.Sf(1.6f))) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
        switch (_vector)
        {
            case VectorIcon.ChevronUp:
                g.DrawLines(p, new[] { new PointF(cx - a, cy + a / 2), new PointF(cx, cy - a / 2), new PointF(cx + a, cy + a / 2) });
                break;
            case VectorIcon.ChevronDown:
                g.DrawLines(p, new[] { new PointF(cx - a, cy - a / 2), new PointF(cx, cy + a / 2), new PointF(cx + a, cy - a / 2) });
                break;
            case VectorIcon.Close:
                g.DrawLine(p, cx - a, cy - a, cx + a, cy + a);
                g.DrawLine(p, cx + a, cy - a, cx - a, cy + a);
                break;
        }
        g.SmoothingMode = old;
    }
}

// =====================================================================
// Inputs
// =====================================================================

/// <summary>Borderless TextBox that draws its own placeholder in the theme's muted color.</summary>
public sealed class PlainTextBox : TextBox
{
    private string _placeholder = "";
    public string Placeholder { get => _placeholder; set { _placeholder = value; Invalidate(); } }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        const int WM_PAINT = 0x000F;
        if (m.Msg == WM_PAINT && TextLength == 0 && _placeholder.Length > 0)
        {
            using var g = CreateGraphics();
            TextRenderer.DrawText(g, _placeholder, Font, new Rectangle(1, 0, Width - 2, Height), Theme.Current.TextMuted, BackColor,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        }
    }

    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
}

/// <summary>Themed single-line input: rounded border that turns accent on focus.</summary>
public sealed class InputBox : Control, IThemed
{
    public PlainTextBox Box { get; } = new() { BorderStyle = BorderStyle.None };
    private bool _mono;

    public InputBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Height = Ui.S(34);
        Box.Font = Theme.Ui;
        Controls.Add(Box);
        Box.GotFocus += (_, _) => Invalidate();
        Box.LostFocus += (_, _) => Invalidate();
        ApplyTheme();
    }

    public bool Mono
    {
        get => _mono;
        set { _mono = value; Box.Font = value ? Theme.Mono : Theme.Ui; LayoutBox(); }
    }

    [AllowNull]
    public override string Text { get => Box.Text; set => Box.Text = value ?? ""; }

    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Box.Focus(); }
    protected override void OnResize(EventArgs e) { base.OnResize(e); LayoutBox(); }

    private void LayoutBox()
    {
        int padX = Ui.S(10);
        int h = Box.PreferredHeight;
        Box.SetBounds(padX, Math.Max(1, (Height - h) / 2), Math.Max(10, Width - 2 * padX), h);
    }

    public void ApplyTheme()
    {
        var pal = Theme.Current;
        Color back = Box.ReadOnly ? pal.Surface : pal.Raised;
        BackColor = back;
        Box.BackColor = back;
        Box.ForeColor = pal.Text;
        Invalidate();
        Box.Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var pal = Theme.Current;
        using (var b = new SolidBrush(Ui.ParentBack(this))) e.Graphics.FillRectangle(b, ClientRectangle);
        bool focus = Box.Focused;
        Ui.FillRound(e.Graphics, ClientRectangle, Ui.S(4), Box.BackColor, focus ? pal.Accent : pal.LineStrong);
        if (focus)
        {
            // 2px focus indicator at the bottom edge
            using var b = new SolidBrush(pal.Accent);
            e.Graphics.FillRectangle(b, Ui.S(3), Height - Ui.S(2), Width - Ui.S(6), Ui.S(2) - 1);
        }
    }
}

/// <summary>Segmented single-choice control (replaces ComboBox, whose dropdown cannot be themed).</summary>
public sealed class SegmentedControl : Control, IThemed
{
    private string[] _items = Array.Empty<string>();
    private int _selected = -1;
    private int _hover = -1;

    public event EventHandler? SelectedIndexChanged;

    public SegmentedControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        Height = Ui.S(34);
        TabStop = true;
        Cursor = Cursors.Hand;
        AccessibleRole = AccessibleRole.PageTabList;
    }

    public string[] Items { get => _items; set { _items = value ?? Array.Empty<string>(); Invalidate(); } }

    public int SelectedIndex
    {
        get => _selected;
        set
        {
            if (value < -1 || value >= _items.Length || value == _selected) return;
            _selected = value;
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ApplyTheme() => Invalidate();

    private Rectangle SegRect(int i)
    {
        int n = Math.Max(1, _items.Length);
        int w = (Width - 2 * Ui.S(3)) / n;
        int x = Ui.S(3) + i * w;
        if (i == n - 1) w = Width - Ui.S(3) - x;
        return new Rectangle(x, Ui.S(3), w, Height - 2 * Ui.S(3));
    }

    private int HitTest(Point p)
    {
        for (int i = 0; i < _items.Length; i++) if (SegRect(i).Contains(p)) return i;
        return -1;
    }

    protected override void OnMouseMove(MouseEventArgs e) { int h = HitTest(e.Location); if (h != _hover) { _hover = h; Invalidate(); } base.OnMouseMove(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = -1; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { Focus(); int h = HitTest(e.Location); if (h >= 0) SelectedIndex = h; base.OnMouseDown(e); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Left or Keys.Right || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Left && _selected > 0) SelectedIndex = _selected - 1;
        else if (e.KeyCode == Keys.Right && _selected < _items.Length - 1) SelectedIndex = _selected + 1;
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var pal = Theme.Current;
        var g = e.Graphics;
        using (var b = new SolidBrush(Ui.ParentBack(this))) g.FillRectangle(b, ClientRectangle);
        Ui.FillRound(g, ClientRectangle, Ui.S(5), pal.Bg, pal.LineStrong);
        for (int i = 0; i < _items.Length; i++)
        {
            var r = SegRect(i);
            bool sel = i == _selected;
            if (sel) Ui.FillRound(g, r, Ui.S(3), pal.Selection, pal.Accent);
            else if (i == _hover) Ui.FillRound(g, r, Ui.S(3), pal.Hover, null);
            TextRenderer.DrawText(g, _items[i], sel ? Theme.UiSemi : Theme.Ui, r, sel ? pal.Text : pal.TextSecondary,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            if (sel && Focused && ShowFocusCues)
            {
                var fr = Rectangle.Inflate(r, -Ui.S(3), -Ui.S(3));
                using var p = new Pen(pal.Accent) { DashStyle = DashStyle.Dot };
                g.DrawRectangle(p, fr);
            }
        }
    }
}

/// <summary>Numeric stepper ( − value unit + ), replaces NumericUpDown whose spin buttons cannot be themed.</summary>
public sealed class Stepper : Control, IThemed
{
    private int _value = 1, _min = 1, _max = 100;
    private int _hover = -1; // 0 = minus, 1 = plus
    public string Unit { get; set; } = "";

    public event EventHandler? ValueChanged;

    public Stepper()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        Height = Ui.S(34);
        Width = Ui.S(150);
        TabStop = true;
        AccessibleRole = AccessibleRole.SpinButton;
    }

    public int Minimum { get => _min; set { _min = value; Value = _value; } }
    public int Maximum { get => _max; set { _max = value; Value = _value; } }

    public int Value
    {
        get => _value;
        set
        {
            int v = Math.Clamp(value, _min, _max);
            if (v == _value) return;
            _value = v;
            AccessibleName = $"{v} {Unit}";
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Sets the value without raising ValueChanged (initial load).</summary>
    public void SetSilently(int v) { _value = Math.Clamp(v, _min, _max); Invalidate(); }

    public void ApplyTheme() => Invalidate();

    private Rectangle MinusRect => new(0, 0, Height, Height);
    private Rectangle PlusRect => new(Width - Height, 0, Height, Height);

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int h = MinusRect.Contains(e.Location) ? 0 : PlusRect.Contains(e.Location) ? 1 : -1;
        if (h != _hover) { _hover = h; Cursor = h >= 0 ? Cursors.Hand : Cursors.Default; Invalidate(); }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e) { _hover = -1; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        if (MinusRect.Contains(e.Location)) Value--;
        else if (PlusRect.Contains(e.Location)) Value++;
        base.OnMouseDown(e);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        Value += e.Delta > 0 ? 1 : -1;
        base.OnMouseWheel(e);
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Up or Keys.Down or Keys.Left or Keys.Right || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Up or Keys.Right) Value++;
        else if (e.KeyCode is Keys.Down or Keys.Left) Value--;
        else if (e.KeyCode == Keys.PageUp) Value += 10;
        else if (e.KeyCode == Keys.PageDown) Value -= 10;
        base.OnKeyDown(e);
    }

    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var pal = Theme.Current;
        var g = e.Graphics;
        using (var b = new SolidBrush(Ui.ParentBack(this))) g.FillRectangle(b, ClientRectangle);
        Ui.FillRound(g, ClientRectangle, Ui.S(4), pal.Raised, Focused ? pal.Accent : pal.LineStrong);

        DrawSide(g, MinusRect, "−", _hover == 0, _value > _min);
        DrawSide(g, PlusRect, "+", _hover == 1, _value < _max);

        using (var p = new Pen(pal.Line))
        {
            g.DrawLine(p, MinusRect.Right, Ui.S(6), MinusRect.Right, Height - Ui.S(7));
            g.DrawLine(p, PlusRect.Left - 1, Ui.S(6), PlusRect.Left - 1, Height - Ui.S(7));
        }

        var mid = new Rectangle(MinusRect.Right, 0, PlusRect.Left - MinusRect.Right, Height);
        string txt = Unit.Length > 0 ? $"{_value} {Unit}" : _value.ToString();
        TextRenderer.DrawText(g, txt, Theme.Mono, mid, pal.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }

    private void DrawSide(Graphics g, Rectangle r, string sign, bool hover, bool enabled)
    {
        var pal = Theme.Current;
        if (hover && enabled) Ui.FillRound(g, Rectangle.Inflate(r, -Ui.S(3), -Ui.S(3)), Ui.S(3), pal.Hover, null);
        TextRenderer.DrawText(g, sign, Theme.UiSemi, r, enabled ? pal.Text : pal.TextMuted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }
}

// =====================================================================
// Header, tiles
// =====================================================================

/// <summary>Top bar: app title on the left, a 2px signal line at the bottom that doubles as progress bar.</summary>
public sealed class HeaderBar : ThemedPanel
{
    private double? _progress;
    public string TitleText { get; set; } = "";
    public string Version { get; set; } = "";

    public HeaderBar()
    {
        BackRole = Role.Surface;
        Height = Ui.S(64);
        Padding = new Padding(Ui.S(20), 0, Ui.S(16), Ui.S(2));
    }

    /// <summary>0..1 while a check runs, null when idle.</summary>
    public double? Progress { get => _progress; set { _progress = value; Invalidate(new Rectangle(0, Height - Ui.S(2), Width, Ui.S(2))); } }

    protected override void OnPaint(PaintEventArgs e)
    {
        var pal = Theme.Current;
        var g = e.Graphics;
        int lineH = Ui.S(2);
        using (var b = new SolidBrush(pal.Line)) g.FillRectangle(b, 0, Height - lineH, Width, lineH);
        if (_progress.HasValue)
        {
            int w = (int)Math.Round(Width * Math.Clamp(_progress.Value, 0, 1));
            using var b = new SolidBrush(pal.Accent);
            g.FillRectangle(b, 0, Height - lineH, Math.Max(Ui.S(6), w), lineH);
        }
        else
        {
            // idle: short accent segment on the left = "signal" mark
            using var b = new SolidBrush(pal.Accent);
            g.FillRectangle(b, 0, Height - lineH, Ui.S(56), lineH);
        }

        int x = Padding.Left;
        var area = new Rectangle(x, 0, Width / 2, Height - lineH);
        // logo mark: small square bracket glyph in accent
        int m = Ui.S(14);
        int my = (area.Height - m) / 2;
        using (var p = new Pen(pal.Accent, Math.Max(1.5f, Ui.Sf(2f))))
        {
            g.DrawLines(p, new[] { new Point(x + Ui.S(5), my), new Point(x, my), new Point(x, my + m), new Point(x + Ui.S(5), my + m) });
            g.DrawLines(p, new[] { new Point(x + m - Ui.S(5), my), new Point(x + m, my), new Point(x + m, my + m), new Point(x + m - Ui.S(5), my + m) });
        }
        using (var b = new SolidBrush(pal.Accent))
            g.FillRectangle(b, x + Ui.S(5), my + Ui.S(5), m - Ui.S(10) + 1, m - Ui.S(10) + 1);
        x += m + Ui.S(12);

        string title = TitleText.ToUpperInvariant();
        var tr = new Rectangle(x, area.Y, area.Width, area.Height);
        TextRenderer.DrawText(g, title, Theme.Title, tr, pal.Text, Ui.Left | TextFormatFlags.NoPadding);
        int tw = TextRenderer.MeasureText(g, title, Theme.Title, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
        if (Version.Length > 0)
            TextRenderer.DrawText(g, "v" + Version, Theme.MonoSmall, new Rectangle(x + tw + Ui.S(10), area.Y + Ui.S(1), Ui.S(80), area.Height),
                pal.TextMuted, Ui.Left | TextFormatFlags.NoPadding);
    }
}

/// <summary>Instrument tile: caps caption, big value, secondary line, optional state glyph and meter.</summary>
public sealed class StatTile : Control, IThemed
{
    private string _caption = "", _value = "", _sub = "";
    private Tone _tone = Tone.Neutral, _subTone = Tone.Muted;
    private double? _meter;
    private RepoState? _glyph;

    public StatTile()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        TabStop = false;
    }

    public void Set(string caption, string value, string sub, Tone tone = Tone.Neutral, Tone subTone = Tone.Muted, double? meter = null, RepoState? glyph = null)
    {
        if (_caption == caption && _value == value && _sub == sub && _tone == tone && _subTone == subTone && _meter == meter && _glyph == glyph) return;
        _caption = caption; _value = value; _sub = sub; _tone = tone; _subTone = subTone; _meter = meter; _glyph = glyph;
        AccessibleName = $"{caption}: {value}. {sub}";
        Invalidate();
    }

    public void ApplyTheme() => Invalidate();

    protected override void OnPaint(PaintEventArgs e)
    {
        var pal = Theme.Current;
        var g = e.Graphics;
        using (var b = new SolidBrush(Ui.ParentBack(this))) g.FillRectangle(b, ClientRectangle);
        Ui.FillRound(g, ClientRectangle, Ui.S(6), pal.Surface, pal.Line);

        // corner marks (top-left / bottom-right): the single decorative "instrument" element
        int cm = Ui.S(8);
        using (var p = new Pen(_tone == Tone.Neutral ? pal.LineStrong : Ui.ToneColor(_tone), Math.Max(1f, Ui.Sf(1.5f))))
        {
            int o = Ui.S(5);
            g.DrawLines(p, new[] { new Point(o, o + cm), new Point(o, o), new Point(o + cm, o) });
            g.DrawLines(p, new[] { new Point(Width - o - 1 - cm, Height - o - 1), new Point(Width - o - 1, Height - o - 1), new Point(Width - o - 1, Height - o - 1 - cm) });
        }

        int pad = Ui.S(16);
        int y = Ui.S(12);
        TextRenderer.DrawText(g, _caption.ToUpperInvariant(), Theme.Caption, new Rectangle(pad, y, Width - 2 * pad, Ui.S(18)), pal.TextSecondary, Ui.Left);
        y += Ui.S(20);

        int vx = pad;
        if (_glyph.HasValue)
        {
            int gs = Ui.S(12);
            Ui.DrawStateGlyph(g, _glyph.Value, new Rectangle(vx, y + (Ui.S(30) - gs) / 2, gs, gs));
            vx += gs + Ui.S(8);
        }
        Color vc = _tone == Tone.Neutral ? pal.Text : Ui.ToneColor(_tone);
        TextRenderer.DrawText(g, _value, Theme.Metric, new Rectangle(vx, y, Width - vx - pad, Ui.S(30)), vc, Ui.Left | TextFormatFlags.NoPadding);
        y += Ui.S(30);

        TextRenderer.DrawText(g, _sub, Theme.UiSmall, new Rectangle(pad, y, Width - 2 * pad, Ui.S(18)),
            _subTone == Tone.Muted ? pal.TextSecondary : Ui.ToneColor(_subTone), Ui.Left);

        if (_meter.HasValue)
        {
            int mh = Ui.S(3);
            var track = new Rectangle(pad, Height - Ui.S(12) - mh, Width - 2 * pad, mh);
            using (var b = new SolidBrush(pal.Line)) g.FillRectangle(b, track);
            int w = (int)Math.Round(track.Width * Math.Clamp(_meter.Value, 0, 1));
            Color mc = _meter.Value < 0.1 ? pal.Error : _meter.Value < 0.25 ? pal.Warn : pal.Accent;
            using (var b = new SolidBrush(mc)) g.FillRectangle(b, track.X, track.Y, w, mh);
        }
    }
}

/// <summary>Lays out tiles in 4 columns, or 2x2 when narrow.</summary>
public sealed class TilesPanel : ThemedPanel
{
    public static int TileHeight => Ui.S(104);
    private int Gap => Ui.S(12);

    public TilesPanel()
    {
        BackRole = Role.Bg;
    }

    private int Columns => Width >= Ui.S(720) ? 4 : 2;

    public int PreferredTilesHeight
    {
        get
        {
            int n = Controls.Count;
            int rows = (int)Math.Ceiling(n / (double)Columns);
            return rows * TileHeight + Math.Max(0, rows - 1) * Gap;
        }
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        int cols = Columns;
        int n = Controls.Count;
        if (n == 0) return;
        int w = (Width - (cols - 1) * Gap) / cols;
        for (int i = 0; i < n; i++)
        {
            int c = i % cols, r = i / cols;
            int x = c * (w + Gap);
            int ww = c == cols - 1 ? Width - x : w;
            Controls[i].SetBounds(x, r * (TileHeight + Gap), ww, TileHeight);
        }
        int h = PreferredTilesHeight;
        if (Height != h) Height = h;
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        PerformLayout();
    }
}

// =====================================================================
// Table
// =====================================================================

/// <summary>Fully themed DataGridView (ListView headers/scroll areas cannot be themed reliably).</summary>
public class ThemedGrid : DataGridView, IThemed
{
    public string EmptyText { get; set; } = "";
    /// <summary>Index of the column whose cells hold a <see cref="RepoState"/> in Tag and get a status glyph.</summary>
    public int StateColumn { get; set; } = -1;

    public ThemedGrid()
    {
        DoubleBuffered = true;
        BorderStyle = BorderStyle.None;
        CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        RowHeadersVisible = false;
        AllowUserToAddRows = false;
        AllowUserToDeleteRows = false;
        AllowUserToResizeRows = false;
        AllowUserToOrderColumns = false;
        ReadOnly = true;
        SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        MultiSelect = true;
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        ColumnHeadersHeight = Ui.S(36);
        RowTemplate.Height = Ui.S(36);
        EnableHeadersVisualStyles = false;
        StandardTab = true;
        ShowCellToolTips = true;
        ApplyTheme();
    }

    public void ApplyTheme()
    {
        var pal = Theme.Current;
        BackgroundColor = pal.Surface;
        GridColor = pal.Line;
        DefaultCellStyle.BackColor = pal.Surface;
        DefaultCellStyle.ForeColor = pal.Text;
        DefaultCellStyle.SelectionBackColor = pal.Selection;
        DefaultCellStyle.SelectionForeColor = pal.SelectionText;
        DefaultCellStyle.Font = Theme.Ui;
        DefaultCellStyle.Padding = new Padding(Ui.S(10), 0, Ui.S(6), 0);
        DefaultCellStyle.WrapMode = DataGridViewTriState.False;
        AlternatingRowsDefaultCellStyle.BackColor = pal.Surface;
        ColumnHeadersDefaultCellStyle.BackColor = pal.Surface;
        ColumnHeadersDefaultCellStyle.ForeColor = pal.TextSecondary;
        ColumnHeadersDefaultCellStyle.SelectionBackColor = pal.Surface;
        ColumnHeadersDefaultCellStyle.SelectionForeColor = pal.TextSecondary;
        ColumnHeadersDefaultCellStyle.Font = Theme.Caption;
        ColumnHeadersDefaultCellStyle.Padding = new Padding(Ui.S(10), 0, Ui.S(6), 0);
        ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
        foreach (Control c in Controls) Theme.ApplyScrollbars(c); // inner H/V scrollbars
        Theme.ApplyScrollbars(this);
        Invalidate();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyTheme();
    }

    protected override void OnCellPainting(DataGridViewCellPaintingEventArgs e)
    {
        var pal = Theme.Current;
        if (e.RowIndex == -1 && e.ColumnIndex >= 0 && e.Graphics != null)
        {
            // header: caps caption + bottom hairline, sort arrow drawn as text
            using (var hb = new SolidBrush(pal.Surface)) e.Graphics.FillRectangle(hb, e.CellBounds);
            string text = (e.FormattedValue?.ToString() ?? "").ToUpperInvariant();
            var col = Columns[e.ColumnIndex];
            if (col.HeaderCell.SortGlyphDirection != SortOrder.None)
                text += col.HeaderCell.SortGlyphDirection == SortOrder.Ascending ? "  ▲" : "  ▼";
            var r = new Rectangle(e.CellBounds.X + Ui.S(10), e.CellBounds.Y, e.CellBounds.Width - Ui.S(14), e.CellBounds.Height);
            TextRenderer.DrawText(e.Graphics, text, Theme.Caption, r, pal.TextSecondary, Ui.Left);
            using (var p = new Pen(pal.LineStrong)) e.Graphics.DrawLine(p, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1);
            e.Handled = true;
            return;
        }

        if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && e.ColumnIndex != StateColumn && e.Graphics != null)
        {
            bool selected = Rows[e.RowIndex].Selected;
            e.PaintBackground(e.CellBounds, selected);
            e.PaintContent(e.CellBounds);
            using (var p = new Pen(pal.Line)) e.Graphics.DrawLine(p, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1);
            e.Handled = true;
            return;
        }

        if (e.RowIndex >= 0 && e.ColumnIndex == StateColumn && e.Graphics != null)
        {
            bool sel = Rows[e.RowIndex].Selected;
            e.PaintBackground(e.CellBounds, sel);
            using (var p = new Pen(pal.Line)) e.Graphics.DrawLine(p, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1);
            var state = Rows[e.RowIndex].Cells[e.ColumnIndex].Tag is RepoState s ? s : RepoState.Pending;
            int gs = Ui.S(10);
            int gx = e.CellBounds.X + Ui.S(12);
            Ui.DrawStateGlyph(e.Graphics, state, new Rectangle(gx, e.CellBounds.Y + (e.CellBounds.Height - gs) / 2, gs, gs));
            var tr = new Rectangle(gx + gs + Ui.S(8), e.CellBounds.Y, e.CellBounds.Right - gx - gs - Ui.S(12), e.CellBounds.Height);
            Color tc = state switch
            {
                RepoState.NotReachable => pal.Error,
                RepoState.RateLimited => pal.Warn,
                _ => sel ? pal.SelectionText : pal.Text
            };
            TextRenderer.DrawText(e.Graphics, e.FormattedValue?.ToString() ?? "", Theme.Ui, tr, tc, Ui.Left);
            e.Handled = true;
            return;
        }
        base.OnCellPainting(e);
    }

    protected override void OnRowPostPaint(DataGridViewRowPostPaintEventArgs e)
    {
        base.OnRowPostPaint(e);
        if (Rows[e.RowIndex].Selected)
        {
            // accent bar on the left of the selected row (selection never hides the status glyph)
            using var b = new SolidBrush(Theme.Current.Accent);
            e.Graphics.FillRectangle(b, e.RowBounds.X, e.RowBounds.Y, Ui.S(3), e.RowBounds.Height - 1);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Rows.Count == 0 && EmptyText.Length > 0)
        {
            var r = new Rectangle(Ui.S(24), ColumnHeadersHeight, Width - Ui.S(48), Height - ColumnHeadersHeight);
            TextRenderer.DrawText(e.Graphics, EmptyText, Theme.Ui, r, Theme.Current.TextSecondary,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        }
    }

    protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); if (Rows.Count == 0) Invalidate(); }
}

// =====================================================================
// Menus and dialogs
// =====================================================================

/// <summary>Theme-aware renderer for tray / context menus.</summary>
public sealed class ThemedMenuRenderer : ToolStripProfessionalRenderer
{
    public ThemedMenuRenderer() : base(new ThemedColorTable()) { RoundedEdges = false; }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        var pal = Theme.Current;
        e.TextColor = !e.Item.Enabled ? pal.TextSecondary : pal.Text;
        e.TextFont = e.Item.Enabled ? Theme.Ui : Theme.UiSmall;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        var pal = Theme.Current;
        var r = new Rectangle(Point.Empty, e.Item.Size);
        using (var b = new SolidBrush(pal.Surface)) e.Graphics.FillRectangle(b, r);
        if (e.Item.Selected && e.Item.Enabled)
        {
            var hr = Rectangle.Inflate(r, -Ui.S(4), -1);
            Ui.FillRound(e.Graphics, hr, Ui.S(3), pal.Selection, null);
        }
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        var pal = Theme.Current;
        using (var b = new SolidBrush(pal.Surface)) e.Graphics.FillRectangle(b, new Rectangle(Point.Empty, e.Item.Size));
        int y = e.Item.Height / 2;
        using var p = new Pen(pal.Line);
        e.Graphics.DrawLine(p, Ui.S(8), y, e.Item.Width - Ui.S(8), y);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = Theme.Current.TextSecondary;
        base.OnRenderArrow(e);
    }

    private sealed class ThemedColorTable : ProfessionalColorTable
    {
        private static Palette P => Theme.Current;
        public override Color ToolStripDropDownBackground => P.Surface;
        public override Color ImageMarginGradientBegin => P.Surface;
        public override Color ImageMarginGradientMiddle => P.Surface;
        public override Color ImageMarginGradientEnd => P.Surface;
        public override Color MenuBorder => P.LineStrong;
        public override Color MenuItemBorder => P.Selection;
        public override Color MenuItemSelected => P.Selection;
        public override Color SeparatorDark => P.Line;
        public override Color SeparatorLight => P.Line;
    }
}

/// <summary>Small themed message / confirmation dialog (MessageBox cannot be themed).</summary>
public sealed class ThemedDialog : Form
{
    private ThemedDialog(string title, string message, Tone tone, bool yesNo, string okText, string cancelText)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Theme.Current.Surface;
        Font = Theme.Ui;
        AutoScaleMode = AutoScaleMode.None;

        int pad = Ui.S(24);
        int width = Ui.S(440);
        var glyphColor = Ui.ToneColor(tone);
        string glyph = tone switch { Tone.Error => "✕", Tone.Warn => "!", Tone.Ok => "✓", _ => "?" };

        var lblGlyph = new Label
        {
            Text = glyph, Font = Theme.Metric, ForeColor = glyphColor, BackColor = Color.Transparent,
            AutoSize = false, Size = new Size(Ui.S(28), Ui.S(32)), Location = new Point(pad, pad - Ui.S(4)), UseMnemonic = false,
            TextAlign = ContentAlignment.TopCenter
        };
        int textX = pad + Ui.S(40);
        var lblMsg = new Label
        {
            Text = message, ForeColor = Theme.Current.Text, BackColor = Color.Transparent, AutoSize = false, UseMnemonic = false,
            Location = new Point(textX, pad), MaximumSize = new Size(width - textX - pad, 0)
        };
        Size msgSize = TextRenderer.MeasureText(message, Theme.Ui, new Size(width - textX - pad, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        lblMsg.Size = new Size(width - textX - pad, Math.Max(Ui.S(24), msgSize.Height + Ui.S(4)));

        int btnY = lblMsg.Bottom + Ui.S(24);
        var ok = new FlatButton { Kind = ButtonKind.Primary, Text = okText, AutoSize = false, DialogResult = yesNo ? DialogResult.Yes : DialogResult.OK };
        ok.Size = ok.GetPreferredSize(Size.Empty);
        ok.Width = Math.Max(ok.Width, Ui.S(96));
        Controls.Add(lblGlyph);
        Controls.Add(lblMsg);
        Controls.Add(ok);

        int right = width - pad;
        if (yesNo)
        {
            var cancel = new FlatButton { Kind = ButtonKind.Secondary, Text = cancelText, AutoSize = false, DialogResult = DialogResult.No };
            cancel.Size = cancel.GetPreferredSize(Size.Empty);
            cancel.Width = Math.Max(cancel.Width, Ui.S(96));
            cancel.Location = new Point(right - cancel.Width, btnY);
            ok.Location = new Point(cancel.Left - Ui.S(8) - ok.Width, btnY);
            Controls.Add(cancel);
            CancelButton = cancel;
            AcceptButton = cancel; // safe default for destructive confirmations
            ActiveControl = cancel;
        }
        else
        {
            ok.Location = new Point(right - ok.Width, btnY);
            AcceptButton = ok;
            CancelButton = ok;
            ActiveControl = ok;
        }
        ClientSize = new Size(width, btnY + ok.Height + pad);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.ApplyTitleBar(this);
    }

    public static void Info(IWin32Window? owner, string title, string message, Tone tone = Tone.Accent)
    {
        using var d = new ThemedDialog(title, message, tone, false, Lang.Get("btn_ok"), "");
        if (owner != null) d.ShowDialog(owner); else d.ShowDialog();
    }

    public static bool Confirm(IWin32Window owner, string title, string message, string yesText, string noText)
    {
        using var d = new ThemedDialog(title, message, Tone.Warn, true, yesText, noText);
        return d.ShowDialog(owner) == DialogResult.Yes;
    }
}
