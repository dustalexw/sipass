using System.Reflection;
using YtdlpStudio.Core;
namespace YtdlpStudio.Windows;

public sealed class OptionsPanel : UserControl {
    readonly FlowLayoutPanel fields = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(16) };
    readonly ToolTip tips = new() { AutoPopDelay = 20000 };
    readonly Action changed;
    DownloadOptions? shownOptions; string? shownCategory;
    public OptionsPanel(Action onChanged) {
        changed = onChanged; Dock = DockStyle.Fill; Controls.Add(fields); BackColor = SystemColors.Window;
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
        fields.Controls.Add(new Label { Text = category, AutoSize = true, UseMnemonic = false, Font = new Font("Segoe UI", 16, FontStyle.Bold), Margin = new Padding(0, 0, 0, D(14)) });
        foreach (var property in typeof(DownloadOptions).GetProperties()) {
            var setting = property.GetCustomAttribute<SettingAttribute>(); if (setting?.Category != category) continue;
            var row = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0, 0, 0, D(13)) };
            object? value = property.GetValue(options); Control control;
            void Set(object newValue) { property.SetValue(options, newValue); changed(); }
            if (property.PropertyType == typeof(bool)) {
                var check = new CheckBox { Text = setting.Label, Checked = (bool)value!, AutoSize = true, UseMnemonic = false, MaximumSize = new Size(width, 0), Tag = "wrap", AccessibleName = setting.Label };
                check.CheckedChanged += (_, _) => Set(check.Checked); control = check;
            } else {
                row.Controls.Add(new Label { Text = setting.Label, AutoSize = true, UseMnemonic = false, MaximumSize = new Size(width, 0), Margin = new Padding(0, 0, 0, D(5)) });
                if (property.PropertyType == typeof(string[]) && setting.Choices.Length > 0) {
                    var list = new CheckedListBox { Width = width, Tag = "stretch", IntegralHeight = false, CheckOnClick = true, BorderStyle = BorderStyle.FixedSingle, AccessibleName = setting.Label };
                    var selected = (string[])value!;
                    foreach (var item in ParseChoices(setting.Choices)) list.Items.Add(item, selected.Contains(item.Value));
                    list.Height = list.ItemHeight * list.Items.Count + D(6);
                    list.ItemCheck += (_, e) => {
                        var selectedValues = list.Items.Cast<Choice>().Where((_, i) => i == e.Index ? e.NewValue == CheckState.Checked : list.GetItemChecked(i)).Select(c => c.Value).ToArray(); Set(selectedValues);
                    }; control = list;
                } else if (setting.Choices.Length > 0) {
                    var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = width, Tag = "stretch", AccessibleName = setting.Label };
                    foreach (var item in ParseChoices(setting.Choices)) combo.Items.Add(item);
                    combo.SelectedItem = combo.Items.Cast<Choice>().FirstOrDefault(c => c.Value == value?.ToString());
                    if (combo.SelectedIndex < 0) combo.SelectedIndex = 0;
                    combo.SelectedIndexChanged += (_, _) => { if (combo.SelectedItem is Choice choice) Set(choice.Value); }; control = combo;
                } else if (property.PropertyType == typeof(int) || property.PropertyType == typeof(double)) {
                    var numeric = new NumericUpDown { Minimum = (decimal)setting.Min, Maximum = (decimal)setting.Max, DecimalPlaces = property.PropertyType == typeof(double) ? 1 : 0, Increment = property.PropertyType == typeof(double) ? 0.5M : 1, Width = D(160), AccessibleName = setting.Label };
                    numeric.Value = Math.Clamp(Convert.ToDecimal(value), numeric.Minimum, numeric.Maximum);
                    numeric.ValueChanged += (_, _) => Set(property.PropertyType == typeof(int) ? (object)(int)numeric.Value : (double)numeric.Value); control = numeric;
                } else {
                    var text = new TextBox { Text = value?.ToString() ?? "", Width = width, Tag = "stretch", AccessibleName = setting.Label };
                    if (property.Name == nameof(DownloadOptions.ExtraArgs)) { text.Multiline = true; text.Height = text.Font.Height * 4 + D(8); text.ScrollBars = ScrollBars.Vertical; }
                    text.TextChanged += (_, _) => Set(text.Text); control = text;
                    if (property.Name == nameof(DownloadOptions.OutputDirectory)) {
                        row.Controls.Add(text); var browse = new Button { Text = "Choose folder…", AutoSize = true };
                        browse.Click += (_, _) => { using var picker = new FolderBrowserDialog { InitialDirectory = text.Text, ShowNewFolderButton = true }; if (picker.ShowDialog(this) == DialogResult.OK) text.Text = picker.SelectedPath; }; row.Controls.Add(browse); control = browse;
                    }
                }
            }
            if (!row.Controls.Contains(control)) row.Controls.Add(control);
            if (setting.Hint.Length > 0) {
                tips.SetToolTip(control, setting.Hint);
                row.Controls.Add(new Label { Text = setting.Hint, MaximumSize = new Size(width, 0), AutoSize = true, UseMnemonic = false, ForeColor = SystemColors.GrayText, Font = new Font("Segoe UI", 9), Margin = new Padding(0, D(5), 0, 0) });
            }
            fields.Controls.Add(row);
        }
        fields.ResumeLayout();
    }
    public sealed record Choice(string Value, string Label) { public override string ToString() => Label; }
    public static IEnumerable<Choice> ParseChoices(string text) => text.Split('|').Select(s => { var parts = s.Split('=', 2); return new Choice(parts[0], parts.Length > 1 ? parts[1] : parts[0]); });
    protected override void Dispose(bool disposing) { if (disposing) tips.Dispose(); base.Dispose(disposing); }
}
