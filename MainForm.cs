using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;

namespace GitHubReleaseFetcher;

public sealed class MainForm : Form
{
    // ---------------- state
    private readonly AppSettings _settings;
    private readonly ReleaseChecker _checker = new();
    private readonly Dictionary<string, RepoStatus> _status = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 15000 };
    private readonly System.Windows.Forms.Timer _noticeTimer = new() { Interval = 10000 };
    private CancellationTokenSource _cts = new();
    private bool _checking;
    private bool _exitRequested;
    private bool _balloonShown;
    private bool _loadingUi;
    private FormWindowState _restoreState = FormWindowState.Normal;
    private readonly ToolTip _tips = new() { ShowAlways = true };
    private Icon? _trayIcon;
    private DateTime _nextCheckUtc;
    private DateTime? _rateLimitUntilUtc;
    private DateTime _lastLogCleanup = DateTime.MinValue;
    private int _progressDone, _progressTotal;
    private string _progressRepo = "";

    // ---------------- layout
    private readonly HeaderBar _header = new() { Dock = DockStyle.Top };
    private readonly ThemedPanel _body = new() { Dock = DockStyle.Fill };
    private readonly TilesPanel _tiles = new() { Dock = DockStyle.Top };
    private readonly StatTile _tileState = new();
    private readonly StatTile _tileNext = new();
    private readonly StatTile _tileReach = new();
    private readonly StatTile _tileApi = new();
    private readonly CardPanel _repoCard = new() { Dock = DockStyle.Fill, Number = "01" };
    private readonly CardPanel _activityCard = new() { Dock = DockStyle.Bottom, Number = "02" };
    private readonly ThemedPanel _settingsPanel = new() { BackRole = ThemedPanel.Role.Surface, Visible = false };

    // ---------------- header
    private readonly FlatButton _btnSettings = new() { Kind = ButtonKind.Ghost, IconGlyph = Theme.IcoSettings, AutoSize = true };
    private readonly FlatButton _btnCheckNow = new() { Kind = ButtonKind.Primary, IconGlyph = Theme.IcoSync, Hint = "F5", AutoSize = true };

    // ---------------- repositories
    private readonly InputBox _txtRepo = new() { Dock = DockStyle.Fill };
    private readonly FlatButton _btnAdd = new() { Kind = ButtonKind.Secondary, IconGlyph = Theme.IcoAdd, AutoSize = true, Dock = DockStyle.Right };
    private readonly NoticeLine _repoNotice = new() { Dock = DockStyle.Top, AutoHide = true, Visible = false };
    private readonly ThemedGrid _grid = new() { Dock = DockStyle.Fill, StateColumn = 1 };
    private readonly ContextMenuStrip _gridMenu = new();
    private readonly ToolStripMenuItem _miRemove = new();
    private readonly ToolStripMenuItem _miOpenReleases = new();
    private readonly ToolStripMenuItem _miOpenRepoFolder = new();
    private readonly ToolStripMenuItem _miCopyName = new();

    // ---------------- activity
    private readonly FlatButton _btnToggleLog = new() { Kind = ButtonKind.Ghost, IconOnly = true };
    private readonly FlatButton _btnOpenLog = new() { Kind = ButtonKind.Ghost, IconGlyph = Theme.IcoDocument, AutoSize = true };
    private readonly ThemedLabel _lblLastLine = new() { AutoSize = false, Dock = DockStyle.Fill, Secondary = true, Font = Theme.MonoSmall, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
    private readonly ThemedPanel _consoleHost = new() { Dock = DockStyle.Fill, BackRole = ThemedPanel.Role.Console };
    private readonly ConsoleBox _console = new() { Dock = DockStyle.Fill };

    // ---------------- settings panel
    private readonly ThemedLabel _lblSettingsTitle = new() { Font = Theme.Title };
    private readonly FlatButton _btnCloseSettings = new() { Kind = ButtonKind.Ghost, IconOnly = true, Vector = VectorIcon.Close };
    private readonly SettingsFlow _settingsFlow = new();
    private readonly CaptionLabel _capTheme = new();
    // items are set here (texts replaced in ApplyLanguage) so SelectedIndex can be restored before ApplyLanguage runs
    private readonly SegmentedControl _segTheme = new() { Items = new[] { "System", "Light", "Dark" } };
    private readonly CaptionLabel _capLanguage = new();
    private readonly SegmentedControl _segLanguage = new() { Items = new[] { "English", "Deutsch" } };
    private readonly CaptionLabel _capFolder = new();
    private readonly InputBox _txtFolder = new() { Mono = true };
    private readonly FlatButton _btnBrowse = new() { Kind = ButtonKind.Secondary, IconGlyph = Theme.IcoFolder, AutoSize = true };
    private readonly FlatButton _btnOpenFolder = new() { Kind = ButtonKind.Ghost, IconGlyph = Theme.IcoOpen, AutoSize = true };
    private readonly NoticeLine _folderNotice = new() { AutoHide = true, Visible = false };
    private readonly CaptionLabel _capInterval = new();
    private readonly Stepper _numInterval = new() { Minimum = AppSettings.MinIntervalHours, Maximum = AppSettings.MaxIntervalHours, Unit = "h" };
    private readonly ThemedLabel _lblEffective = new() { Secondary = true, Font = Theme.UiSmall };
    private readonly CaptionLabel _capToken = new();
    private readonly NoticeLine _tokenState = new();
    private readonly InputBox _txtToken = new() { Mono = true };
    private readonly FlatButton _btnSaveToken = new() { Kind = ButtonKind.Secondary, IconGlyph = Theme.IcoSave, AutoSize = true };
    private readonly FlatButton _btnRemoveToken = new() { Kind = ButtonKind.Ghost, IconGlyph = Theme.IcoDelete, AutoSize = true };
    private readonly NoticeLine _tokenResult = new() { AutoHide = true, Visible = false };
    private readonly ThemedLabel _lblTokenHint = new() { Secondary = true, Font = Theme.UiSmall };
    private readonly ThemedLabel _lblTrayHint = new() { Secondary = true, Font = Theme.UiSmall };

    // ---------------- tray
    private readonly NotifyIcon _tray = new();
    private readonly ContextMenuStrip _trayMenu = new();
    private readonly ToolStripMenuItem _miState = new() { Enabled = false, Tag = "info" };
    private readonly ToolStripMenuItem _miLast = new() { Enabled = false, Tag = "info" };
    private readonly ToolStripMenuItem _miNext = new() { Enabled = false, Tag = "info" };
    private readonly ToolStripMenuItem _miRepos = new() { Enabled = false, Tag = "info" };
    private readonly ToolStripMenuItem _miRate = new() { Enabled = false, Tag = "info" };
    private readonly ToolStripMenuItem _miOpen = new();
    private readonly ToolStripMenuItem _miCheck = new();
    private readonly ToolStripMenuItem _miExit = new();

    public MainForm()
    {
        _settings = AppSettings.Load();
        Lang.Current = _settings.Language;
        Theme.SetMode(Theme.ParseMode(_settings.Theme));

        Logger.Cleanup();
        _lastLogCleanup = DateTime.Now;
        Logger.LineWritten += OnLogLine;

        Icon appIcon = LoadAppIcon();
        Icon = appIcon;
        AutoScaleMode = AutoScaleMode.None; // all sizes are scaled explicitly via Ui.S()
        Font = Theme.Ui;
        BackColor = Theme.Current.Bg;       // before the handle exists: no white flash
        Size = new Size(Ui.S(1180), Ui.S(800));
        MinimumSize = new Size(Ui.S(900), Ui.S(620));
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;

        BuildUi();
        BuildTray(appIcon);

        _loadingUi = true;
        _txtFolder.Text = _settings.DownloadFolder;
        _numInterval.SetSilently(Math.Clamp(_settings.CheckIntervalHours, AppSettings.MinIntervalHours, AppSettings.MaxIntervalHours));
        _segLanguage.SelectedIndex = _settings.Language == "de" ? 1 : 0;
        _segTheme.SelectedIndex = Theme.Mode switch { ThemeMode.Light => 1, ThemeMode.Dark => 2, _ => 0 };
        _loadingUi = false;

        foreach (var r in _settings.Repositories) _status[r] = new RepoStatus { Repo = r };

        ApplyLanguage();
        SetLogExpanded(_settings.LogExpanded);
        ApplyThemeColors(); // controls were created as field initializers before the palette was resolved

        string ver = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
        _header.Version = ver;
        Logger.Info(Lang.F("log_start", ver));
        if (AppSettings.LoadError != null) Logger.Warn(Lang.F("log_settings_load_failed", AppSettings.LoadError));

        // first check shortly after start if overdue / never done
        DateTime due = (_settings.LastCheckUtc ?? DateTime.MinValue).AddHours(EffectiveIntervalHours());
        _nextCheckUtc = due <= DateTime.UtcNow.AddSeconds(10) ? DateTime.UtcNow.AddSeconds(10) : due;
        Logger.Info(Lang.F("log_next", _nextCheckUtc.ToLocalTime().ToString("g")));

        Theme.Changed += OnThemeChanged;
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        _timer.Tick += OnTimerTick;
        _timer.Start();
        _noticeTimer.Tick += (_, _) => { _noticeTimer.Stop(); _repoNotice.Clear(); };
        UpdateStatusDisplay();

        // first start: no folder yet -> show settings right away
        if (string.IsNullOrWhiteSpace(_settings.DownloadFolder))
        {
            SetSettingsVisible(true);
            _folderNotice.Show(Lang.Get("msg_no_folder"), Tone.Warn);
        }
    }

    // =====================================================================
    // UI construction
    // =====================================================================
    private static Icon LoadAppIcon()
    {
        try
        {
            using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("app.ico");
            if (s != null) return new Icon(s);
        }
        catch { /* ignore */ }
        return (Icon)SystemIcons.Application.Clone();
    }

    private void BuildUi()
    {
        // ---- header: title (painted) + actions on the right
        var headerActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Right, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false,
            BackColor = Color.Transparent, Padding = new Padding(0, Ui.S(13), 0, 0), Margin = Padding.Empty
        };
        _btnSettings.Margin = new Padding(0, 0, Ui.S(8), 0);
        _btnCheckNow.Margin = Padding.Empty;
        headerActions.Controls.Add(_btnSettings);
        headerActions.Controls.Add(_btnCheckNow);
        _header.Controls.Add(headerActions);

        // ---- tiles
        _tiles.Controls.Add(_tileState);
        _tiles.Controls.Add(_tileNext);
        _tiles.Controls.Add(_tileReach);
        _tiles.Controls.Add(_tileApi);

        // ---- repository card
        var inputRow = new ThemedPanel { Dock = DockStyle.Top, Height = Ui.S(34), BackRole = ThemedPanel.Role.Surface };
        var gap = new Panel { Dock = DockStyle.Right, Width = Ui.S(8), BackColor = Color.Transparent };
        inputRow.Controls.Add(_txtRepo);
        inputRow.Controls.Add(gap);
        inputRow.Controls.Add(_btnAdd);
        _repoNotice.Height = Ui.S(30);

        _grid.Columns.Add(MakeColumn("repo", 28, Ui.S(140), Theme.UiSemi));
        _grid.Columns.Add(MakeColumn("status", 20, Ui.S(170), Theme.Ui));
        _grid.Columns.Add(MakeColumn("release", 13, Ui.S(80), Theme.MonoSmall));
        _grid.Columns.Add(MakeColumn("pre", 14, Ui.S(110), Theme.MonoSmall));
        _grid.Columns.Add(MakeColumn("last", 9, Ui.S(90), Theme.MonoSmall));
        _grid.Columns.Add(MakeColumn("details", 28, Ui.S(60), Theme.Ui));
        ApplyColumnColors();
        _grid.ContextMenuStrip = _gridMenu;
        _gridMenu.Items.Add(_miOpenReleases);
        _gridMenu.Items.Add(_miOpenRepoFolder);
        _gridMenu.Items.Add(_miCopyName);
        _gridMenu.Items.Add(new ToolStripSeparator());
        _gridMenu.Items.Add(_miRemove);
        _gridMenu.Renderer = new ThemedMenuRenderer();
        _gridMenu.Font = Theme.Ui; // layout must be measured with the font the renderer draws with
        _gridMenu.ShowImageMargin = false;
        _gridMenu.Opening += (_, e) =>
        {
            int n = _grid.SelectedRows.Count;
            if (n == 0) { e.Cancel = true; return; }
            _miRemove.Enabled = true;
            _miOpenReleases.Enabled = _miOpenRepoFolder.Enabled = _miCopyName.Enabled = n == 1;
        };
        _miRemove.Click += (_, _) => RemoveSelected();
        _miOpenReleases.Click += (_, _) => { if (SelectedRepo() is string r) OpenUrl($"https://github.com/{r}/releases"); };
        _miOpenRepoFolder.Click += (_, _) => { if (SelectedRepo() is string r) OpenRepoFolder(r); };
        _miCopyName.Click += (_, _) => { if (SelectedRepo() is string r) { try { Clipboard.SetText(r); } catch { /* clipboard busy */ } } };
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0 && _grid.Rows[e.RowIndex].Tag is string r) OpenUrl($"https://github.com/{r}/releases"); };

        _repoCard.Controls.Add(_grid);
        _repoCard.Controls.Add(_repoNotice);
        _repoCard.Controls.Add(inputRow);

        // ---- activity card (collapsible)
        _consoleHost.Padding = new Padding(Ui.S(10), Ui.S(8), Ui.S(4), Ui.S(8));
        _consoleHost.Controls.Add(_console);
        _activityCard.Controls.Add(_consoleHost);
        _activityCard.Controls.Add(_lblLastLine);
        _activityCard.Controls.Add(_btnToggleLog);
        _activityCard.Controls.Add(_btnOpenLog);
        _btnToggleLog.Height = Ui.S(28);
        _btnOpenLog.Height = Ui.S(28);
        _activityCard.Resize += (_, _) => LayoutActivityButtons();
        _activityCard.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left && e.Y < CardPanel.CaptionHeight) SetLogExpanded(!_settings.LogExpanded, save: true);
        };
        _activityCard.MouseMove += (_, e) => _activityCard.Cursor = e.Y < CardPanel.CaptionHeight ? Cursors.Hand : Cursors.Default;
        _body.Resize += (_, _) =>
        {
            if (_settings.LogExpanded && _activityCard.Height != ExpandedLogHeight()) _activityCard.Height = ExpandedLogHeight();
        };

        // ---- body: tiles / repo card / activity card
        _body.Padding = new Padding(Ui.S(16), Ui.S(16), Ui.S(16), Ui.S(16));
        var gapTop = new Panel { Dock = DockStyle.Top, Height = Ui.S(12), BackColor = Color.Transparent };
        var gapBottom = new Panel { Dock = DockStyle.Bottom, Height = Ui.S(12), BackColor = Color.Transparent };
        // docking order: Fill first, last added = outermost
        _body.Controls.Add(_repoCard);
        _body.Controls.Add(gapBottom);
        _body.Controls.Add(_activityCard);
        _body.Controls.Add(gapTop);
        _body.Controls.Add(_tiles);

        BuildSettingsPanel();

        Controls.Add(_body);
        Controls.Add(_header);
        Controls.Add(_settingsPanel); // overlay, positioned in LayoutSettingsPanel
        _settingsPanel.BringToFront();

        // ---- events
        _btnSettings.Click += (_, _) => SetSettingsVisible(!_settingsPanel.Visible);
        _btnCloseSettings.Click += (_, _) => SetSettingsVisible(false);
        _btnBrowse.Click += (_, _) => BrowseFolder();
        _btnOpenFolder.Click += (_, _) => OpenFolder();
        _btnSaveToken.Click += async (_, _) => await SaveTokenAsync();
        _btnRemoveToken.Click += (_, _) => RemoveToken();
        _txtToken.Box.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await SaveTokenAsync(); }
        };
        _numInterval.ValueChanged += (_, _) => OnIntervalChanged();
        _segLanguage.SelectedIndexChanged += (_, _) => OnLanguageChanged();
        _segTheme.SelectedIndexChanged += (_, _) => OnThemeModeChanged();
        _btnAdd.Click += (_, _) => AddRepositories();
        _txtRepo.Box.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; AddRepositories(); }
        };
        _btnCheckNow.Click += async (_, _) => await ManualCheckAsync();
        _btnOpenLog.Click += (_, _) => OpenLog();
        _btnToggleLog.Click += (_, _) => SetLogExpanded(!_settings.LogExpanded, save: true);
        _grid.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Delete) { e.Handled = true; RemoveSelected(); }
        };
        _grid.CellMouseDown += (_, e) =>
        {
            // right click selects the row under the mouse (if not already part of the selection)
            if (e.Button == MouseButtons.Right && e.RowIndex >= 0 && !_grid.Rows[e.RowIndex].Selected)
            {
                _grid.ClearSelection();
                _grid.Rows[e.RowIndex].Selected = true;
            }
        };

        Resize += OnFormResize;
        Layout += (_, _) => LayoutSettingsPanel();
        FormClosing += OnFormClosing;
        FormClosed += (_, _) =>
        {
            // reached on Windows shutdown / logoff (exit via tray sets these earlier); stops a running check
            _exitRequested = true;
            try { _cts.Cancel(); } catch { /* ignore */ }
        };
        Shown += (_, _) =>
        {
            LoadLogTail();
            _grid.ClearSelection();
            _txtRepo.Focus();
        };
    }

    private static DataGridViewTextBoxColumn MakeColumn(string name, float weight, int minWidth, Font font)
    {
        var c = new DataGridViewTextBoxColumn
        {
            Name = name,
            FillWeight = weight,
            MinimumWidth = minWidth,
            SortMode = DataGridViewColumnSortMode.Automatic,
        };
        c.DefaultCellStyle.Font = font;
        return c;
    }

    private void BuildSettingsPanel()
    {
        _settingsPanel.Paint += (_, e) =>
        {
            using var p = new Pen(Theme.Current.LineStrong);
            e.Graphics.DrawLine(p, 0, 0, 0, _settingsPanel.Height);
        };
        _settingsPanel.Padding = new Padding(1, 0, 0, 0); // keep the edge line visible

        var head = new ThemedPanel { Dock = DockStyle.Top, Height = Ui.S(60), BackRole = ThemedPanel.Role.Surface, Padding = new Padding(Ui.S(24), Ui.S(14), Ui.S(14), 0) };
        _lblSettingsTitle.Dock = DockStyle.Left;
        _lblSettingsTitle.AutoSize = false;
        _lblSettingsTitle.Width = Ui.S(260);
        _lblSettingsTitle.TextAlign = ContentAlignment.MiddleLeft;
        _btnCloseSettings.Dock = DockStyle.Right;
        _btnCloseSettings.Width = Ui.S(34);
        head.Controls.Add(_lblSettingsTitle);
        head.Controls.Add(_btnCloseSettings);
        head.Paint += (_, e) =>
        {
            using var p = new Pen(Theme.Current.Line);
            e.Graphics.DrawLine(p, Ui.S(24), head.Height - 1, head.Width - Ui.S(14), head.Height - 1);
        };

        _settingsFlow.Dock = DockStyle.Fill;
        _settingsFlow.Padding = new Padding(Ui.S(24), Ui.S(4), Ui.S(20), Ui.S(24));

        var folderButtons = Row(_btnBrowse, _btnOpenFolder);
        var tokenButtons = Row(_btnSaveToken, _btnRemoveToken);
        _txtToken.Box.UseSystemPasswordChar = true;
        _txtFolder.Box.ReadOnly = true;
        _txtFolder.ApplyTheme();
        _lblEffective.AutoSize = true;
        _lblTokenHint.AutoSize = true;
        _lblTrayHint.AutoSize = true;

        _settingsFlow.AddSection(_capTheme, _segTheme);
        _settingsFlow.AddSection(_capLanguage, _segLanguage);
        _settingsFlow.AddSection(_capFolder, _txtFolder, folderButtons, _folderNotice);
        _settingsFlow.AddSection(_capInterval, _numInterval, _lblEffective);
        _settingsFlow.AddSection(_capToken, _tokenState, _txtToken, tokenButtons, _tokenResult, _lblTokenHint);
        _settingsFlow.AddFooter(_lblTrayHint);

        _settingsPanel.Controls.Add(_settingsFlow);
        _settingsPanel.Controls.Add(head);
    }

    private static FlowLayoutPanel Row(params Control[] controls)
    {
        var f = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, BackColor = Color.Transparent, Margin = Padding.Empty };
        for (int i = 0; i < controls.Length; i++)
        {
            controls[i].Margin = new Padding(0, 0, i < controls.Length - 1 ? Ui.S(8) : 0, 0);
            f.Controls.Add(controls[i]);
        }
        return f;
    }

    private void LayoutActivityButtons()
    {
        int h = _btnToggleLog.Height;
        int y = (CardPanel.CaptionHeight - h) / 2;
        int right = _activityCard.Width - Ui.S(10);
        _btnToggleLog.SetBounds(right - h, y, h, h);
        _btnOpenLog.Size = _btnOpenLog.GetPreferredSize(Size.Empty);
        _btnOpenLog.Location = new Point(_btnToggleLog.Left - Ui.S(6) - _btnOpenLog.Width, y);
        _btnToggleLog.BringToFront();
        _btnOpenLog.BringToFront();
    }

    private void SetLogExpanded(bool expanded, bool save = false)
    {
        if (save && _settings.LogExpanded != expanded)
        {
            _settings.LogExpanded = expanded;
            _settings.Save();
        }
        _consoleHost.Visible = expanded;
        _lblLastLine.Visible = !expanded;
        _activityCard.Height = expanded ? ExpandedLogHeight() : CardPanel.CaptionHeight + Ui.S(40);
        _activityCard.Padding = new Padding(Ui.S(16), CardPanel.CaptionHeight + Ui.S(4), Ui.S(16), Ui.S(expanded ? 12 : 8));
        _btnToggleLog.Vector = expanded ? VectorIcon.ChevronDown : VectorIcon.ChevronUp;
        _btnToggleLog.Text = Lang.Get(expanded ? "btn_hide_log" : "btn_show_log");
        _tips.SetToolTip(_btnToggleLog, _btnToggleLog.Text);
        _btnToggleLog.Invalidate();
        if (expanded) _console.ScrollToEnd();
        LayoutActivityButtons();
    }

    private void LayoutSettingsPanel()
    {
        int w = Math.Min(Ui.S(380), ClientSize.Width - Ui.S(200));
        _settingsPanel.SetBounds(ClientSize.Width - w, _header.Bottom, w, Math.Max(0, ClientSize.Height - _header.Bottom));
    }

    /// <summary>
    /// Expanded log takes about a third of the body, but never so much that the repository table
    /// shows fewer than about three rows (matters at the minimum window size).
    /// </summary>
    private int ExpandedLogHeight()
    {
        int h = _body.ClientSize.Height;
        int spaceLeft = h - _body.Padding.Vertical - _tiles.Height - Ui.S(24) - Ui.S(250); // 250 = repo card with ~3 rows
        int wanted = Math.Min((int)(h * 0.32), spaceLeft);
        return Math.Clamp(wanted, Ui.S(96), Ui.S(260));
    }

    private void SetSettingsVisible(bool visible)
    {
        LayoutSettingsPanel();
        _settingsPanel.Visible = visible;
        if (visible) _settingsPanel.BringToFront();
        _btnSettings.Kind = visible ? ButtonKind.Secondary : ButtonKind.Ghost;
        if (visible) _segTheme.Focus();
    }

    private void BuildTray(Icon icon)
    {
        _trayMenu.Items.Add(_miState);
        _trayMenu.Items.Add(_miLast);
        _trayMenu.Items.Add(_miNext);
        _trayMenu.Items.Add(_miRepos);
        _trayMenu.Items.Add(_miRate);
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add(_miOpen);
        _trayMenu.Items.Add(_miCheck);
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add(_miExit);
        _trayMenu.Renderer = new ThemedMenuRenderer();
        _trayMenu.Font = Theme.Ui;
        _trayMenu.ShowImageMargin = false;
        _trayMenu.Opening += (_, _) => UpdateTrayMenu();

        _miOpen.Click += (_, _) => RestoreFromTray();
        _miCheck.Click += async (_, _) => await ManualCheckAsync();
        _miExit.Click += (_, _) => ExitApplication();

        _trayIcon = new Icon(icon, SystemInformation.SmallIconSize);
        _tray.Icon = _trayIcon;
        _tray.Text = "GitHub Release Fetcher";
        _tray.ContextMenuStrip = _trayMenu;
        _tray.Visible = true;
        _tray.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) RestoreFromTray();
        };
    }

    private void ApplyLanguage()
    {
        Text = Lang.Get("app_title");
        _header.TitleText = Lang.Get("app_title");
        _header.Invalidate();
        _btnSettings.Text = Lang.Get("btn_settings");
        _btnCheckNow.Text = Lang.Get("btn_check_now");

        _repoCard.Title = Lang.Get("grp_repos");
        _activityCard.Title = Lang.Get("grp_activity");
        _txtRepo.Box.Placeholder = Lang.Get("repo_placeholder");
        _btnAdd.Text = Lang.Get("btn_add");
        _btnOpenLog.Text = Lang.Get("btn_open_log");
        _btnToggleLog.Text = Lang.Get(_settings.LogExpanded ? "btn_hide_log" : "btn_show_log");
        _miRemove.Text = Lang.Get("btn_remove");
        _miRemove.ShortcutKeyDisplayString = Lang.Get("key_del");
        _grid.EmptyText = Lang.Get("grid_empty");

        _grid.Columns["repo"]!.HeaderText = Lang.Get("col_repo");
        _grid.Columns["status"]!.HeaderText = Lang.Get("col_status");
        _grid.Columns["release"]!.HeaderText = Lang.Get("col_release");
        _grid.Columns["pre"]!.HeaderText = Lang.Get("col_prerelease");
        _grid.Columns["last"]!.HeaderText = Lang.Get("col_lastcheck");
        _grid.Columns["details"]!.HeaderText = Lang.Get("col_details");

        _lblSettingsTitle.Text = Lang.Get("grp_settings").ToUpperInvariant();
        _btnCloseSettings.Text = Lang.Get("btn_close");
        _capTheme.Text = Lang.Get("sec_theme");
        _segTheme.Items = new[] { Lang.Get("theme_system"), Lang.Get("theme_light"), Lang.Get("theme_dark") };
        _capLanguage.Text = Lang.Get("sec_language");
        _capFolder.Text = Lang.Get("sec_folder");
        _txtFolder.Box.Placeholder = Lang.Get("folder_placeholder");
        _btnBrowse.Text = Lang.Get("btn_browse");
        _btnOpenFolder.Text = Lang.Get("btn_open_folder");
        _capInterval.Text = Lang.Get("sec_interval");
        _segTheme.Caption = Lang.Get("sec_theme");
        _segLanguage.Caption = Lang.Get("sec_language");
        _numInterval.Caption = Lang.Get("sec_interval");
        _numInterval.SetSilently(_numInterval.Value);
        _tips.SetToolTip(_btnCheckNow, Lang.Get("tip_check"));
        _tips.SetToolTip(_btnSettings, Lang.Get("tip_settings"));
        _tips.SetToolTip(_btnCloseSettings, Lang.Get("tip_close"));
        _tips.SetToolTip(_btnOpenLog, Lang.Get("tip_open_log"));
        _tips.SetToolTip(_btnToggleLog, _btnToggleLog.Text);
        _miOpenReleases.Text = Lang.Get("mi_open_releases");
        _miOpenRepoFolder.Text = Lang.Get("mi_open_repo_folder");
        _miCopyName.Text = Lang.Get("mi_copy_name");
        _capToken.Text = Lang.Get("sec_token");
        _txtToken.Box.Placeholder = Lang.Get("token_placeholder");
        _btnSaveToken.Text = Lang.Get("btn_save_token");
        _btnRemoveToken.Text = Lang.Get("btn_remove_token");
        _lblTokenHint.Text = Lang.Get("token_hint");
        _lblTrayHint.Text = Lang.Get("tray_balloon");
        _tokenResult.Clear();
        if (!string.IsNullOrWhiteSpace(_settings.DownloadFolder)) _folderNotice.Clear();

        _miOpen.Text = Lang.Get("tray_open");
        _miCheck.Text = Lang.Get("tray_check");
        _miExit.Text = Lang.Get("tray_exit");

        UpdateTokenState();
        UpdateEffectiveInterval();
        RefreshRepoList();
        UpdateStatusDisplay();
        _settingsFlow.PerformLayout();
        LayoutActivityButtons();
    }

    // =====================================================================
    // Theme
    // =====================================================================
    private void OnThemeModeChanged()
    {
        if (_loadingUi) return;
        var mode = _segTheme.SelectedIndex switch { 1 => ThemeMode.Light, 2 => ThemeMode.Dark, _ => ThemeMode.System };
        _settings.Theme = Theme.ModeToString(mode);
        _settings.Save();
        Theme.SetMode(mode);
    }

    private void OnThemeChanged()
    {
        if (IsDisposed) return;
        Theme.WithoutRedraw(this, ApplyThemeColors);
        Theme.ApplyTitleBar(this);
        Theme.ApplyScrollbars(_console);
        _trayMenu.Invalidate();
        _gridMenu.Invalidate();
    }

    private void ApplyThemeColors()
    {
        BackColor = Theme.Current.Bg;
        Ui.ApplyThemeTree(this);
        ApplyColumnColors();
        _console.Rerender();
        UpdateTokenState();
        UpdateEffectiveInterval();
    }

    private void ApplyColumnColors()
    {
        _grid.Columns["details"]!.DefaultCellStyle.ForeColor = Theme.Current.TextSecondary;
        _grid.Columns["last"]!.DefaultCellStyle.ForeColor = Theme.Current.TextSecondary;
    }

    private void OnUserPreferenceChanged(object? sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
    {
        if (e.Category is not (Microsoft.Win32.UserPreferenceCategory.General or Microsoft.Win32.UserPreferenceCategory.Accessibility or
            Microsoft.Win32.UserPreferenceCategory.Color or Microsoft.Win32.UserPreferenceCategory.VisualStyle)) return;
        if (IsDisposed || !IsHandleCreated) return;
        try { BeginInvoke(new Action(Theme.Refresh)); } catch { /* closing */ }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.ApplyTitleBar(this);
        Theme.ApplyScrollbars(_console);
    }

    protected override void WndProc(ref Message m)
    {
        const int WM_SETTINGCHANGE = 0x001A;
        const int SPI_SETHIGHCONTRAST = 0x0043;
        if (m.Msg == WM_SETTINGCHANGE)
        {
            string? area = m.LParam != IntPtr.Zero ? Marshal.PtrToStringUni(m.LParam) : null;
            if (area == "ImmersiveColorSet" || (int)m.WParam == SPI_SETHIGHCONTRAST)
                BeginInvoke(new Action(Theme.Refresh));
        }
        base.WndProc(ref m);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F5) { _ = ManualCheckAsync(); return true; }
        if (keyData == Keys.Escape && _settingsPanel.Visible) { SetSettingsVisible(false); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    // =====================================================================
    // Settings handlers
    // =====================================================================
    private void BrowseFolder()
    {
        using var dlg = new FolderBrowserDialog { ShowNewFolderButton = true, UseDescriptionForTitle = true, Description = Lang.Get("sec_folder") };
        if (Directory.Exists(_settings.DownloadFolder)) dlg.SelectedPath = _settings.DownloadFolder;
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        _settings.DownloadFolder = dlg.SelectedPath;
        _settings.Save();
        _txtFolder.Text = dlg.SelectedPath;
        _folderNotice.Clear();
        Logger.Info(Lang.F("log_folder_set", dlg.SelectedPath));
    }

    private void OpenFolder()
    {
        if (!Directory.Exists(_settings.DownloadFolder))
        {
            _folderNotice.Show(Lang.Get("msg_no_folder"), Tone.Warn);
            return;
        }
        try { Process.Start(new ProcessStartInfo(_settings.DownloadFolder) { UseShellExecute = true }); }
        catch (Exception ex) { Logger.Error(ex.Message); }
    }

    private async Task SaveTokenAsync()
    {
        string token = _txtToken.Text.Trim();
        if (token.Length == 0) return;

        try
        {
            _settings.SetToken(token);
        }
        catch (Exception ex)
        {
            Logger.Error(ex.Message);
            _tokenResult.Show(ex.Message, Tone.Error);
            return;
        }
        _settings.Save();
        _txtToken.Text = "";
        UpdateTokenState();
        UpdateEffectiveInterval();

        _btnSaveToken.Enabled = false;
        _tokenResult.Show(Lang.Get("token_validating"), Tone.Muted);
        try
        {
            bool ok; int? limit; string error;
            try { (ok, limit, error) = await _checker.ValidateTokenAsync(token, _cts.Token); }
            catch (OperationCanceledException) { return; }
            string msg;
            if (ok)
            {
                msg = Lang.F("msg_token_check_ok", limit?.ToString() ?? Lang.Get("unknown"));
                Logger.Info(msg);
                _tokenResult.Show(msg, Tone.Ok);
            }
            else
            {
                msg = error.StartsWith("HTTP") ? Lang.F("msg_token_check_fail", error) : Lang.F("msg_token_check_unknown", error);
                Logger.Warn(msg);
                _tokenResult.Show(msg, Tone.Warn);
            }
        }
        finally
        {
            _btnSaveToken.Enabled = true;
            UpdateStatusDisplay();
        }
    }

    private void RemoveToken()
    {
        if (!_settings.HasToken) return;
        _settings.SetToken("");
        _settings.Save();
        UpdateTokenState();
        UpdateEffectiveInterval();
        Logger.Info(Lang.Get("msg_token_removed"));
        _tokenResult.Show(Lang.Get("msg_token_removed"), Tone.Muted);
        UpdateStatusDisplay();
    }

    private void UpdateTokenState()
    {
        bool has = _settings.HasToken;
        _tokenState.Show(Lang.Get(has ? "token_set" : "token_not_set"), has ? Tone.Ok : Tone.Warn);
        _btnRemoveToken.Enabled = has;
    }

    private void OnIntervalChanged()
    {
        if (_loadingUi) return;
        _settings.CheckIntervalHours = _numInterval.Value;
        _settings.Save();
        Logger.Info(Lang.F("log_interval_set", _settings.CheckIntervalHours));
        UpdateEffectiveInterval();
        RecalculateNextCheck();
    }

    private void OnLanguageChanged()
    {
        if (_loadingUi) return;
        _settings.Language = _segLanguage.SelectedIndex == 1 ? "de" : "en";
        Lang.Current = _settings.Language;
        _settings.Save();
        ApplyLanguage();
    }

    /// <summary>
    /// GitHub allows 60 API requests/h without token, 5000/h with token.
    /// One check costs 1 request per repository. Only ~80% of the budget is used
    /// (headroom for source downloads via API). If the selected interval would exceed that,
    /// the interval is raised automatically.
    /// </summary>
    private int EffectiveIntervalHours()
    {
        int selected = _settings.CheckIntervalHours;
        int repoCount = Math.Max(1, _settings.Repositories.Count);
        double budgetPerHour = _settings.HasToken ? 4000 : 48;
        int minHours = (int)Math.Ceiling(repoCount / budgetPerHour);
        return Math.Max(selected, Math.Max(1, minHours));
    }

    private void UpdateEffectiveInterval()
    {
        int eff = EffectiveIntervalHours();
        if (eff > _settings.CheckIntervalHours)
        {
            _lblEffective.Text = Lang.F("interval_adjusted", eff, _settings.Repositories.Count);
            _lblEffective.Tone = Tone.Warn;
        }
        else
        {
            _lblEffective.Text = Lang.F("interval_ok", eff);
            _lblEffective.Tone = Tone.Neutral;
        }
        _settingsFlow.PerformLayout();
    }

    private void RecalculateNextCheck()
    {
        if (_checking) return;
        DateTime baseTime = _settings.LastCheckUtc ?? DateTime.UtcNow;
        DateTime next = baseTime.AddHours(EffectiveIntervalHours());
        if (next < DateTime.UtcNow.AddSeconds(10)) next = DateTime.UtcNow.AddSeconds(10);
        if (_rateLimitUntilUtc.HasValue && next < _rateLimitUntilUtc.Value) next = _rateLimitUntilUtc.Value;
        _nextCheckUtc = next;
        Logger.Info(Lang.F("log_next", _nextCheckUtc.ToLocalTime().ToString("g")));
        UpdateStatusDisplay();
    }

    // =====================================================================
    // Repositories
    // =====================================================================
    private void ShowNotice(string text, Tone tone)
    {
        _repoNotice.Show(text, tone);
        _noticeTimer.Stop();
        _noticeTimer.Start();
    }

    private void AddRepositories()
    {
        var entries = RepoParser.SplitInput(_txtRepo.Text).ToList();
        if (entries.Count == 0) return;

        var problems = new List<string>();
        int added = 0;
        foreach (var e in entries)
        {
            if (!RepoParser.TryParse(e, out string repo))
            {
                problems.Add(Lang.F("msg_invalid_repo", e));
                Logger.Warn(Lang.F("msg_invalid_repo", e));
                continue;
            }
            if (_settings.Repositories.Contains(repo, StringComparer.OrdinalIgnoreCase))
            {
                problems.Add(Lang.F("msg_duplicate_repo", repo));
                continue;
            }
            _settings.Repositories.Add(repo);
            _status[repo] = new RepoStatus { Repo = repo };
            Logger.Info(Lang.F("log_repo_added", repo));
            added++;
        }

        if (added > 0)
        {
            _settings.Save();
            RefreshRepoList();
            UpdateEffectiveInterval();
            UpdateStatusDisplay();
        }

        _txtRepo.Text = problems.Count > 0 && added == 0 ? _txtRepo.Text : "";
        if (problems.Count > 0)
        {
            string text = added > 0 ? Lang.F("msg_added", added) + "  ·  " + string.Join("  ·  ", problems) : string.Join("  ·  ", problems);
            ShowNotice(text, Tone.Warn);
        }
        else if (added > 0) ShowNotice(Lang.F("msg_added", added), Tone.Ok);
    }

    private void RemoveSelected()
    {
        var names = _grid.SelectedRows.Cast<DataGridViewRow>().Select(r => r.Cells["repo"].Value?.ToString() ?? "").Where(n => n.Length > 0).ToList();
        if (names.Count == 0) return;
        string question = names.Count == 1 ? Lang.F("confirm_remove_one", names[0]) : Lang.F("confirm_remove", names.Count);
        if (!ThemedDialog.Confirm(this, Lang.Get("app_title"), question, Lang.Get("btn_remove_confirm"), Lang.Get("btn_cancel")))
            return;

        foreach (var r in names)
        {
            _settings.Repositories.RemoveAll(x => string.Equals(x, r, StringComparison.OrdinalIgnoreCase));
            _status.Remove(r);
            Logger.Info(Lang.F("log_repo_removed", r));
        }
        _settings.Save();
        RefreshRepoList();
        UpdateEffectiveInterval();
        UpdateStatusDisplay();
        ShowNotice(Lang.F("msg_removed", names.Count), Tone.Muted);
    }

    public static string StateText(RepoStatus st) => st.State == RepoState.NotReachable
        ? st.Reason switch
        {
            FailReason.NotFound => Lang.Get("st_notfound"),
            FailReason.Unauthorized => Lang.Get("st_unauthorized"),
            FailReason.Forbidden => Lang.Get("st_forbidden"),
            FailReason.Network => Lang.Get("st_network"),
            _ => Lang.Get("st_notreachable")
        }
        : StateText(st.State);

    /// <summary>Short instruction for the details column; the technical message stays in the tooltip and the log.</summary>
    private static string DetailsText(RepoStatus st) => st.State != RepoState.NotReachable ? st.Details : st.Reason switch
    {
        FailReason.NotFound => Lang.Get("hint_notfound"),
        FailReason.Unauthorized => Lang.Get("hint_unauthorized"),
        FailReason.Forbidden => Lang.Get("hint_forbidden"),
        FailReason.Network => Lang.Get("hint_network"),
        _ => st.Details
    };

    public static string StateText(RepoState s) => s switch
    {
        RepoState.Pending => Lang.Get("st_pending"),
        RepoState.Checking => Lang.Get("st_checking"),
        RepoState.Ok => Lang.Get("st_ok"),
        RepoState.NoReleases => Lang.Get("st_noreleases"),
        RepoState.NotReachable => Lang.Get("st_notreachable"),
        RepoState.RateLimited => Lang.Get("st_ratelimited"),
        _ => s.ToString()
    };

    private static void FillRow(DataGridViewRow row, RepoStatus st)
    {
        row.Cells[0].Value = st.Repo;
        row.Cells[1].Value = StateText(st);
        row.Cells[1].Tag = st.State;
        row.Cells[1].ToolTipText = st.Details;
        row.Cells[2].Value = st.StableTag.Length > 0 ? st.StableTag : "–";
        row.Cells[3].Value = st.PreTag.Length > 0 ? st.PreTag : "–";
        row.Cells[4].Value = st.LastCheck.HasValue ? When(st.LastCheck) : "–";
        row.Cells[4].ToolTipText = st.LastCheck.HasValue ? st.LastCheck.Value.ToString("g") : Lang.Get("never");
        row.Cells[5].Value = DetailsText(st);
        row.Cells[5].ToolTipText = st.Details;
        row.Tag = st.Repo;
    }

    private void RefreshRepoList()
    {
        var selected = new HashSet<string>(_grid.SelectedRows.Cast<DataGridViewRow>().Select(r => r.Tag as string ?? ""), StringComparer.OrdinalIgnoreCase);
        var sortCol = _grid.SortedColumn;
        var sortDir = _grid.SortOrder;

        _grid.SuspendLayout();
        try
        {
            _grid.Rows.Clear();
            foreach (var repo in _settings.Repositories)
            {
                if (!_status.TryGetValue(repo, out var st))
                {
                    st = new RepoStatus { Repo = repo };
                    _status[repo] = st;
                }
                int i = _grid.Rows.Add();
                FillRow(_grid.Rows[i], st);
            }
            if (sortCol != null && sortDir != SortOrder.None)
                _grid.Sort(sortCol, sortDir == SortOrder.Ascending ? System.ComponentModel.ListSortDirection.Ascending : System.ComponentModel.ListSortDirection.Descending);
            _grid.ClearSelection();
            foreach (DataGridViewRow r in _grid.Rows)
                if (r.Tag is string name && selected.Contains(name)) r.Selected = true;
        }
        finally
        {
            _grid.ResumeLayout();
        }
        _repoCard.RightText = Lang.F("list_count", _settings.Repositories.Count);
        _grid.Invalidate();
    }

    private void UpdateRepoRow(RepoStatus st)
    {
        var row = _grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => string.Equals(r.Tag as string, st.Repo, StringComparison.OrdinalIgnoreCase));
        if (row == null) { RefreshRepoList(); return; }
        FillRow(row, st);
    }

    // =====================================================================
    // Checking
    // =====================================================================
    private async void OnTimerTick(object? sender, EventArgs e)
    {
        try
        {
            if (DateTime.Now - _lastLogCleanup > TimeSpan.FromHours(24))
            {
                _lastLogCleanup = DateTime.Now;
                Logger.Cleanup();
            }

            UpdateStatusDisplay();
            if (_checking || DateTime.UtcNow < _nextCheckUtc) return;
            await RunCheckAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex.ToString());
        }
    }

    private async Task ManualCheckAsync()
    {
        if (_checking)
        {
            if (Visible) ShowNotice(Lang.Get("msg_already_checking"), Tone.Muted);
            return;
        }
        if (_rateLimitUntilUtc.HasValue && _rateLimitUntilUtc.Value > DateTime.UtcNow)
        {
            string msg = Lang.F("msg_rate_wait", _rateLimitUntilUtc.Value.ToLocalTime().ToString("g"));
            Logger.Warn(msg);
            if (Visible) ShowNotice(msg, Tone.Warn);
            return;
        }
        try { await RunCheckAsync(); }
        catch (Exception ex) { Logger.Error(ex.ToString()); }
    }

    private bool EnsureDownloadFolder()
    {
        string folder = _settings.DownloadFolder;
        if (string.IsNullOrWhiteSpace(folder)) return false;
        try
        {
            Directory.CreateDirectory(folder);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(Lang.F("log_folder_error", folder, ex.Message));
            return false;
        }
    }

    private async Task RunCheckAsync()
    {
        if (_checking) return;

        if (!EnsureDownloadFolder())
        {
            Logger.Warn(Lang.Get("log_round_skipped_nofolder"));
            _nextCheckUtc = DateTime.UtcNow.AddHours(EffectiveIntervalHours());
            Logger.Info(Lang.F("log_next", _nextCheckUtc.ToLocalTime().ToString("g")));
            UpdateStatusDisplay();
            if (Visible)
            {
                SetSettingsVisible(true);
                _folderNotice.Show(Lang.Get("msg_no_folder"), Tone.Warn);
            }
            return;
        }

        _checking = true;
        _btnCheckNow.Enabled = false;
        _miCheck.Enabled = false;

        var repos = _settings.Repositories.ToList();
        string folder = _settings.DownloadFolder;
        string token = _settings.GetToken();
        int ok = 0, fail = 0, postponed = 0, newFiles = 0;
        _rateLimitUntilUtc = null;
        _progressDone = 0;
        _progressTotal = repos.Count;
        _progressRepo = "";
        UpdateStatusDisplay();

        Logger.Info(Lang.F("log_round_start", repos.Count, token.Length > 0 ? "yes/ja" : "no/nein"));

        try
        {
            for (int i = 0; i < repos.Count; i++)
            {
                string repo = repos[i];
                if (!_status.TryGetValue(repo, out var st)) { st = new RepoStatus { Repo = repo }; _status[repo] = st; }

                if (_rateLimitUntilUtc.HasValue)
                {
                    st.State = RepoState.RateLimited;
                    st.Details = Lang.F("msg_rate_wait", _rateLimitUntilUtc.Value.ToLocalTime().ToString("g"));
                    UpdateRepoRow(st);
                    postponed++;
                    _progressDone = i + 1;
                    continue;
                }

                st.State = RepoState.Checking;
                _progressRepo = repo;
                UpdateRepoRow(st);
                UpdateStatusDisplay();

                RepoCheckResult r = await _checker.CheckRepoAsync(repo, folder, token, _cts.Token);

                // repo may have been removed from the list meanwhile
                st.State = r.State;
                st.Reason = r.Reason;
                st.Details = r.Details;
                if (r.State != RepoState.RateLimited) st.LastCheck = DateTime.Now;
                if (r.State == RepoState.Ok || r.State == RepoState.NoReleases)
                {
                    st.StableTag = r.StableTag;
                    st.PreTag = r.PreTag;
                    ok++;
                }
                else if (r.State == RepoState.NotReachable) fail++;
                else if (r.State == RepoState.RateLimited)
                {
                    postponed++;
                    _rateLimitUntilUtc = r.RateLimitedUntilUtc ?? DateTime.UtcNow.AddHours(1);
                }
                newFiles += r.NewFiles;
                _progressDone = i + 1;

                if (_status.ContainsKey(repo)) UpdateRepoRow(st);
                UpdateStatusDisplay();

                // small pause between repositories (avoids GitHub secondary rate limits)
                if (i < repos.Count - 1 && !_rateLimitUntilUtc.HasValue)
                    await Task.Delay(1500, _cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            return; // program is exiting
        }
        catch (Exception ex)
        {
            // unexpected error: log it, but still schedule the next check normally (no retry loop)
            Logger.Error(ex.ToString());
        }
        finally
        {
            _checking = false;
            _progressRepo = "";
            if (!_exitRequested)
            {
                _btnCheckNow.Enabled = true;
                _miCheck.Enabled = true;
            }
        }

        Logger.Info(Lang.F("log_round_end", ok, fail, postponed, newFiles));

        _settings.LastCheckUtc = DateTime.UtcNow;
        _settings.Save();

        DateTime next = DateTime.UtcNow.AddHours(EffectiveIntervalHours());
        // if repositories were postponed because of the API limit, continue right after the reset
        if (_rateLimitUntilUtc.HasValue && postponed > 0 && _rateLimitUntilUtc.Value < next)
            next = _rateLimitUntilUtc.Value.AddMinutes(1);
        _nextCheckUtc = next;
        Logger.Info(Lang.F("log_next", _nextCheckUtc.ToLocalTime().ToString("g")));

        UpdateStatusDisplay();

        if (newFiles > 0 && !Visible)
            _tray.ShowBalloonTip(5000, Lang.Get("app_title"), Lang.F("details_files", newFiles), ToolTipIcon.Info);
    }

    // =====================================================================
    // Status display
    // =====================================================================
    private (int reachable, int notReachable, int total) Counts()
    {
        int reach = 0, notReach = 0;
        foreach (var r in _settings.Repositories)
        {
            if (!_status.TryGetValue(r, out var st)) continue;
            if (st.State is RepoState.Ok or RepoState.NoReleases) reach++;
            else if (st.State == RepoState.NotReachable) notReach++;
        }
        return (reach, notReach, _settings.Repositories.Count);
    }

    private static CultureInfo Culture => CultureInfo.CurrentCulture;
    /// <summary>Compact local time: only the time for today, otherwise short date + time.</summary>
    public static string When(DateTime? local) =>
        local == null ? Lang.Get("never") : local.Value.ToString(local.Value.Date == DateTime.Today ? "t" : "g");
    private string StateNow() => _checking ? Lang.Get("status_checking") : Lang.Get("status_idle");
    private string LastText() => When(_settings.LastCheckUtc?.ToLocalTime());
    private string NextText() => _nextCheckUtc == default ? Lang.Get("unknown") : _nextCheckUtc.ToLocalTime().ToString("g");
    private string RateText()
    {
        if (!_checker.RateRemaining.HasValue) return Lang.Get("unknown");
        string s = $"{_checker.RateRemaining}/{_checker.RateLimit?.ToString() ?? "?"}";
        if (_checker.RateResetUtc.HasValue) s += $" (reset {_checker.RateResetUtc.Value.ToLocalTime():t})";
        return s;
    }

    private bool RatePaused => _rateLimitUntilUtc.HasValue && _rateLimitUntilUtc.Value > DateTime.UtcNow;

    /// <summary>Updates the instrument tiles, header progress line, check button and tray tooltip.</summary>
    private void UpdateStatusDisplay()
    {
        var (reach, notReach, total) = Counts();
        var ci = Culture;

        // --- state tile
        if (_checking)
        {
            string sub = _progressRepo.Length > 0 ? _progressRepo : Lang.Get("status_checking");
            _tileState.Set(Lang.Get("tile_state"), Lang.F("tile_progress", Math.Min(_progressDone + 1, Math.Max(1, _progressTotal)), _progressTotal),
                sub, Tone.Neutral, Tone.Muted, null, RepoState.Checking);
        }
        else if (RatePaused)
        {
            _tileState.Set(Lang.Get("tile_state"), Lang.Get("status_paused"),
                Lang.F("tile_until", _rateLimitUntilUtc!.Value.ToLocalTime().ToString("t")), Tone.Warn, Tone.Warn, null, RepoState.RateLimited);
        }
        else
        {
            string sub = _settings.HasToken ? Lang.Get("tile_token_on") : Lang.Get("tile_token_off");
            _tileState.Set(Lang.Get("tile_state"), Lang.Get("status_idle"), sub, Tone.Neutral, _settings.HasToken ? Tone.Muted : Tone.Warn, null, RepoState.Ok);
        }

        // --- next check tile (mission-control countdown)
        if (_checking)
        {
            _tileNext.Set(Lang.Get("tile_next"), Lang.Get("tile_running"), Lang.F("tile_last", LastText()));
        }
        else if (_nextCheckUtc == default)
        {
            _tileNext.Set(Lang.Get("tile_next"), "–", Lang.Get("unknown"));
        }
        else
        {
            TimeSpan left = _nextCheckUtc - DateTime.UtcNow;
            if (left < TimeSpan.Zero) left = TimeSpan.Zero;
            string countdown = left.TotalHours >= 100
                ? $"T–{(int)left.TotalDays}d {left.Hours:00}h"
                : $"T–{(int)left.TotalHours:00}:{left.Minutes:00}";
            string sub = Lang.F("tile_next_sub", _nextCheckUtc.ToLocalTime().ToString("t"), LastText());
            _tileNext.Set(Lang.Get("tile_next"), countdown, sub);
        }

        // --- reachable tile
        string reachSub;
        Tone reachTone;
        if (total == 0) { reachSub = Lang.Get("tile_no_repos"); reachTone = Tone.Muted; }
        else if (notReach > 0) { reachSub = FailSummary(notReach); reachTone = Tone.Error; }
        else if (reach == total) { reachSub = Lang.Get("tile_allreach"); reachTone = Tone.Ok; }
        else { reachSub = Lang.Get("tile_not_checked"); reachTone = Tone.Muted; }
        _tileReach.Set(Lang.Get("tile_reach"), $"{reach}/{total}", reachSub, Tone.Neutral, reachTone);

        // --- API tile
        if (_checker.RateRemaining.HasValue && _checker.RateLimit is int lim && lim > 0)
        {
            int rem = _checker.RateRemaining.Value;
            string reset = _checker.RateResetUtc.HasValue ? Lang.F("tile_reset", _checker.RateResetUtc.Value.ToLocalTime().ToString("t")) : "";
            double ratio = rem / (double)lim;
            Tone t = ratio < 0.1 ? Tone.Error : ratio < 0.25 ? Tone.Warn : Tone.Neutral;
            _tileApi.Set(Lang.Get("tile_api"), rem.ToString("N0", ci), Lang.F("tile_api_sub", lim.ToString("N0", ci), reset).Trim(), t, Tone.Muted, ratio);
        }
        else
        {
            _tileApi.Set(Lang.Get("tile_api"), "–", Lang.Get(_settings.HasToken ? "tile_api_unknown_token" : "tile_api_unknown"), Tone.Neutral, Tone.Muted, null);
        }

        _header.Progress = _checking && _progressTotal > 0 ? _progressDone / (double)_progressTotal : null;
        _btnCheckNow.Text = Lang.Get(_checking ? "btn_checking" : "btn_check_now");

        string tip = $"GitHub Release Fetcher - {StateNow()} ({reach}/{total})";
        _tray.Text = tip.Length > 63 ? tip.Substring(0, 63) : tip;
    }

    /// <summary>"2 not found" if all failures share one cause, otherwise "3 not reachable".</summary>
    private string FailSummary(int notReach)
    {
        var reasons = _settings.Repositories
            .Select(r => _status.TryGetValue(r, out var st) ? st : null)
            .Where(st => st != null && st.State == RepoState.NotReachable)
            .Select(st => st!.Reason).Distinct().ToList();
        if (reasons.Count != 1) return Lang.F("tile_notreach", notReach);
        return reasons[0] switch
        {
            FailReason.NotFound => Lang.F("tile_fail_notfound", notReach),
            FailReason.Unauthorized => Lang.F("tile_fail_unauthorized", notReach),
            FailReason.Forbidden => Lang.F("tile_fail_forbidden", notReach),
            FailReason.Network => Lang.F("tile_fail_network", notReach),
            _ => Lang.F("tile_notreach", notReach)
        };
    }

    private string? SelectedRepo() =>
        _grid.SelectedRows.Count == 1 ? _grid.SelectedRows[0].Tag as string : null;

    private void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { Logger.Error(ex.Message); }
    }

    /// <summary>Opens the repository's own download folder (same naming as ReleaseChecker), or the root folder if it does not exist yet.</summary>
    private void OpenRepoFolder(string repo)
    {
        string dir = Path.Combine(_settings.DownloadFolder, repo.Replace('/', '_'));
        if (Directory.Exists(dir))
        {
            try { Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true }); }
            catch (Exception ex) { Logger.Error(ex.Message); }
        }
        else OpenFolder();
    }

    private void UpdateTrayMenu()
    {
        var (reach, notReach, total) = Counts();
        _miState.Text = Lang.F("tray_state", StateNow());
        _miLast.Text = Lang.F("tray_last", LastText());
        _miNext.Text = Lang.F("tray_next", NextText());
        _miRepos.Text = Lang.F("tray_repos", reach, total, notReach);
        _miRate.Text = Lang.F("tray_rate", RateText());
        _miCheck.Enabled = !_checking;
    }

    private void OnLogLine(string line)
    {
        if (_exitRequested || IsDisposed || !IsHandleCreated) return;
        try
        {
            BeginInvoke(new Action(() => AppendDetail(line)));
        }
        catch { /* form closing */ }
    }

    private void AppendDetail(string line)
    {
        if (_console.IsDisposed) return;
        _console.AppendLine(line);
        _lblLastLine.Text = line;
    }

    /// <summary>Shows the last log lines (incl. startup messages) in the activity area.</summary>
    private void LoadLogTail()
    {
        try
        {
            if (!File.Exists(Logger.LogFile)) return;
            string[] lines;
            using (var fs = new FileStream(Logger.LogFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs, System.Text.Encoding.UTF8))
            {
                lines = sr.ReadToEnd().Split('\n');
            }
            var tail = lines.Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).TakeLast(300).ToList();
            _console.SetLines(tail);
            if (tail.Count > 0) _lblLastLine.Text = tail[^1];
        }
        catch { /* ignore */ }
    }

    private void OpenLog()
    {
        Logger.EnsureExists();
        try { Process.Start(new ProcessStartInfo(Logger.LogFile) { UseShellExecute = true }); }
        catch
        {
            try { Process.Start(new ProcessStartInfo("notepad.exe", $"\"{Logger.LogFile}\"") { UseShellExecute = true }); }
            catch (Exception ex) { ThemedDialog.Info(this, Text, ex.Message, Tone.Error); }
        }
    }

    // =====================================================================
    // Tray / window behaviour
    // =====================================================================
    private void OnFormResize(object? sender, EventArgs e)
    {
        if (WindowState == FormWindowState.Minimized) HideToTray();
        else _restoreState = WindowState;
    }

    private void HideToTray()
    {
        Hide();
        if (!_balloonShown)
        {
            _balloonShown = true;
            _tray.ShowBalloonTip(4000, Lang.Get("app_title"), Lang.Get("tray_balloon"), ToolTipIcon.Info);
        }
    }

    private void RestoreFromTray()
    {
        Show();
        if (WindowState == FormWindowState.Minimized) WindowState = _restoreState;
        Activate();
        BringToFront();
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        // closing (X) only hides to tray; exit only via tray menu (or Windows shutdown)
        if (!_exitRequested && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            HideToTray();
        }
    }

    private void ExitApplication()
    {
        _exitRequested = true;
        Logger.Info(Lang.Get("log_exit"));
        Logger.LineWritten -= OnLogLine;
        _timer.Stop();
        try { _cts.Cancel(); } catch { /* ignore */ }
        _tray.Visible = false;
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Logger.LineWritten -= OnLogLine;
            Theme.Changed -= OnThemeChanged;
            Microsoft.Win32.SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            _tips.Dispose();
            _timer.Dispose();
            _noticeTimer.Dispose();
            _tray.Visible = false;
            _tray.Dispose();
            _trayIcon?.Dispose();
            _trayMenu.Dispose();
            _gridMenu.Dispose();
            _checker.Dispose();
            _cts.Dispose();
        }
        base.Dispose(disposing);
    }
}

