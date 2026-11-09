using System.Diagnostics;
using System.Reflection;

namespace GitHubReleaseFetcher;

public sealed class MainForm : Form
{
    // ---------------- state
    private readonly AppSettings _settings;
    private readonly ReleaseChecker _checker = new();
    private readonly Dictionary<string, RepoStatus> _status = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 15000 };
    private CancellationTokenSource _cts = new();
    private bool _checking;
    private bool _exitRequested;
    private bool _balloonShown;
    private bool _loadingUi;
    private DateTime _nextCheckUtc;
    private DateTime? _rateLimitUntilUtc;
    private DateTime _lastLogCleanup = DateTime.MinValue;

    // ---------------- controls
    private readonly GroupBox _grpSettings = new() { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(8) };
    private readonly GroupBox _grpRepos = new() { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(8) };
    private readonly GroupBox _grpStatus = new() { Dock = DockStyle.Fill, Padding = new Padding(8) };

    private readonly Label _lblFolder = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly TextBox _txtFolder = new() { ReadOnly = true, Anchor = AnchorStyles.Left | AnchorStyles.Right };
    private readonly Button _btnBrowse = new() { AutoSize = true };
    private readonly Button _btnOpenFolder = new() { AutoSize = true };

    private readonly Label _lblToken = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly TextBox _txtToken = new() { UseSystemPasswordChar = true, Anchor = AnchorStyles.Left | AnchorStyles.Right };
    private readonly Button _btnSaveToken = new() { AutoSize = true };
    private readonly Button _btnRemoveToken = new() { AutoSize = true };
    private readonly Label _lblTokenState = new() { AutoSize = true, Anchor = AnchorStyles.Left };

    private readonly Label _lblInterval = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly NumericUpDown _numInterval = new() { Minimum = AppSettings.MinIntervalHours, Maximum = AppSettings.MaxIntervalHours, Width = 70, Anchor = AnchorStyles.Left };
    private readonly Label _lblEffective = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly Label _lblLanguage = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly ComboBox _cmbLanguage = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110, Anchor = AnchorStyles.Left };

    private readonly Label _lblRepoInput = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly TextBox _txtRepo = new() { Anchor = AnchorStyles.Left | AnchorStyles.Right };
    private readonly Button _btnAdd = new() { AutoSize = true };
    private readonly Button _btnShowList = new() { AutoSize = true };
    private readonly Button _btnCheckNow = new() { AutoSize = true };
    private readonly Button _btnOpenLog = new() { AutoSize = true };

    private readonly Label _lblStatusLine = new() { Dock = DockStyle.Top, AutoSize = false, Height = 24, TextAlign = ContentAlignment.MiddleLeft };
    private readonly SplitContainer _split = new() { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
    private readonly ListView _lv = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true, HideSelection = false, MultiSelect = false };
    private readonly Label _lblDetails = new() { Dock = DockStyle.Top, AutoSize = false, Height = 20, TextAlign = ContentAlignment.BottomLeft };
    private readonly TextBox _txtDetails = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font(FontFamily.GenericMonospace, 9f), BackColor = SystemColors.Window };

    private readonly NotifyIcon _tray = new();
    private readonly ContextMenuStrip _trayMenu = new();
    private readonly ToolStripMenuItem _miState = new() { Enabled = false };
    private readonly ToolStripMenuItem _miLast = new() { Enabled = false };
    private readonly ToolStripMenuItem _miNext = new() { Enabled = false };
    private readonly ToolStripMenuItem _miRepos = new() { Enabled = false };
    private readonly ToolStripMenuItem _miRate = new() { Enabled = false };
    private readonly ToolStripMenuItem _miOpen = new();
    private readonly ToolStripMenuItem _miCheck = new();
    private readonly ToolStripMenuItem _miExit = new();

    public MainForm()
    {
        _settings = AppSettings.Load();
        Lang.Current = _settings.Language;

        Logger.Cleanup();
        _lastLogCleanup = DateTime.Now;
        Logger.LineWritten += OnLogLine;

        Icon appIcon = LoadAppIcon();
        Icon = appIcon;
        Size = new Size(1100, 760);
        MinimumSize = new Size(820, 560);
        StartPosition = FormStartPosition.CenterScreen;

        BuildUi();
        BuildTray(appIcon);

        _loadingUi = true;
        _txtFolder.Text = _settings.DownloadFolder;
        _numInterval.Value = Math.Clamp(_settings.CheckIntervalHours, AppSettings.MinIntervalHours, AppSettings.MaxIntervalHours);
        _cmbLanguage.Items.AddRange(new object[] { "English", "Deutsch" });
        _cmbLanguage.SelectedIndex = _settings.Language == "de" ? 1 : 0;
        _loadingUi = false;

        foreach (var r in _settings.Repositories) _status[r] = new RepoStatus { Repo = r };

        ApplyLanguage();

        string ver = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
        Logger.Info(Lang.F("log_start", ver));
        if (AppSettings.LoadError != null) Logger.Warn(Lang.F("log_settings_load_failed", AppSettings.LoadError));

        // first check shortly after start if overdue / never done
        DateTime due = (_settings.LastCheckUtc ?? DateTime.MinValue).AddHours(EffectiveIntervalHours());
        _nextCheckUtc = due <= DateTime.UtcNow.AddSeconds(10) ? DateTime.UtcNow.AddSeconds(10) : due;
        Logger.Info(Lang.F("log_next", _nextCheckUtc.ToLocalTime().ToString("g")));

        _timer.Tick += OnTimerTick;
        _timer.Start();
        UpdateStatusLine();
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
        // ---- settings group
        var tlS = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 4 };
        tlS.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        tlS.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        tlS.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        tlS.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        tlS.Controls.Add(_lblFolder, 0, 0);
        tlS.Controls.Add(_txtFolder, 1, 0);
        tlS.Controls.Add(_btnBrowse, 2, 0);
        tlS.Controls.Add(_btnOpenFolder, 3, 0);

        tlS.Controls.Add(_lblToken, 0, 1);
        tlS.Controls.Add(_txtToken, 1, 1);
        tlS.Controls.Add(_btnSaveToken, 2, 1);
        tlS.Controls.Add(_btnRemoveToken, 3, 1);

        tlS.Controls.Add(new Label { AutoSize = true }, 0, 2);
        tlS.Controls.Add(_lblTokenState, 1, 2);
        tlS.SetColumnSpan(_lblTokenState, 3);

        var flInterval = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(0) };
        flInterval.Controls.Add(_numInterval);
        _lblEffective.Margin = new Padding(10, 6, 20, 0);
        flInterval.Controls.Add(_lblEffective);
        _lblLanguage.Margin = new Padding(10, 6, 3, 0);
        flInterval.Controls.Add(_lblLanguage);
        flInterval.Controls.Add(_cmbLanguage);

        tlS.Controls.Add(_lblInterval, 0, 3);
        tlS.Controls.Add(flInterval, 1, 3);
        tlS.SetColumnSpan(flInterval, 3);
        _grpSettings.Controls.Add(tlS);

        // ---- repos group
        var tlR = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2 };
        tlR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        tlR.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        tlR.Controls.Add(_lblRepoInput, 0, 0);
        tlR.SetColumnSpan(_lblRepoInput, 2);
        tlR.Controls.Add(_txtRepo, 0, 1);
        tlR.Controls.Add(_btnAdd, 1, 1);
        var flBtns = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill, WrapContents = true, Margin = new Padding(0) };
        flBtns.Controls.Add(_btnShowList);
        flBtns.Controls.Add(_btnCheckNow);
        flBtns.Controls.Add(_btnOpenLog);
        tlR.Controls.Add(flBtns, 0, 2);
        tlR.SetColumnSpan(flBtns, 2);
        _grpRepos.Controls.Add(tlR);

        // ---- status group
        _lv.Columns.Add("", 230);
        _lv.Columns.Add("", 170);
        _lv.Columns.Add("", 140);
        _lv.Columns.Add("", 140);
        _lv.Columns.Add("", 130);
        _lv.Columns.Add("", 300);
        _split.Panel1.Controls.Add(_lv);
        _split.Panel2.Controls.Add(_txtDetails);
        _split.Panel2.Controls.Add(_lblDetails);
        _grpStatus.Controls.Add(_split);
        _grpStatus.Controls.Add(_lblStatusLine);

        // order matters for docking: Fill first, then Top controls (last added = top-most)
        Controls.Add(_grpStatus);
        Controls.Add(_grpRepos);
        Controls.Add(_grpSettings);

        // ---- events
        _btnBrowse.Click += (_, _) => BrowseFolder();
        _btnOpenFolder.Click += (_, _) => OpenFolder();
        _btnSaveToken.Click += async (_, _) => await SaveTokenAsync();
        _btnRemoveToken.Click += (_, _) => RemoveToken();
        _numInterval.ValueChanged += (_, _) => OnIntervalChanged();
        _cmbLanguage.SelectedIndexChanged += (_, _) => OnLanguageChanged();
        _btnAdd.Click += (_, _) => AddRepositories();
        _txtRepo.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; AddRepositories(); }
        };
        _btnShowList.Click += (_, _) => ShowRepoList();
        _btnCheckNow.Click += async (_, _) => await ManualCheckAsync();
        _btnOpenLog.Click += (_, _) => OpenLog();

        Resize += OnFormResize;
        FormClosing += OnFormClosing;
        Shown += (_, _) =>
        {
            try { _split.SplitterDistance = Math.Max(100, _split.Height / 2); } catch { /* ignore */ }
            LoadLogTail();
        };
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
        _trayMenu.Opening += (_, _) => UpdateTrayMenu();

        _miOpen.Click += (_, _) => RestoreFromTray();
        _miCheck.Click += async (_, _) => await ManualCheckAsync();
        _miExit.Click += (_, _) => ExitApplication();

        _tray.Icon = icon;
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
        _grpSettings.Text = Lang.Get("grp_settings");
        _grpRepos.Text = Lang.Get("grp_repos");
        _grpStatus.Text = Lang.Get("grp_status");
        _lblDetails.Text = Lang.Get("grp_details");

        _lblFolder.Text = Lang.Get("lbl_folder");
        _btnBrowse.Text = Lang.Get("btn_browse");
        _btnOpenFolder.Text = Lang.Get("btn_open_folder");
        _lblToken.Text = Lang.Get("lbl_token");
        _btnSaveToken.Text = Lang.Get("btn_save_token");
        _btnRemoveToken.Text = Lang.Get("btn_remove_token");
        _lblInterval.Text = Lang.Get("lbl_interval");
        _lblLanguage.Text = Lang.Get("lbl_language");

        _lblRepoInput.Text = Lang.Get("lbl_repo_input");
        _btnAdd.Text = Lang.Get("btn_add");
        _btnShowList.Text = Lang.Get("btn_show_list");
        _btnCheckNow.Text = Lang.Get("btn_check_now");
        _btnOpenLog.Text = Lang.Get("btn_open_log");

        _lv.Columns[0].Text = Lang.Get("col_repo");
        _lv.Columns[1].Text = Lang.Get("col_status");
        _lv.Columns[2].Text = Lang.Get("col_release");
        _lv.Columns[3].Text = Lang.Get("col_prerelease");
        _lv.Columns[4].Text = Lang.Get("col_lastcheck");
        _lv.Columns[5].Text = Lang.Get("col_details");

        _miOpen.Text = Lang.Get("tray_open");
        _miCheck.Text = Lang.Get("tray_check");
        _miExit.Text = Lang.Get("tray_exit");

        UpdateTokenState();
        UpdateEffectiveInterval();
        RefreshRepoList();
        UpdateStatusLine();
    }

    // =====================================================================
    // Settings handlers
    // =====================================================================
    private void BrowseFolder()
    {
        using var dlg = new FolderBrowserDialog { ShowNewFolderButton = true, UseDescriptionForTitle = true, Description = Lang.Get("lbl_folder") };
        if (Directory.Exists(_settings.DownloadFolder)) dlg.SelectedPath = _settings.DownloadFolder;
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        _settings.DownloadFolder = dlg.SelectedPath;
        _settings.Save();
        _txtFolder.Text = dlg.SelectedPath;
        Logger.Info(Lang.F("log_folder_set", dlg.SelectedPath));
    }

    private void OpenFolder()
    {
        if (!Directory.Exists(_settings.DownloadFolder))
        {
            MessageBox.Show(this, Lang.Get("msg_no_folder"), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        _settings.Save();
        _txtToken.Clear();
        UpdateTokenState();
        UpdateEffectiveInterval();

        _btnSaveToken.Enabled = false;
        try
        {
            var (ok, limit, error) = await _checker.ValidateTokenAsync(token, _cts.Token);
            string msg;
            if (ok)
            {
                msg = Lang.F("msg_token_check_ok", limit?.ToString() ?? Lang.Get("unknown"));
                Logger.Info(msg);
                MessageBox.Show(this, msg, Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                msg = error.StartsWith("HTTP") ? Lang.F("msg_token_check_fail", error) : Lang.F("msg_token_check_unknown", error);
                Logger.Warn(msg);
                MessageBox.Show(this, msg, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        finally
        {
            _btnSaveToken.Enabled = true;
            UpdateStatusLine();
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
    }

    private void UpdateTokenState()
    {
        bool has = _settings.HasToken;
        _lblTokenState.Text = Lang.Get(has ? "token_set" : "token_not_set");
        _lblTokenState.ForeColor = has ? Color.DarkGreen : Color.DarkOrange;
    }

    private void OnIntervalChanged()
    {
        if (_loadingUi) return;
        _settings.CheckIntervalHours = (int)_numInterval.Value;
        _settings.Save();
        Logger.Info(Lang.F("log_interval_set", _settings.CheckIntervalHours));
        UpdateEffectiveInterval();
        RecalculateNextCheck();
    }

    private void OnLanguageChanged()
    {
        if (_loadingUi) return;
        _settings.Language = _cmbLanguage.SelectedIndex == 1 ? "de" : "en";
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
            _lblEffective.ForeColor = Color.DarkOrange;
        }
        else
        {
            _lblEffective.Text = Lang.F("interval_ok", eff);
            _lblEffective.ForeColor = SystemColors.ControlText;
        }
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
        UpdateStatusLine();
    }

    // =====================================================================
    // Repositories
    // =====================================================================
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
            UpdateStatusLine();
        }

        _txtRepo.Text = problems.Count > 0 && added == 0 ? _txtRepo.Text : "";
        if (problems.Count > 0)
            MessageBox.Show(this, string.Join(Environment.NewLine, problems), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void ShowRepoList()
    {
        using var f = new RepoListForm(_settings.Repositories, _status, Icon);
        f.ShowDialog(this);
        if (f.RemovedRepos.Count == 0) return;

        foreach (var r in f.RemovedRepos)
        {
            _settings.Repositories.RemoveAll(x => string.Equals(x, r, StringComparison.OrdinalIgnoreCase));
            _status.Remove(r);
            Logger.Info(Lang.F("log_repo_removed", r));
        }
        _settings.Save();
        RefreshRepoList();
        UpdateEffectiveInterval();
        UpdateStatusLine();
    }

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

    public static Color StateColor(RepoState s) => s switch
    {
        RepoState.Ok or RepoState.NoReleases => Color.FromArgb(220, 245, 220),
        RepoState.NotReachable => Color.FromArgb(250, 215, 215),
        RepoState.RateLimited => Color.FromArgb(255, 235, 200),
        RepoState.Checking => Color.FromArgb(215, 230, 250),
        _ => SystemColors.Window
    };

    private void RefreshRepoList()
    {
        _lv.BeginUpdate();
        try
        {
            _lv.Items.Clear();
            foreach (var repo in _settings.Repositories)
            {
                if (!_status.TryGetValue(repo, out var st))
                {
                    st = new RepoStatus { Repo = repo };
                    _status[repo] = st;
                }
                var item = new ListViewItem(repo) { Name = repo, BackColor = StateColor(st.State) };
                item.SubItems.Add(StateText(st.State));
                item.SubItems.Add(st.StableTag.Length > 0 ? st.StableTag : "-");
                item.SubItems.Add(st.PreTag.Length > 0 ? st.PreTag : "-");
                item.SubItems.Add(st.LastCheck?.ToString("g") ?? Lang.Get("never"));
                item.SubItems.Add(st.Details);
                _lv.Items.Add(item);
            }
        }
        finally
        {
            _lv.EndUpdate();
        }
    }

    private void UpdateRepoRow(RepoStatus st)
    {
        var items = _lv.Items.Find(st.Repo, false);
        if (items.Length == 0) { RefreshRepoList(); return; }
        var item = items[0];
        item.BackColor = StateColor(st.State);
        item.SubItems[1].Text = StateText(st.State);
        item.SubItems[2].Text = st.StableTag.Length > 0 ? st.StableTag : "-";
        item.SubItems[3].Text = st.PreTag.Length > 0 ? st.PreTag : "-";
        item.SubItems[4].Text = st.LastCheck?.ToString("g") ?? Lang.Get("never");
        item.SubItems[5].Text = st.Details;
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

            UpdateStatusLine();
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
            MessageBox.Show(this, Lang.Get("msg_already_checking"), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (_rateLimitUntilUtc.HasValue && _rateLimitUntilUtc.Value > DateTime.UtcNow)
        {
            string msg = Lang.F("msg_rate_wait", _rateLimitUntilUtc.Value.ToLocalTime().ToString("g"));
            Logger.Warn(msg);
            if (Visible) MessageBox.Show(this, msg, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            UpdateStatusLine();
            if (Visible) MessageBox.Show(this, Lang.Get("msg_no_folder"), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _checking = true;
        _btnCheckNow.Enabled = false;
        _miCheck.Enabled = false;
        UpdateStatusLine();

        var repos = _settings.Repositories.ToList();
        string folder = _settings.DownloadFolder;
        string token = _settings.GetToken();
        int ok = 0, fail = 0, postponed = 0, newFiles = 0;
        _rateLimitUntilUtc = null;

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
                    continue;
                }

                st.State = RepoState.Checking;
                UpdateRepoRow(st);

                RepoCheckResult r = await _checker.CheckRepoAsync(repo, folder, token, _cts.Token);

                // repo may have been removed from the list meanwhile
                st.State = r.State;
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

                if (_status.ContainsKey(repo)) UpdateRepoRow(st);
                UpdateStatusLine();

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

        UpdateStatusLine();

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

    private string StateNow() => _checking ? Lang.Get("status_checking") : Lang.Get("status_idle");
    private string LastText() => _settings.LastCheckUtc?.ToLocalTime().ToString("g") ?? Lang.Get("never");
    private string NextText() => _nextCheckUtc == default ? Lang.Get("unknown") : _nextCheckUtc.ToLocalTime().ToString("g");
    private string RateText()
    {
        if (!_checker.RateRemaining.HasValue) return Lang.Get("unknown");
        string s = $"{_checker.RateRemaining}/{_checker.RateLimit?.ToString() ?? "?"}";
        if (_checker.RateResetUtc.HasValue) s += $" (reset {_checker.RateResetUtc.Value.ToLocalTime():t})";
        return s;
    }

    private void UpdateStatusLine()
    {
        var (reach, _, total) = Counts();
        _lblStatusLine.Text = Lang.F("status_line", StateNow(), LastText(), NextText(), reach, total, RateText());

        string tip = $"GitHub Release Fetcher - {StateNow()}";
        _tray.Text = tip.Length > 63 ? tip.Substring(0, 63) : tip;
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
        if (_txtDetails.IsDisposed) return;
        if (_txtDetails.TextLength > 400_000)
        {
            string t = _txtDetails.Text;
            int cut = t.IndexOf('\n', t.Length / 2);
            _txtDetails.Text = cut > 0 ? t.Substring(cut + 1) : "";
        }
        _txtDetails.AppendText(line + Environment.NewLine);
    }

    /// <summary>Shows the last log lines (incl. startup messages) in the detail area.</summary>
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
            var tail = lines.Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).TakeLast(300);
            _txtDetails.Text = string.Join(Environment.NewLine, tail) + Environment.NewLine;
            _txtDetails.SelectionStart = _txtDetails.TextLength;
            _txtDetails.ScrollToCaret();
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
            catch (Exception ex) { MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
    }

    // =====================================================================
    // Tray / window behaviour
    // =====================================================================
    private void OnFormResize(object? sender, EventArgs e)
    {
        if (WindowState == FormWindowState.Minimized) HideToTray();
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
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
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
            _timer.Dispose();
            _tray.Visible = false;
            _tray.Dispose();
            _trayMenu.Dispose();
            _checker.Dispose();
            _cts.Dispose();
        }
        base.Dispose(disposing);
    }
}
