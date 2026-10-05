using System.Text.Json.Nodes;
using YtdlpStudio.Core;
namespace YtdlpStudio.Windows;

public sealed class CommentDialog : Form {
    sealed record Item(CommentCandidate Candidate) { public override string ToString() => $"{Candidate.Author} · {Candidate.Chapters.Count} chapters · {Candidate.Likes} likes"; }
    readonly ListBox lists = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    readonly TextBox preview = new() { Dock = DockStyle.Fill, ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Segoe UI", 11) };
    public CommentCandidate? Selected => (lists.SelectedItem as Item)?.Candidate;
    public CommentDialog(CommentPreview info) {
        SuspendLayout(); AutoScaleDimensions = new SizeF(96F, 96F); AutoScaleMode = AutoScaleMode.Dpi;
        Text = "Choose comment chapters — " + info.Title; Size = new Size(950, 650); MinimumSize = new Size(750, 500); StartPosition = FormStartPosition.CenterParent; Font = new Font("Segoe UI", 10);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Padding = new Padding(16) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 285)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var title = new Label { Text = info.Title + "\nThis selection applies only to this video.", Dock = DockStyle.Fill, AutoSize = true, UseMnemonic = false, Margin = new Padding(3, 0, 3, 10) }; layout.Controls.Add(title, 0, 0); layout.SetColumnSpan(title, 2);
        foreach (var candidate in info.Candidates) lists.Items.Add(new Item(candidate));
        lists.SelectedIndexChanged += (_, _) => {
            if (Selected is { } selected) preview.Text = string.Join(Environment.NewLine, selected.Chapters.Select(c => Time(c.Start).PadLeft(8) + "   " + c.Title)) + Environment.NewLine + Environment.NewLine + "Original comment" + Environment.NewLine + selected.Text;
        };
        layout.Controls.Add(lists, 0, 1); layout.Controls.Add(preview, 1, 1);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        var use = new Button { Text = "Use these chapters", AutoSize = true, DialogResult = DialogResult.OK }; var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel }; actions.Controls.AddRange([use, cancel]); layout.Controls.Add(actions, 0, 2); layout.SetColumnSpan(actions, 2);
        Controls.Add(layout); AcceptButton = use; CancelButton = cancel; ResumeLayout(false); PerformLayout(); if (lists.Items.Count > 0) lists.SelectedIndex = 0;
    }
    public static string Time(double seconds) { var t = TimeSpan.FromSeconds(seconds); return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{(int)t.TotalMinutes}:{t.Seconds:00}"; }
}
public sealed class FormatDialog : Form {
    readonly DataGridView table = new() { AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize, Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
    readonly Label summary = new() { AutoSize = true, UseMnemonic = false, Anchor = AnchorStyles.Left, TextAlign = ContentAlignment.MiddleLeft };
    readonly Button use = new() { Text = "Use selected format", AutoSize = true, DialogResult = DialogResult.OK, Enabled = false };
    public string? SelectedFormat { get; private set; }
    public FormatDialog(JsonObject info) {
        SuspendLayout(); AutoScaleDimensions = new SizeF(96F, 96F); AutoScaleMode = AutoScaleMode.Dpi;
        Text = "Analyze — " + CommentChapters.Text(info["title"]); Size = new Size(1100, 700); MinimumSize = new Size(850, 500); StartPosition = FormStartPosition.CenterParent; Font = new Font("Segoe UI", 10);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, Padding = new Padding(16) };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 130)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label { Text = CommentChapters.Text(info["title"]) + " · " + CommentChapters.Text(info["uploader"]) + " · " + CommentDialog.Time(CommentChapters.Number(info["duration"])), Dock = DockStyle.Fill, AutoSize = true, UseMnemonic = false, Margin = new Padding(3, 0, 3, 10) }, 0, 0);
        foreach (var column in new[] { "ID", "Type", "Ext", "Resolution", "FPS", "Video codec", "Audio codec", "Bitrate", "Size" }) table.Columns.Add(column, column);
        if (info["entries"] is JsonArray entries) {
            table.Columns.Clear(); table.Columns.Add("Item", "Playlist item"); foreach (var entry in entries.OfType<JsonObject>()) table.Rows.Add(CommentChapters.Text(entry["title"]));
            summary.Text = $"Playlist with {entries.Count} entries. Use Playlist settings to select a range; formats are chosen per video.";
        } else {
            var formats = (info["formats"] as JsonArray ?? []).OfType<JsonObject>().Where(f => CommentChapters.Text(f["vcodec"]) != "none" || CommentChapters.Text(f["acodec"]) != "none").Reverse();
            foreach (var format in formats) {
                bool video = CommentChapters.Text(format["vcodec"]) is not ("" or "none"); bool audio = CommentChapters.Text(format["acodec"]) is not ("" or "none");
                int index = table.Rows.Add(CommentChapters.Text(format["format_id"]), video && audio ? "A+V" : video ? "Video" : "Audio", CommentChapters.Text(format["ext"]), CommentChapters.Text(format["resolution"]), CommentChapters.Text(format["fps"]), CommentChapters.Text(format["vcodec"]), CommentChapters.Text(format["acodec"]), CommentChapters.Text(format["tbr"]), SizeText(CommentChapters.Number(format["filesize"] ?? format["filesize_approx"])));
                table.Rows[index].Tag = format;
            }
            table.ClearSelection();
            table.SelectionChanged += (_, _) => {
                var selected = table.SelectedRows.Cast<DataGridViewRow>().Select(r => r.Tag as JsonObject).Where(f => f != null).Cast<JsonObject>().ToArray();
                bool HasVideo(JsonObject f) => CommentChapters.Text(f["vcodec"]) is not ("" or "none");
                bool HasAudio(JsonObject f) => CommentChapters.Text(f["acodec"]) is not ("" or "none");
                var video = selected.Where(HasVideo).ToArray(); var audio = selected.Where(f => !HasVideo(f) && HasAudio(f)).ToArray();
                bool valid = selected.Length is 1 or 2 && video.Length <= 1 && audio.Length <= 1 && !(video.Length == 1 && HasAudio(video[0]) && audio.Length > 0);
                SelectedFormat = valid ? string.Join('+', video.Concat(audio).Select(f => CommentChapters.Text(f["format_id"]))) : null;
                use.Enabled = !string.IsNullOrEmpty(SelectedFormat);
                summary.Text = valid ? "Selected format: " + SelectedFormat : "Select one video and one audio stream, or one combined format.";
            };
            summary.Text = "Select one video and one audio stream, or one combined format.";
        }
        layout.Controls.Add(table, 0, 1);
        var details = new TextBox { ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Text = info["chapters"] is JsonArray chapters ? string.Join(Environment.NewLine, chapters.OfType<JsonObject>().Select(c => CommentDialog.Time(CommentChapters.Number(c["start_time"])) + "   " + CommentChapters.Text(c["title"]))) : "No existing chapters" }; layout.Controls.Add(details, 0, 2);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink }; var close = new Button { Text = "Close", AutoSize = true, DialogResult = DialogResult.Cancel }; actions.Controls.AddRange([use, close, summary]); layout.Controls.Add(actions, 0, 3); Controls.Add(layout); AcceptButton = use; CancelButton = close; ResumeLayout(false); PerformLayout();
    }
    static string SizeText(double bytes) => bytes <= 0 ? "" : bytes >= 1e9 ? $"{bytes / 1e9:0.0} GB" : $"{bytes / 1e6:0.0} MB";
}
