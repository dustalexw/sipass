using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using YtdlpStudio.Core;
namespace YtdlpStudio.Linux;

/// Builds the controls for one option category from the [Setting] attributes, like the Windows OptionsPanel.
public sealed class OptionsView : ScrollViewer {
    readonly Window owner;
    readonly Action changed;
    public OptionsView(Window owner, Action onChanged) { this.owner = owner; changed = onChanged; HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled; }

    public void Show(DownloadOptions options, string category) {
        const double width = 440;
        var fields = new StackPanel { Spacing = 16, Margin = new Thickness(22, 16, 22, 22) };
        var (icon, colors) = Look.Category(category);
        fields.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 0, 0, 4),
            Children = { Look.Tile(icon, colors, 32), new TextBlock { Text = category, FontSize = 21, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center } } });
        foreach (var property in typeof(DownloadOptions).GetProperties()) {
            var setting = property.GetCustomAttribute<SettingAttribute>(); if (setting?.Category != category) continue;
            var row = new StackPanel { Spacing = 6, MaxWidth = width, HorizontalAlignment = HorizontalAlignment.Left };
            object? value = property.GetValue(options);
            void Set(object newValue) { property.SetValue(options, newValue); changed(); }
            // The Windows labels mention the Recycle Bin; on Linux the file goes to the desktop trash.
            string label = setting.Label.Replace("Recycle Bin", "Trash");
            if (property.PropertyType == typeof(bool)) {
                var check = new CheckBox { Content = new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap }, IsChecked = (bool)value! };
                check.IsCheckedChanged += (_, _) => Set(check.IsChecked == true); row.Children.Add(check);
            } else {
                row.Children.Add(new TextBlock { Text = label, FontWeight = FontWeight.SemiBold, FontSize = 13, TextWrapping = TextWrapping.Wrap });
                if (property.PropertyType == typeof(string[]) && setting.Choices.Length > 0) {
                    var selected = ((string[])value!).ToHashSet();
                    var list = new StackPanel { Spacing = 2 };
                    foreach (var choice in ParseChoices(setting.Choices)) {
                        var item = new CheckBox { Content = choice.Label, IsChecked = selected.Contains(choice.Value), Tag = choice.Value };
                        item.IsCheckedChanged += (_, _) => Set(list.Children.OfType<CheckBox>().Where(c => c.IsChecked == true).Select(c => (string)c.Tag!).ToArray());
                        list.Children.Add(item);
                    }
                    row.Children.Add(new Border { Child = list, Background = Look.B(Look.P.Field), BorderBrush = Look.B(Look.P.Hairline), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 6) });
                } else if (setting.Choices.Length > 0) {
                    var choices = ParseChoices(setting.Choices).ToArray();
                    var combo = new ComboBox { ItemsSource = choices, Width = width, SelectedItem = choices.FirstOrDefault(c => c.Value == value?.ToString()) ?? choices[0] };
                    combo.SelectionChanged += (_, _) => { if (combo.SelectedItem is Choice c) Set(c.Value); }; row.Children.Add(combo);
                } else if (property.PropertyType == typeof(int) || property.PropertyType == typeof(double)) {
                    bool isDouble = property.PropertyType == typeof(double);
                    var numeric = new NumericUpDown { Minimum = (decimal)setting.Min, Maximum = (decimal)setting.Max, Increment = isDouble ? 0.5m : 1, FormatString = isDouble ? "0.0" : "0", Width = 170, HorizontalAlignment = HorizontalAlignment.Left,
                        Value = Math.Clamp(Convert.ToDecimal(value), (decimal)setting.Min, (decimal)setting.Max) };
                    numeric.ValueChanged += (_, _) => { if (numeric.Value is { } v) Set(isDouble ? (object)(double)v : (int)v); }; row.Children.Add(numeric);
                } else {
                    var text = new TextBox { Text = value?.ToString() ?? "", Width = width };
                    if (property.Name == nameof(DownloadOptions.ExtraArgs)) { text.AcceptsReturn = true; text.TextWrapping = TextWrapping.Wrap; text.Height = 96; }
                    text.TextChanged += (_, _) => Set(text.Text ?? ""); row.Children.Add(text);
                    if (property.Name == nameof(DownloadOptions.OutputDirectory)) {
                        var browse = new Button { Classes = { "chip" }, Content = Icons.WithText(Icons.Folder, "Choose folder…", Brushes.Transparent, 15), HorizontalAlignment = HorizontalAlignment.Left };
                        browse.Click += async (_, _) => {
                            var start = Directory.Exists(text.Text) ? await owner.StorageProvider.TryGetFolderFromPathAsync(text.Text!) : null;
                            var picked = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Download folder", SuggestedStartLocation = start });
                            if (picked.FirstOrDefault()?.TryGetLocalPath() is { } path) text.Text = path;
                        };
                        row.Children.Add(browse);
                    }
                }
            }
            if (setting.Hint.Length > 0) {
                var hint = setting.Hint.Replace("Windows backslashes are preserved", "backslashes are kept as typed");
                row.Children.Add(new TextBlock { Text = hint, Classes = { "secondary" }, FontSize = 12, TextWrapping = TextWrapping.Wrap });
                ToolTip.SetTip(row, hint);
            }
            fields.Children.Add(row);
        }
        Content = fields; Offset = default;
    }
    public sealed record Choice(string Value, string Label) { public override string ToString() => Label; }
    public static IEnumerable<Choice> ParseChoices(string text) => text.Split('|').Select(s => { var parts = s.Split('=', 2); return new Choice(parts[0], parts.Length > 1 ? parts[1] : parts[0]); });
}
