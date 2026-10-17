using LinkTrimmer.Core;

namespace LinkTrimmer.App;

/// <summary>Dialog dodawania / edycji pojedynczej reguły.</summary>
public sealed class RuleEditForm : Form
{
    private readonly TextBox _name = new();
    private readonly TextBox _hosts = new();
    private readonly TextBox _rewrite = new();
    private readonly CheckBox _keepOnly = new() { Text = Loc.T("edit.keepOnly"), AutoSize = true };
    private readonly TextBox _keep = new();
    private readonly TextBox _remove = new();
    private readonly CheckBox _enabled = new() { Text = Loc.T("edit.enabled"), AutoSize = true, Checked = true };

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public SiteRule Rule { get; }

    public RuleEditForm(SiteRule? existing)
    {
        Rule = existing ?? new SiteRule();
        Text = Loc.T(existing == null ? "edit.titleNew" : "edit.titleEdit");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = MinimizeBox = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        Icon = AppIcon.Create(32);

        var table = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1, Padding = new Padding(12) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        void Add(string label, Control c, string? hint = null)
        {
            table.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(0, 6, 0, 0) });
            c.Dock = DockStyle.Fill;
            table.Controls.Add(c);
            if (hint != null) table.Controls.Add(new Label { Text = hint, AutoSize = true, ForeColor = SystemColors.GrayText });
        }

        Add(Loc.T("edit.name"), _name);
        Add(Loc.T("edit.hosts"), _hosts, Loc.T("edit.hosts.hint"));
        Add(Loc.T("edit.rewrite"), _rewrite, Loc.T("edit.rewrite.hint"));
        table.Controls.Add(_keepOnly);
        Add(Loc.T("edit.keep"), _keep, Loc.T("edit.keep.hint"));
        Add(Loc.T("edit.remove"), _remove, Loc.T("edit.remove.hint"));
        table.Controls.Add(_enabled);
        _keep.Enabled = false;
        _keepOnly.CheckedChanged += (_, _) => _keep.Enabled = _keepOnly.Checked;

        var ok = new Button { Text = Loc.T("edit.save"), DialogResult = DialogResult.None };
        var cancel = new Button { Text = Loc.T("edit.cancel"), DialogResult = DialogResult.Cancel };
        foreach (var b in new[] { ok, cancel })
        {
            b.AutoSize = true;
            b.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            b.MinimumSize = new Size(110, 36);
            b.Padding = new Padding(10, 2, 10, 2);
            b.Margin = new Padding(6);
        }
        ok.Click += (_, _) => { if (Validate_()) { Apply(); DialogResult = DialogResult.OK; } };
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(6), WrapContents = false
        };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);

        // Dwa wiersze: treść i pasek przycisków – oba wysokości z zawartości, więc nic się nie ucina.
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(table, 0, 0);
        root.Controls.Add(buttons, 0, 1);
        Controls.Add(root);
        AcceptButton = ok;
        CancelButton = cancel;

        _name.Text = Rule.Name;
        _hosts.Text = string.Join(", ", Rule.Hosts);
        _rewrite.Text = Rule.RewriteHost ?? "";
        _keepOnly.Checked = Rule.KeepParams != null;
        _keep.Text = string.Join(", ", Rule.KeepParams ?? new());
        _remove.Text = string.Join(", ", Rule.RemoveParams);
        _enabled.Checked = existing?.Enabled ?? true;

        // Wysokość okna wyliczona z zawartości (uwzględnia DPI i czcionkę).
        const int width = 560;
        var h = root.GetPreferredSize(new Size(width, 0)).Height;
        ClientSize = new Size(width, h + 8);
    }

    private static List<string> Split(string s) =>
        s.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private bool Validate_()
    {
        if (Split(_hosts.Text).Count == 0)
        {
            MessageBox.Show(this, Loc.T("edit.err.hosts"), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        return true;
    }

    private void Apply()
    {
        Rule.Name = string.IsNullOrWhiteSpace(_name.Text) ? Split(_hosts.Text)[0] : _name.Text.Trim();
        Rule.Hosts = Split(_hosts.Text);
        Rule.RewriteHost = string.IsNullOrWhiteSpace(_rewrite.Text) ? null : _rewrite.Text.Trim();
        Rule.KeepParams = _keepOnly.Checked ? Split(_keep.Text) : null;
        Rule.RemoveParams = Split(_remove.Text);
        Rule.Enabled = _enabled.Checked;
    }
}
