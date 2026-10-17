using LinkTrimmer.Core;

namespace LinkTrimmer.App;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // Tylko jedna instancja naraz.
        using var mutex = new Mutex(true, "LinkTrimmer.SingleInstance", out var isNew);
        if (!isNew) return;

        ApplicationConfiguration.Initialize();

        var configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LinkTrimmer", "config.json");
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        var config = TrimmerConfig.Load(configPath);

        Loc.Init(config.Language); // zapisany język albo wykryty z systemu

        // LinkProcessor trzyma referencję do tego samego obiektu config, więc edycje w oknie działają od razu.
        using var watcher = new ClipboardWatcher(new LinkProcessor(config));
        using var form = new MainForm(config, configPath);

        using var tray = new NotifyIcon { Icon = AppIcon.CreateTray(), Text = "LinkTrimmer", Visible = true };
        var menu = new ContextMenuStrip();
        var openItem = new ToolStripMenuItem();
        openItem.Click += (_, _) => form.ShowFromTray();
        var toggle = new ToolStripMenuItem { Checked = true, CheckOnClick = true };
        toggle.CheckedChanged += (_, _) => watcher.Enabled = toggle.Checked;
        var langMenu = new ToolStripMenuItem();
        var langItems = Loc.Languages.Select(l =>
        {
            var item = new ToolStripMenuItem(l.Name) { Tag = l.Code };
            item.Click += (_, _) =>
            {
                config.Language = l.Code;
                config.Save(configPath);
                Loc.SetLanguage(l.Code);
            };
            langMenu.DropDownItems.Add(item);
            return item;
        }).ToList();
        var autostartItem = new ToolStripMenuItem();
        autostartItem.Click += (_, _) => Autostart.Set(!Autostart.IsEnabled());
        menu.Opening += (_, _) => autostartItem.Checked = Autostart.IsEnabled(); // stan zawsze z rejestru
        var exitItem = new ToolStripMenuItem();
        exitItem.Click += (_, _) =>
        {
            form.ReallyExit = true;
            tray.Visible = false; // usuwa ikonę z trayu (bez "ducha" ikony)
            watcher.Dispose();    // odpina nasłuch schowka
            Application.Exit();
        };
        menu.Items.AddRange(new ToolStripItem[] { openItem, toggle, autostartItem, langMenu, new ToolStripSeparator(), exitItem });
        tray.ContextMenuStrip = menu;

        void ApplyTrayTexts()
        {
            openItem.Text = Loc.T("tray.open");
            toggle.Text = Loc.T("tray.enabled");
            autostartItem.Text = Loc.T("tray.autostart");
            langMenu.Text = Loc.T("tray.language");
            exitItem.Text = Loc.T("tray.exit");
            foreach (var i in langItems) i.Checked = (string)i.Tag! == Loc.Current;
        }
        Loc.Changed += ApplyTrayTexts;
        ApplyTrayTexts();
        tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) form.ShowFromTray(); };

        var tipShown = false;
        form.HiddenToTray += () =>
        {
            if (tipShown) return;
            tipShown = true;
            tray.ShowBalloonTip(2000, "LinkTrimmer", Loc.T("tray.balloon"), ToolTipIcon.None);
        };

        if (args.Contains("--minimized"))
        {
            form.ShowInTaskbar = true;
            form.Load += (_, _) => form.BeginInvoke(form.Hide);
        }

        Application.Run(form);

        // Pewność, że żaden wątek w tle nie zatrzyma procesu – aplikacja kończy się całkowicie.
        Environment.Exit(0);
    }
}
