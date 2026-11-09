namespace GitHubReleaseFetcher;

/// <summary>Separate window listing all entered repositories with their status.</summary>
public sealed class RepoListForm : Form
{
    private readonly ListView _lv = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true, MultiSelect = true, HideSelection = false };
    private readonly Label _lblCount = new() { AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 20, 3) };
    private readonly Button _btnRemove = new() { AutoSize = true };
    private readonly Button _btnClose = new() { AutoSize = true, DialogResult = DialogResult.OK };

    public List<string> RemovedRepos { get; } = new();

    public RepoListForm(IReadOnlyList<string> repos, IReadOnlyDictionary<string, RepoStatus> status, Icon? icon)
    {
        Text = Lang.Get("list_title");
        if (icon != null) Icon = icon;
        Size = new Size(820, 480);
        MinimumSize = new Size(500, 300);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MinimizeBox = false;

        _lv.Columns.Add(Lang.Get("col_repo"), 260);
        _lv.Columns.Add(Lang.Get("col_status"), 170);
        _lv.Columns.Add(Lang.Get("col_release"), 130);
        _lv.Columns.Add(Lang.Get("col_prerelease"), 130);
        _lv.Columns.Add(Lang.Get("col_lastcheck"), 120);

        foreach (var r in repos.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            status.TryGetValue(r, out var st);
            var state = st?.State ?? RepoState.Pending;
            var item = new ListViewItem(r) { Name = r, BackColor = MainForm.StateColor(state) };
            item.SubItems.Add(MainForm.StateText(state));
            item.SubItems.Add(string.IsNullOrEmpty(st?.StableTag) ? "-" : st!.StableTag);
            item.SubItems.Add(string.IsNullOrEmpty(st?.PreTag) ? "-" : st!.PreTag);
            item.SubItems.Add(st?.LastCheck?.ToString("g") ?? Lang.Get("never"));
            _lv.Items.Add(item);
        }

        _btnRemove.Text = Lang.Get("btn_remove");
        _btnClose.Text = Lang.Get("btn_close");
        UpdateCount();

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(6), FlowDirection = FlowDirection.LeftToRight };
        bottom.Controls.Add(_lblCount);
        bottom.Controls.Add(_btnRemove);
        bottom.Controls.Add(_btnClose);

        Controls.Add(_lv);
        Controls.Add(bottom);
        AcceptButton = _btnClose;
        CancelButton = _btnClose;

        _btnRemove.Click += (_, _) => RemoveSelected();
        _lv.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Delete) RemoveSelected();
        };
    }

    private void UpdateCount() => _lblCount.Text = Lang.F("list_count", _lv.Items.Count);

    private void RemoveSelected()
    {
        if (_lv.SelectedItems.Count == 0) return;
        if (MessageBox.Show(this, Lang.F("confirm_remove", _lv.SelectedItems.Count), Text,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        foreach (ListViewItem item in _lv.SelectedItems.Cast<ListViewItem>().ToList())
        {
            RemovedRepos.Add(item.Text);
            _lv.Items.Remove(item);
        }
        UpdateCount();
    }
}