/// <summary>Vertical settings stack: section caption + controls, full width, scrolls when needed.</summary>
public sealed class SettingsFlow : FlowLayoutPanel, IThemed
{
    private readonly List<Control> _fullWidth = new();

    public SettingsFlow()
    {
        FlowDirection = FlowDirection.TopDown;
        WrapContents = false;
        AutoScroll = true;
        DoubleBuffered = true;
        ApplyTheme();
    }

    public void AddSection(CaptionLabel caption, params Control[] items)
    {
        caption.Margin = new Padding(0, Ui.S(20), 0, Ui.S(8));
        Add(caption, true);
        foreach (var c in items)
        {
            c.Margin = new Padding(0, 0, 0, Ui.S(8));
            bool full = c is InputBox or SegmentedControl or NoticeLine or ThemedLabel;
            Add(c, full);
        }
    }

    public void AddFooter(Control c)
    {
        c.Margin = new Padding(0, Ui.S(28), 0, 0);
        Add(c, true);
    }

    private void Add(Control c, bool fullWidth)
    {
        if (fullWidth) _fullWidth.Add(c);
        Controls.Add(c);
    }

    public void ApplyTheme()
    {
        BackColor = Theme.Current.Surface;
        Theme.ApplyScrollbars(this); // AutoScroll uses the window's own scrollbar
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.ApplyScrollbars(this);
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        int w = ClientSize.Width - Padding.Horizontal;
        foreach (var c in _fullWidth)
        {
            if (c is ThemedLabel l)
            {
                l.MaximumSize = new Size(w, 0);
                l.AutoSize = true;
            }
            else if (c.Width != w) c.Width = w;
        }
        base.OnLayout(levent);
    }
}

