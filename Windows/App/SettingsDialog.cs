using System.Diagnostics;
namespace YtdlpStudio.Windows;

/// Settings window: appearance (System, Light, Dark) and where the app keeps its data.
public sealed class SettingsDialog : Form {
    public string? ChosenAppearance { get; private set; }
    public SettingsDialog(string appearance, string toolSummary, Action openDataFolder) {
        SuspendLayout(); AutoScaleDimensions = new SizeF(96F, 96F); AutoScaleMode = AutoScaleMode.Dpi;
        Text = "Settings"; Font = new Font("Segoe UI", 10); FormBorderStyle = FormBorderStyle.FixedDialog; MinimizeBox = false; MaximizeBox = false; ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent; AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink; KeyPreview = true;
        var layout = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, Padding = new Padding(22, 18, 22, 18) };
        Label Heading(string text) => new() { Text = text, AutoSize = true, Font = new Font("Segoe UI Semibold", 11.5f), Margin = new Padding(0, 4, 0, 10) };
        layout.Controls.Add(Heading("Appearance"));
        var cards = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 6) };
        foreach (var (value, label, glyph) in new[] { ("system", "System", Theme.Glyph.Monitor), ("light", "Light", Theme.Glyph.Sun), ("dark", "Dark", Theme.Glyph.Moon) }) {
            var card = new AppearanceCard(value, label, glyph) { Selected = value == appearance };
            card.Click += (_, _) => { if (value == appearance) return; ChosenAppearance = value; DialogResult = DialogResult.OK; };
            cards.Controls.Add(card);
        }
        layout.Controls.Add(cards);
        layout.Controls.Add(new Label { Text = "System follows the Windows light or dark setting.", AutoSize = true, Tag = "secondary", Margin = new Padding(0, 0, 0, 18) });
        layout.Controls.Add(Heading("About"));
        layout.Controls.Add(new Label { Text = toolSummary.Length > 0 ? toolSummary : "Checking bundled tools…", AutoSize = true, MaximumSize = new Size(540, 0), Tag = "secondary", Margin = new Padding(0, 0, 0, 12) });
        var links = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 14) };
        var data = new Button { Text = "Open settings folder", AutoSize = true, Margin = new Padding(0, 0, 8, 0) }; data.Click += (_, _) => openDataFolder();
        var updates = new Button { Text = "Get app updates", AutoSize = true };
        updates.Click += (_, _) => Process.Start(new ProcessStartInfo("https://github.com/dustalexw/sipass/releases/latest") { UseShellExecute = true });
        links.Controls.AddRange([data, updates]); layout.Controls.Add(links);
        var done = new Button { Text = "Done", AutoSize = true, DialogResult = DialogResult.Cancel, Anchor = AnchorStyles.Right, MinimumSize = new Size(90, 0) };
        layout.Controls.Add(done); AcceptButton = done; CancelButton = done;
        Controls.Add(layout); Theme.StyleDialog(this); ResumeLayout(false); PerformLayout();
    }
}
