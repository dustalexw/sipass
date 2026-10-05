using System.Diagnostics;
using System.Reflection;
using System.Text.Json.Nodes;
using YtdlpStudio.Core;
namespace YtdlpStudio.Windows;

public sealed class MainForm : Form {
    readonly SettingsStore store = new();
    readonly ToolPaths tools = ToolPaths.Bundled();
    readonly DownloadQueue queue;
    readonly TextBox urls = new() { Multiline = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, PlaceholderText = "Paste video or playlist links, one per line", AccessibleName = "Video links" };
    readonly TextBox command = new() { ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Horizontal, WordWrap = false, Dock = DockStyle.Fill, Font = new Font("Consolas", 9), AccessibleName = "PowerShell download command" };
    readonly ListBox categories = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, ItemHeight = 32, IntegralHeight = false };
    readonly OptionsPanel options;
    readonly DataGridView grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoGenerateColumns = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, RowHeadersVisible = false, BackgroundColor = SystemColors.Window, BorderStyle = BorderStyle.None };
    readonly Label footer = new() { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, ForeColor = SystemColors.GrayText };
    readonly Label commandNote = new() { AutoSize = true, ForeColor = SystemColors.GrayText };
    readonly Button analyze, findComments, cancelSearch, download;
    readonly System.Windows.Forms.Timer timer = new() { Interval = 250 };
    readonly Dictionary<string, string> selections = new();
    CancellationTokenSource? searchCancellation;
    bool settingsDirty, closing, ready;
    DateTime lastEdit;
    public DownloadOptions Current => store.Settings.Options;

    public MainForm() {
        queue = new(tools, path => Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(path, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin));
        options = new OptionsPanel(OptionsChanged);
        Text = "YT-DLP Studio 2.1 — Windows"; Font = new Font("Segoe UI", 10); Size = new Size(1280, 820); MinimumSize = new Size(1080, 680); StartPosition = FormStartPosition.CenterScreen;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(12) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 150)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 110)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26)); Controls.Add(root);
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 315));
        header.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); header.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); header.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        var title = new Label { Text = "YT-DLP Studio", AutoSize = true, Font = new Font("Segoe UI", 18, FontStyle.Bold) }; header.Controls.Add(title, 0, 0);
        header.Controls.Add(urls, 0, 1);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(8, 0, 0, 0) };
        download = Button("Download", StartDownload); download.Width = 290; download.Height = 34;
        analyze = Button("Analyze formats…", () => _ = AnalyzeAsync()); analyze.Width = 290;
        findComments = Button("Find comment chapters…", () => _ = FindCommentsAsync()); findComments.Width = 290;
        actions.Controls.AddRange([download, analyze, findComments]); header.Controls.Add(actions, 1, 1); header.SetRowSpan(actions, 2);
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        toolbar.Controls.Add(Button("Paste links", () => { if (Clipboard.ContainsText()) urls.Text = urls.Text.Trim().Length == 0 ? Clipboard.GetText().Trim() : urls.Text.Trim() + Environment.NewLine + Clipboard.GetText().Trim(); }));
        toolbar.Controls.Add(Button("Presets ▾", ShowPresets));
        toolbar.Controls.Add(Button("Open download folder", () => OpenFolder(Current.OutputDirectory)));
        cancelSearch = Button("Cancel search", () => searchCancellation?.Cancel()); cancelSearch.Visible = false; toolbar.Controls.Add(cancelSearch);
        header.Controls.Add(toolbar, 0, 2); root.Controls.Add(header, 0, 0);
        var split = new SplitContainer { Dock = DockStyle.Fill, Size = new Size(1230, 500), SplitterDistance = 700, Panel1MinSize = 640, Panel2MinSize = 340 };
        var left = new SplitContainer { Dock = DockStyle.Fill, Size = new Size(695, 500), SplitterDistance = 190, Panel1MinSize = 170, Panel2MinSize = 430, FixedPanel = FixedPanel.Panel1 };
        left.Panel1.Controls.Add(categories); left.Panel2.Controls.Add(options); split.Panel1.Controls.Add(left);
        foreach (var category in typeof(DownloadOptions).GetProperties().Select(p => p.GetCustomAttribute<SettingAttribute>()?.Category).Where(c => c != null).Distinct()) categories.Items.Add(category!);
        categories.SelectedIndexChanged += (_, _) => { if (categories.SelectedItem is string category) options.ShowCategory(Current, category); }; categories.SelectedIndex = 0;
        var queuePanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
        queuePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); queuePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); queuePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        queuePanel.Controls.Add(new Label { Text = "Download queue", AutoSize = true, Font = new Font("Segoe UI", 12, FontStyle.Bold) }, 0, 0);
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Title", HeaderText = "Video", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 120 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", Width = 150 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Progress", HeaderText = "Progress", Width = 70 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Speed", HeaderText = "Speed", Width = 80 });
        grid.CellDoubleClick += (_, _) => ShowLog(); queuePanel.Controls.Add(grid, 0, 1);
        var queueActions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true };
        queueActions.Controls.AddRange([Button("Cancel", () => { if (SelectedJob() is { } j) queue.Cancel(j); }), Button("Retry", () => { if (SelectedJob() is { } j) queue.Retry(j); }), Button("Show files", RevealFiles), Button("View log", ShowLog), Button("Clear finished", () => queue.ClearFinished()), Button("Cancel all", () => queue.CancelAll())]);
        queuePanel.Controls.Add(queueActions, 0, 2); split.Panel2.Controls.Add(queuePanel); root.Controls.Add(split, 0, 1);
        var preview = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 2, Padding = new Padding(0, 8, 0, 0) };
        preview.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); preview.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130)); preview.RowStyles.Add(new RowStyle(SizeType.Absolute, 26)); preview.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        preview.Controls.Add(commandNote, 0, 0); preview.SetColumnSpan(commandNote, 2); preview.Controls.Add(command, 0, 1);
        preview.Controls.Add(Button("Copy command", () => { if (command.Text.Length > 0) Clipboard.SetText(command.Text); }), 1, 1); root.Controls.Add(preview, 0, 2); root.Controls.Add(footer, 0, 3);
        urls.TextChanged += (_, _) => UpdateCommand(); timer.Tick += (_, _) => Tick(); timer.Start(); UpdateCommand();
        Shown += async (_, _) => {
            try {
                tools.EnsureAvailable(); ready = true;
                var yt = await ProcessRunner.RunAsync(tools.Ytdlp, ["--version"]);
                footer.Text = "Bundled yt-dlp " + yt.Stdout.Trim() + " · FFmpeg and Deno included · Windows x64 · 2.1.0";
                if (store.LoadWarning != null) MessageBox.Show(this, store.LoadWarning, "Settings restored");
            } catch (Exception error) { footer.Text = error.Message; }
            UpdateButtons();
        };
        FormClosing += async (_, e) => {
            if (closing) return; e.Cancel = true; closing = true; timer.Stop(); searchCancellation?.Cancel();
            try { store.Save(); await queue.StopAsync(); } catch (Exception error) { MessageBox.Show(error.Message, "Closing YT-DLP Studio"); }
            Close();
        };
    }
    static Button Button(string text, Action action) { var b = new Button { Text = text, AutoSize = true, Padding = new Padding(6, 2, 6, 2) }; b.Click += (_, _) => action(); return b; }
    string[] Links => urls.Lines.Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
    void OptionsChanged() { settingsDirty = true; lastEdit = DateTime.UtcNow; queue.MaxConcurrent = Current.MaxConcurrent; UpdateCommand(); }
    void UpdateCommand() {
        try {
            command.Text = CommandBuilder.Display(tools.Ytdlp, CommandBuilder.Arguments(Current, Links.Length > 0 ? Links : ["<URL>"], tools));
            commandNote.Text = Current.ChapterSource == "youtube" ? "PowerShell download command" : "Comment chapters are prepared by the app; this command shows download options only.";
        } catch (Exception error) { command.Text = ""; commandNote.Text = error.Message; }
        UpdateButtons();
    }
    void UpdateButtons() { download.Enabled = ready && Links.Length > 0; analyze.Enabled = ready && Links.Length > 0 && searchCancellation == null; findComments.Enabled = analyze.Enabled; }
    void Tick() {
        var jobs = queue.Jobs; var selectedId = SelectedJob()?.Id;
        var existing = grid.Rows.Cast<DataGridViewRow>().Where(r => r.Tag is DownloadJob).ToDictionary(r => ((DownloadJob)r.Tag!).Id);
        foreach (var job in jobs) {
            if (!existing.TryGetValue(job.Id, out var row)) { int index = grid.Rows.Add(); row = grid.Rows[index]; row.Tag = job; }
            row.Cells[0].Value = job.Title; row.Cells[1].Value = job.Stage; row.Cells[2].Value = (job.Progress * 100).ToString("0") + "%"; row.Cells[3].Value = job.Speed;
            foreach (DataGridViewCell cell in row.Cells) cell.ToolTipText = job.Stage + (job.Eta.Length > 0 ? " · ETA " + job.Eta : "");
            row.DefaultCellStyle.ForeColor = job.State == JobState.Failed ? Color.Firebrick : SystemColors.ControlText;
        }
        foreach (var row in grid.Rows.Cast<DataGridViewRow>().ToArray()) if (row.Tag is DownloadJob job && !jobs.Contains(job)) grid.Rows.Remove(row);
        if (selectedId != null) foreach (DataGridViewRow row in grid.Rows) if (((DownloadJob)row.Tag!).Id == selectedId) row.Selected = true;
        if (settingsDirty && DateTime.UtcNow - lastEdit > TimeSpan.FromSeconds(1)) {
            try { store.Save(); settingsDirty = false; } catch (IOException error) { footer.Text = "Could not save settings: " + error.Message; }
        }
    }
    DownloadJob? SelectedJob() => grid.SelectedRows.Count > 0 ? grid.SelectedRows[0].Tag as DownloadJob : null;
    void StartDownload() {
        try { queue.MaxConcurrent = Current.MaxConcurrent; queue.Enqueue(Links, Current, selections); urls.Clear(); }
        catch (Exception error) { Error(error); }
    }
    async Task AnalyzeAsync() {
        if (Links.FirstOrDefault() is not { } url || searchCancellation != null) return;
        var o = Current.Clone(); searchCancellation = new(); cancelSearch.Visible = true; UpdateButtons(); footer.Text = "Analyzing video…";
        try {
            var info = await CommentChapters.FetchAsync(url, o, tools, false, preview: true, cancellation: searchCancellation.Token);
            using var dialog = new FormatDialog(info);
            if (dialog.ShowDialog(this) == DialogResult.OK && dialog.SelectedFormat is { Length: > 0 } format) { Current.CustomFormat = format; OptionsChanged(); if (categories.SelectedItem is string category) options.ShowCategory(Current, category); }
        } catch (OperationCanceledException) { footer.Text = "Analysis cancelled."; } catch (Exception error) { Error(error); }
        finally { searchCancellation.Dispose(); searchCancellation = null; cancelSearch.Visible = false; UpdateButtons(); }
    }
    async Task FindCommentsAsync() {
        if (Links.FirstOrDefault() is not { } url || searchCancellation != null) return;
        var o = Current.Clone(); searchCancellation = new(); cancelSearch.Visible = true; UpdateButtons(); footer.Text = "Searching the first video's top 200 comments…";
        try {
            var info = await CommentChapters.PreviewAsync(url, o, tools, searchCancellation.Token);
            using var dialog = new CommentDialog(info);
            if (dialog.ShowDialog(this) == DialogResult.OK && dialog.Selected is { } chosen) {
                selections[info.VideoId] = chosen.Id;
                if (Current.ChapterSource == "youtube") Current.ChapterSource = "comments";
                footer.Text = $"Selected {chosen.Chapters.Count} chapters from {chosen.Author} for {info.Title}.";
                categories.SelectedItem = "Chapters & SponsorBlock"; options.ShowCategory(Current, "Chapters & SponsorBlock"); OptionsChanged();
            }
        } catch (OperationCanceledException) { footer.Text = "Comment search cancelled."; } catch (Exception error) { Error(error); }
        finally { searchCancellation.Dispose(); searchCancellation = null; cancelSearch.Visible = false; UpdateButtons(); }
    }
    void ShowPresets() {
        var menu = new ContextMenuStrip();
        void Apply(Preset p) { store.Apply(p); OptionsChanged(); if (categories.SelectedItem is string category) options.ShowCategory(Current, category); }
        foreach (var preset in SettingsStore.BuiltIn) menu.Items.Add(preset.Name, null, (_, _) => Apply(preset));
        if (store.Settings.Presets.Count > 0) { menu.Items.Add(new ToolStripSeparator()); foreach (var preset in store.Settings.Presets) menu.Items.Add(preset.Name, null, (_, _) => Apply(preset)); }
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Save current as preset…", null, (_, _) => { var name = Prompt("Save preset", "Preset name"); if (!string.IsNullOrWhiteSpace(name)) store.SavePreset(name); });
        var delete = new ToolStripMenuItem("Delete saved preset"); foreach (var preset in store.Settings.Presets) delete.DropDownItems.Add(preset.Name, null, (_, _) => { store.Settings.Presets.Remove(preset); store.Save(); }); menu.Items.Add(delete);
        menu.Items.Add("Reset options", null, (_, _) => Apply(new("Default", new())));
        menu.Items.Add("Open settings folder", null, (_, _) => OpenFolder(AppPaths.DataDirectory));
        menu.Items.Add("Get app updates", null, (_, _) => Process.Start(new ProcessStartInfo("https://github.com/dustalexw/ytdlp-studio/releases/latest") { UseShellExecute = true }));
        menu.Closed += (_, _) => menu.Dispose(); menu.Show(Cursor.Position);
    }
    void RevealFiles() {
        if (SelectedJob() is not { } job) return;
        string? file = job.OutputFiles.FirstOrDefault(File.Exists);
        if (file != null) { var start = new ProcessStartInfo("explorer.exe") { UseShellExecute = false }; start.ArgumentList.Add("/select," + file); Process.Start(start); }
        else OpenFolder(job.Options.OutputDirectory);
    }
    void ShowLog() {
        if (SelectedJob() is not { } job) return;
        using var dialog = new Form { Text = job.Title + " — Log", Size = new Size(950, 600), StartPosition = FormStartPosition.CenterParent, Font = Font };
        var text = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font("Consolas", 9), Text = string.Join(Environment.NewLine, job.Log.ToArray()) }; dialog.Controls.Add(text);
        using var refresh = new System.Windows.Forms.Timer { Interval = 500 }; refresh.Tick += (_, _) => { if (job.Active) { text.Text = string.Join(Environment.NewLine, job.Log.ToArray()); text.SelectionStart = text.Text.Length; text.ScrollToCaret(); } }; refresh.Start(); dialog.ShowDialog(this);
    }
    void OpenFolder(string path) { try { Directory.CreateDirectory(path); Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); } catch (Exception error) { Error(error); } }
    void Error(Exception error) { if (!IsDisposed) { footer.Text = error.Message; MessageBox.Show(this, error.Message, "YT-DLP Studio", MessageBoxButtons.OK, MessageBoxIcon.Error); } }
    string? Prompt(string title, string label) {
        using var dialog = new Form { Text = title, Size = new Size(380, 170), FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false, Font = Font };
        var text = new TextBox { Left = 20, Top = 45, Width = 320 }; var ok = new Button { Text = "Save", DialogResult = DialogResult.OK, Left = 250, Top = 85, Width = 90 }; dialog.Controls.AddRange([new Label { Text = label, Left = 20, Top = 18, AutoSize = true }, text, ok]); dialog.AcceptButton = ok; return dialog.ShowDialog(this) == DialogResult.OK ? text.Text : null;
    }
    protected override void Dispose(bool disposing) { if (disposing) { timer.Dispose(); searchCancellation?.Cancel(); } base.Dispose(disposing); }
}