/// <summary>Read-only log console with colored levels; keeps its own line buffer so it can re-render on theme change.</summary>
public sealed class ConsoleBox : RichTextBox
{
    private const int MaxLines = 1500;
    private readonly List<string> _lines = new();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    private const int WM_SETREDRAW = 0x000B;
    private const int WM_VSCROLL = 0x0115;
    private const int SB_BOTTOM = 7;

    public ConsoleBox()
    {
        ReadOnly = true;
        BorderStyle = BorderStyle.None;
        DetectUrls = false;
        WordWrap = true;
        ScrollBars = RichTextBoxScrollBars.Vertical;
        Font = Theme.MonoSmall;
        ApplyColors();
    }

    private void ApplyColors()
    {
        BackColor = Theme.Current.ConsoleBg;
        ForeColor = Theme.Current.ConsoleText;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.ApplyScrollbars(this);
    }

    public void SetLines(IEnumerable<string> lines)
    {
        _lines.Clear();
        _lines.AddRange(lines);
        if (_lines.Count > MaxLines) _lines.RemoveRange(0, _lines.Count - MaxLines);
        Rerender();
    }

    public void AppendLine(string line)
    {
        _lines.Add(line);
        if (_lines.Count > MaxLines + 300)
        {
            _lines.RemoveRange(0, _lines.Count - MaxLines);
            Rerender();
            return;
        }
        Write(line);
        ScrollToEnd();
    }

