using System.Reflection;
using YtdlpStudio.Core;
namespace YtdlpStudio.Windows;

public sealed class OptionsPanel : UserControl {
    readonly FlowLayoutPanel fields = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(16) };
    readonly ToolTip tips = new() { AutoPopDelay = 20000 };
    readonly Action changed;
    DownloadOptions? shownOptions; string? shownCategory;
    public OptionsPanel(Action onChanged) {
        changed = onChanged; Dock = DockStyle.Fill; Controls.Add(fields); BackColor = Theme.P.Panel; fields.BackColor = Theme.P.Panel; ForeColor = Theme.P.Text;
        fields.ClientSizeChanged += (_, _) => FitWidths();
        // Fields are sized in device pixels for the current DPI; rebuild them when the window moves to a monitor with a different scale.
        DpiChangedAfterParent += (_, _) => { if (shownOptions != null && shownCategory != null) BeginInvoke(() => ShowCategory(shownOptions, shownCategory)); };
    }
    int D(int logical) => LogicalToDeviceUnits(logical);
    int FieldWidth => Math.Max(D(160), Math.Min(D(430), fields.ClientSize.Width - fields.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - D(12)));
    void FitWidths() {
        int width = FieldWidth; fields.SuspendLayout();
        foreach (Control row in fields.Controls) foreach (Control child in row.Controls) {
            if (child.Tag as string == "stretch") child.Width = width;
            else if (child.Tag as string == "wrap" || child is Label) child.MaximumSize = new Size(width, 0);
        }
        fields.ResumeLayout();
    }
    public void ShowCategory(DownloadOptions options, string category) {
        shownOptions = options; shownCategory = category; fields.Padding = new Padding(D(16)); int width = FieldWidth;
        fields.SuspendLayout(); foreach (Control field in fields.Controls.Cast<Control>().ToArray()) field.Dispose(); fields.Controls.Clear();
        var (glyph, colors) = Theme.Category(category);
        var heading = new Label { Text = category, AutoSize = true, UseMnemonic = false, Font = new Font("Segoe UI Semibold", 15), ForeColor = Theme.P.Text, Padding = new Padding(D(40), 0, 0, 0), MinimumSize = new Size(0, D(32)), TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 0, 0, D(16)) };
        heading.Paint += (_, e) => { Theme.Smooth(e.Graphics); float s = DeviceDpi / 96f; Tiles.Draw(e.Graphics, new RectangleF(0, (heading.Height - 30 * s) / 2, 30 * s, 30 * s), glyph, colors, s); };
        fields.Controls.Add(heading);
        foreach (var property in typeof(DownloadOptions).GetProperties()) {
            var setting = property.GetCustomAttribute<SettingAttribute>(); if (setting?.Category != category) continue;
            var row = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0, 0, 0, D(13)) };
            object? value = property.GetValue(options); Control control;
            void Set(object newValue) { property.SetValue(options, newValue); changed(); }
            if (property.PropertyType == typeof(bool)) {
                var check = new CheckBox { Text = setting.Label, Checked = (bool)value!, AutoSize = true, UseMnemonic = false, MaximumSize = new Size(width, 0), Tag = "wrap", AccessibleName = setting.Label, ForeColor = Theme.P.Text };
                check.CheckedChanged += (_, _) => Set(check.Checked); control = check;
            } else {
                row.Controls.Add(new Label { Text = setting.Label, AutoSize = true, UseMnemonic = false, MaximumSize = new Size(width, 0), Margin = new Padding(0, 0, 0, D(5)), ForeColor = Theme.P.Text, Font = new Font("Segoe UI Semibold", 9.5f) });
                if (property.PropertyType == typeof(string[]) && setting.Choices.Length > 0) {
                    var list = new CheckedListBox { Width = width, Tag = "stretch", IntegralHeight = false, CheckOnClick = true, BorderStyle = BorderStyle.FixedSingle, AccessibleName = setting.Label, BackColor = Theme.P.Field, ForeColor = Theme.P.Text };
                    var selected = (string[])value!;
                    foreach (var item in ParseChoices(setting.Choices)) list.Items.Add(item, selected.Contains(item.Value));
                    list.Height = list.ItemHeight * list.Items.Count + D(6);
                    list.ItemCheck += (_, e) => {
                        var selectedValues = list.Items.Cast<Choice>().Where((_, i) => i == e.Index ? e.NewValue == CheckState.Checked : list.GetItemChecked(i)).Select(c => c.Value).ToArray(); Set(selectedValues);
                    }; control = list;
                } else if (setting.Choices.Length > 0) {
                    var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = width, Tag = "stretch", AccessibleName = setting.Label, BackColor = Theme.P.Field, ForeColor = Theme.P.Text };
                    foreach (var item in ParseChoices(setting.Choices)) combo.Items.Add(item);
                    combo.SelectedItem = combo.Items.Cast<Choice>().FirstOrDefault(c => c.Value == value?.ToString());
                    if (combo.SelectedIndex < 0) combo.SelectedIndex = 0;
                    combo.SelectedIndexChanged += (_, _) => { if (combo.SelectedItem is Choice choice) Set(choice.Value); }; control = combo;
                } else if (property.PropertyType == typeof(int) || property.PropertyType == typeof(double)) {
                    var numeric = new NumericUpDown { Minimum = (decimal)setting.Min, Maximum = (decimal)setting.Max, DecimalPlaces = property.PropertyType == typeof(double) ? 1 : 0, Increment = property.PropertyType == typeof(double) ? 0.5M : 1, Width = D(160), AccessibleName = setting.Label, BackColor = Theme.P.Field, ForeColor = Theme.P.Text, BorderStyle = BorderStyle.FixedSingle };
                    numeric.Value = Math.Clamp(Convert.ToDecimal(value), numeric.Minimum, numeric.Maximum);
                    numeric.ValueChanged += (_, _) => Set(property.PropertyType == typeof(int) ? (object)(int)numeric.Value : (double)numeric.Value); control = numeric;
                } else {
                    var text = new TextBox { Text = value?.ToString() ?? "", Width = width, Tag = "stretch", AccessibleName = setting.Label, BackColor = Theme.P.Field, ForeColor = Theme.P.Text, BorderStyle = BorderStyle.FixedSingle };
                    if (property.Name == nameof(DownloadOptions.ExtraArgs)) { text.Multiline = true; text.Height = text.Font.Height * 4 + D(8); text.ScrollBars = ScrollBars.Vertical; }
                    text.TextChanged += (_, _) => Set(text.Text); control = text;
                    if (property.Name == nameof(DownloadOptions.OutputDirectory)) {
                        row.Controls.Add(text); var browse = new Button { Text = "Choose folder…", AutoSize = true, FlatStyle = FlatStyle.Flat, BackColor = Theme.P.Chip, ForeColor = Theme.P.Text, Margin = new Padding(0, D(6), 0, 0) };
                        browse.FlatAppearance.BorderColor = Theme.P.Hairline; browse.FlatAppearance.MouseOverBackColor = Theme.P.Hover;
                        browse.Click += (_, _) => { using var picker = new FolderBrowserDialog { InitialDirectory = text.Text, ShowNewFolderButton = true }; if (picker.ShowDialog(this) == DialogResult.OK) text.Text = picker.SelectedPath; }; row.Controls.Add(browse); control = browse;
                    }
                }
            }
            if (!row.Controls.Contains(control)) row.Controls.Add(control);
            if (setting.Hint.Length > 0) {
                tips.SetToolTip(control, setting.Hint);
                row.Controls.Add(new Label { Text = setting.Hint, MaximumSize = new Size(width, 0), AutoSize = true, UseMnemonic = false, ForeColor = Theme.P.Secondary, Font = new Font("Segoe UI", 9), Margin = new Padding(0, D(5), 0, 0) });
            }
            fields.Controls.Add(row);
        }
        fields.ResumeLayout();
    }
    public sealed record Choice(string Value, string Label) { public override string ToString() => Label; }
    public static IEnumerable<Choice> ParseChoices(string text) => text.Split('|').Select(s => { var parts = s.Split('=', 2); return new Choice(parts[0], parts.Length > 1 ? parts[1] : parts[0]); });
    protected override void Dispose(bool disposing) { if (disposing) tips.Dispose(); base.Dispose(disposing); }
}
