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
    readonly ListBox categories = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, IntegralHeight = false };
    readonly OptionsPanel options;
    readonly DataGridView grid = new() { Dock = DockStyle.Fill, AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoGenerateColumns = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, RowHeadersVisible = false, BackgroundColor = SystemColors.Window, BorderStyle = BorderStyle.None };
    readonly Label footer = new() { Dock = DockStyle.Fill, AutoSize = true, UseMnemonic = false, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, ForeColor = SystemColors.GrayText };
    readonly Label commandNote = new() { AutoSize = true, UseMnemonic = false, ForeColor = SystemColors.GrayText };
    readonly Button analyze, findComments, cancelSearch, download;
    readonly System.Windows.Forms.Timer timer = new() { Interval = 250 };
    readonly Dictionary<string, string> selections = new();
    CancellationTokenSource? searchCancellation;
    bool settingsDirty, closing, ready;
    DateTime lastEdit;
    public DownloadOptions Current => store.Settings.Options;

    SplitContainer left = null!;
    public MainForm() {
        // Layout values below are written for 96 DPI; AutoScaleMode.Dpi scales them to the monitor (and on monitor changes).
        SuspendLayout(); AutoScaleDimensions = new SizeF(96F, 96F); AutoScaleMode = AutoScaleMode.Dpi;
        queue = new(tools, path => Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(path, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin));
        options = new OptionsPanel(OptionsChanged);
        Text = "YT-DLP Studio 2.1 — Windows"; Font = new Font("Segoe UI", 10); Size = new Size(1280, 820); MinimumSize = new Size(900, 600); StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(12) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 165)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 120)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); Controls.Add(root);
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 315));
        header.RowStyles.Add(new RowStyle(SizeType.AutoSize)); header.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var title = new Label { Text = "YT-DLP Studio", AutoSize = true, Font = new Font("Segoe UI", 18, FontStyle.Bold) }; header.Controls.Add(title, 0, 0);
        header.Controls.Add(urls, 0, 1);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(8, 0, 0, 0) };
        download = Button("Download", StartDownload); download.Width = 290; download.Height = 34;
        analyze = Button("Analyze formats…", () => _ = AnalyzeAsync()); analyze.Width = 290;
        findComments = Button("Find comment chapters…", () => _ = FindCommentsAsync()); findComments.Width = 290;
        actions.Controls.AddRange([download, analyze, findComments]); header.Controls.Add(actions, 1, 1); header.SetRowSpan(actions, 2);
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        toolbar.Controls.Add(Button("Paste links", () => { if (Clipboard.ContainsText()) urls.Text = urls.Text.Trim().Length == 0 ? Clipboard.GetText().Trim() : urls.Text.Trim() + Environment.NewLine + Clipboard.GetText().Trim(); }));
        toolbar.Controls.Add(Button("Presets ▾", ShowPresets));
        toolbar.Controls.Add(Button("Open download folder", () => OpenFolder(Current.OutputDirectory)));
        cancelSearch = Button("Cancel search", () => searchCancellation?.Cancel()); cancelSearch.Visible = false; toolbar.Controls.Add(cancelSearch);
        header.Controls.Add(toolbar, 0, 2); root.Controls.Add(header, 0, 0);
        var split = new SplitContainer { Dock = DockStyle.Fill, Size = new Size(1230, 500), SplitterDistance = 720, Panel1MinSize = 480, Panel2MinSize = 300 };
        left = new SplitContainer { Dock = DockStyle.Fill, Size = new Size(715, 500), SplitterDistance = 190, Panel1MinSize = 140, Panel2MinSize = 320, FixedPanel = FixedPanel.Panel1 };
        left.Panel1.Controls.Add(categories); left.Panel2.Controls.Add(options); split.Panel1.Controls.Add(left);
        foreach (var category in typeof(DownloadOptions).GetProperties().Select(p => p.GetCustomAttribute<SettingAttribute>()?.Category).Where(c => c != null).Distinct()) categories.Items.Add(category!);
        categories.SelectedIndexChanged += (_, _) => { if (categories.SelectedItem is string category) options.ShowCategory(Current, category); };
        var queuePanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
        queuePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); queuePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); queuePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        queuePanel.Controls.Add(new Label { Text = "Download queue", AutoSize = true, Font = new Font("Segoe UI", 12, FontStyle.Bold) }, 0, 0);
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Title", HeaderText = "Video", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 100, MinimumWidth = 80 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 55, MinimumWidth = 60 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Progress", HeaderText = "Progress", AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Speed", HeaderText = "Speed", AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });
        grid.CellDoubleClick += (_, _) => ShowLog(); queuePanel.Controls.Add(grid, 0, 1);
        var queueActions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        queueActions.Controls.AddRange([Button("Cancel", () => { if (SelectedJob() is { } j) queue.Cancel(j); }), Button("Retry", () => { if (SelectedJob() is { } j) queue.Retry(j); }), Button("Show files", RevealFiles), Button("View log", ShowLog), Button("Clear finished", () => queue.ClearFinished()), Button("Cancel all", () => queue.CancelAll())]);
        queuePanel.Controls.Add(queueActions, 0, 2); split.Panel2.Controls.Add(queuePanel); root.Controls.Add(split, 0, 1);
        var preview = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 2, Padding = new Padding(0, 8, 0, 0) };
        preview.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); preview.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); preview.RowStyles.Add(new RowStyle(SizeType.AutoSize)); preview.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        preview.Controls.Add(commandNote, 0, 0); preview.SetColumnSpan(commandNote, 2); preview.Controls.Add(command, 0, 1);
        preview.Controls.Add(Button("Copy command", () => { if (command.Text.Length > 0) Clipboard.SetText(command.Text); }), 1, 1); root.Controls.Add(preview, 0, 2); root.Controls.Add(footer, 0, 3);
        urls.TextChanged += (_, _) => UpdateCommand(); timer.Tick += (_, _) => Tick(); timer.Start(); UpdateCommand();
        ResumeLayout(false); PerformLayout();
        // Build the options after DPI scaling so OptionsPanel's own DPI-aware sizes are not scaled twice.
        categories.SelectedIndex = 0;
        Load += (_, _) => { FitToScreen(); SizeCategoryList(); };
        DpiChanged += (_, _) => BeginInvoke(SizeCategoryList);
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
    void FitToScreen() {
        var area = Screen.FromControl(this).WorkingArea;
        if (Width > area.Width || Height > area.Height) { Bounds = area; WindowState = FormWindowState.Maximized; }
    }
    void SizeCategoryList() {
        int widest = categories.Items.Cast<string>().Select(c => TextRenderer.MeasureText(c, categories.Font).Width).DefaultIfEmpty(0).Max();
        int wanted = widest + SystemInformation.VerticalScrollBarWidth + LogicalToDeviceUnits(16);
        int max = left.Width - left.Panel2MinSize - left.SplitterWidth;
        if (max > left.Panel1MinSize) left.SplitterDistance = Math.Clamp(wanted, left.Panel1MinSize, max);
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
        using var dialog = new Form { Text = job.Title + " — Log", Size = LogicalToDeviceUnits(new Size(950, 600)), StartPosition = FormStartPosition.CenterParent, Font = Font };
        var text = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font("Consolas", 9), Text = string.Join(Environment.NewLine, job.Log.ToArray()) }; dialog.Controls.Add(text);
        using var refresh = new System.Windows.Forms.Timer { Interval = 500 }; refresh.Tick += (_, _) => { if (job.Active) { text.Text = string.Join(Environment.NewLine, job.Log.ToArray()); text.SelectionStart = text.Text.Length; text.ScrollToCaret(); } }; refresh.Start(); dialog.ShowDialog(this);
    }
    void OpenFolder(string path) { try { Directory.CreateDirectory(path); Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); } catch (Exception error) { Error(error); } }
    void Error(Exception error) { if (!IsDisposed) { footer.Text = error.Message; MessageBox.Show(this, error.Message, "YT-DLP Studio", MessageBoxButtons.OK, MessageBoxIcon.Error); } }
    string? Prompt(string title, string label) {
        using var dialog = new Form { Text = title, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false, Font = Font };
        var layout = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, Padding = new Padding(LogicalToDeviceUnits(16)) };
        var text = new TextBox { Width = LogicalToDeviceUnits(320) };
        var ok = new Button { Text = "Save", DialogResult = DialogResult.OK, AutoSize = true, Anchor = AnchorStyles.Right };
        layout.Controls.Add(new Label { Text = label, AutoSize = true, UseMnemonic = false }); layout.Controls.Add(text); layout.Controls.Add(ok);
        dialog.Controls.Add(layout); dialog.AcceptButton = ok; return dialog.ShowDialog(this) == DialogResult.OK ? text.Text : null;
    }
    protected override bool ProcessCmdKey(ref Message message, Keys keys) {
        if (keys == (Keys.Control | Keys.Enter)) { if (download.Enabled) StartDownload(); return true; }
        if (keys == (Keys.Control | Keys.I)) { if (analyze.Enabled) _ = AnalyzeAsync(); return true; }
        if (keys == (Keys.Control | Keys.L)) { urls.Focus(); urls.SelectAll(); return true; }
        return base.ProcessCmdKey(ref message, keys);
    }
    protected override void Dispose(bool disposing) { if (disposing) { timer.Dispose(); searchCancellation?.Cancel(); } base.Dispose(disposing); }
}
