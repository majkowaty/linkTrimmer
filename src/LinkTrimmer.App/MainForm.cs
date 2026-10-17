using LinkTrimmer.Core;

namespace LinkTrimmer.App;

/// <summary>Główne okno: tabela reguł + dodaj / edytuj / usuń. Zamknięcie "X" chowa do tray'a.</summary>
public sealed class MainForm : Form
{
    private readonly TrimmerConfig _config;
    private readonly string _configPath;
    private readonly DataGridView _grid = new();
    private readonly Button _add = new();
    private readonly Button _edit = new() { Enabled = false };
    private readonly Button _delete = new() { Enabled = false };

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool ReallyExit { get; set; }

    private readonly Label _langLabel = new() { AutoSize = true };
    private readonly ComboBox _lang = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
    private bool _fillingLang;
    private readonly CheckBox _autostart = new() { AutoSize = true };
    private bool _fillingAutostart;
    public event Action? HiddenToTray;

    public MainForm(TrimmerConfig config, string configPath)
    {
        _config = config;
        _configPath = configPath;

        Icon = AppIcon.Create(32);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(760, 420);
        MinimumSize = new Size(600, 300);
        StartPosition = FormStartPosition.CenterScreen;

        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.MultiSelect = false;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "On", FillWeight = 20 });
        _grid.Columns.Add("Name", "");
        _grid.Columns.Add("Hosts", "");
        _grid.Columns.Add("Rewrite", "");
        _grid.Columns.Add("Params", "");
        _grid.SelectionChanged += (_, _) => UpdateButtons();
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) EditSelected(); };
        _grid.KeyDown += (_, e) => { if (e.KeyCode == Keys.Delete) DeleteSelected(); };

        _add.Click += (_, _) => AddRule();
        _edit.Click += (_, _) => EditSelected();
        _delete.Click += (_, _) => DeleteSelected();

        foreach (var b in new[] { _add, _edit, _delete })
        {
            b.AutoSize = true;
            b.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            b.MinimumSize = new Size(110, 36);
            b.Padding = new Padding(10, 2, 10, 2);
            b.Margin = new Padding(6);
        }
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(6) };
        bar.Controls.AddRange(new Control[] { _add, _edit, _delete });

        // Wybór języka na końcu paska przycisków.
        _langLabel.Margin = new Padding(24, 14, 4, 0);
        _lang.Margin = new Padding(4, 10, 0, 0);
        foreach (var l in Loc.Languages) _lang.Items.Add(l.Name);
        _lang.SelectedIndexChanged += (_, _) =>
        {
            if (_fillingLang || _lang.SelectedIndex < 0) return;
            var code = Loc.Languages[_lang.SelectedIndex].Code;
            _config.Language = code;
            Save();
            Loc.SetLanguage(code);
        };
        bar.Controls.Add(_langLabel);
        bar.Controls.Add(_lang);

        // Autostart z Windowsem (wpis w rejestrze użytkownika); stan odświeżany przy każdej aktywacji okna.
        _autostart.Margin = new Padding(24, 14, 0, 0);
        _autostart.CheckedChanged += (_, _) =>
        {
            if (_fillingAutostart) return;
            if (!Autostart.Set(_autostart.Checked)) RefreshAutostart(); // się nie udało – pokaż prawdziwy stan
        };
        Activated += (_, _) => RefreshAutostart();
        bar.Controls.Add(_autostart);

        Controls.Add(_grid);
        Controls.Add(bar);

        Loc.Changed += ApplyTexts;
        Disposed += (_, _) => Loc.Changed -= ApplyTexts;
        ApplyTexts();
    }

    /// <summary>Ustawia wszystkie teksty zgodnie z bieżącym językiem.</summary>
    private void ApplyTexts()
    {
        Text = Loc.T("main.title");
        _grid.Columns["On"]!.HeaderText = Loc.T("main.col.on");
        // Nagłówek pierwszej kolumny musi się zawsze zmieścić (zależnie od języka i DPI), niezależnie od szerokości okna.
        var headerWidth = TextRenderer.MeasureText(_grid.Columns["On"]!.HeaderText, _grid.ColumnHeadersDefaultCellStyle.Font ?? _grid.Font).Width;
        _grid.Columns["On"]!.MinimumWidth = headerWidth + (int)(24 * DeviceDpi / 96.0);
        _grid.Columns["Name"]!.HeaderText = Loc.T("main.col.name");
        _grid.Columns["Hosts"]!.HeaderText = Loc.T("main.col.hosts");
        _grid.Columns["Rewrite"]!.HeaderText = Loc.T("main.col.rewrite");
        _grid.Columns["Params"]!.HeaderText = Loc.T("main.col.params");
        _add.Text = Loc.T("main.add");
        _edit.Text = Loc.T("main.edit");
        _delete.Text = Loc.T("main.delete");
        _langLabel.Text = Loc.T("main.language");
        _autostart.Text = Loc.T("main.autostart");
        RefreshAutostart();

        _fillingLang = true;
        _lang.SelectedIndex = Loc.Languages.ToList().FindIndex(l => l.Code == Loc.Current);
        _fillingLang = false;

        Refill();
    }

    private void RefreshAutostart()
    {
        _fillingAutostart = true;
        _autostart.Checked = Autostart.IsEnabled();
        _fillingAutostart = false;
    }

    private SiteRule? Selected =>
        _grid.SelectedRows.Count == 1 ? _grid.SelectedRows[0].Tag as SiteRule : null;

    private void UpdateButtons() => _edit.Enabled = _delete.Enabled = Selected != null;

    private void Refill()
    {
        var keep = Selected;
        _grid.Rows.Clear();
        foreach (var r in _config.Rules)
        {
            var parts = new List<string>();
            if (r.KeepParams != null)
                parts.Add(r.KeepParams.Count == 0 ? Loc.T("main.params.removeAll") : Loc.T("main.params.keep", string.Join(", ", r.KeepParams)));
            if (r.RemoveParams.Count > 0) parts.Add(Loc.T("main.params.remove", string.Join(", ", r.RemoveParams)));
            var i = _grid.Rows.Add(r.Enabled, r.Name, string.Join(", ", r.Hosts), r.RewriteHost ?? "", string.Join("; ", parts));
            _grid.Rows[i].Tag = r;
            if (r == keep) _grid.Rows[i].Selected = true;
        }
        UpdateButtons();
    }

    private void Save() => _config.Save(_configPath);

    private void AddRule()
    {
        using var dlg = new RuleEditForm(null);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        _config.Rules.Add(dlg.Rule);
        Save();
        Refill();
    }

    private void EditSelected()
    {
        if (Selected is not { } rule) return;
        using var dlg = new RuleEditForm(rule); // edytuje ten sam obiekt => LinkProcessor widzi zmianę od razu
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        Save();
        Refill();
    }

    private void DeleteSelected()
    {
        if (Selected is not { } rule) return;
        if (MessageBox.Show(this, Loc.T("main.confirmDelete", rule.Name), Text,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        _config.Rules.Remove(rule);
        Save();
        Refill();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!ReallyExit && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            HiddenToTray?.Invoke();
        }
        base.OnFormClosing(e);
    }

    public void ShowFromTray()
    {
        Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
    }
}