    public void Rerender()
    {
        if (IsDisposed) return;
        ApplyColors();
        Theme.ApplyScrollbars(this);
        bool handle = IsHandleCreated && Visible;
        if (handle) SendMessage(Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
        try
        {
            Clear();
            foreach (var l in _lines) Write(l);
        }
        finally
        {
            if (handle)
            {
                SendMessage(Handle, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
                Invalidate();
            }
        }
        ScrollToEnd();
    }

    public void ScrollToEnd()
    {
        if (!IsHandleCreated) return;
        SendMessage(Handle, WM_VSCROLL, (IntPtr)SB_BOTTOM, IntPtr.Zero);
    }

    private void Write(string line)
    {
        var pal = Theme.Current;
        // format: "yyyy-MM-dd HH:mm:ss [LEVEL] message"
        int lb = line.IndexOf('[');
        int rb = lb >= 0 ? line.IndexOf(']', lb) : -1;
        if (lb == 20 && rb > lb)
        {
            string time = line.Substring(0, lb);
            string level = line.Substring(lb, rb - lb + 1);
            string msg = line.Substring(rb + 1);
            Color lc = level.Contains("ERROR") ? pal.Error : level.Contains("WARN") ? pal.Warn : pal.TextMuted;
            Color mc = level.Contains("ERROR") ? pal.Error : level.Contains("WARN") ? pal.Warn : pal.ConsoleText;
            Append(time, pal.TextMuted);
            Append(level, lc);
            Append(msg + "\n", mc);
        }
        else Append(line + "\n", pal.ConsoleText);
    }

    private void Append(string text, Color color)
    {
        SelectionStart = TextLength;
        SelectionLength = 0;
        SelectionColor = color;
        AppendText(text);
    }
}
