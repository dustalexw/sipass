using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using YtdlpStudio.Core;
namespace YtdlpStudio.Linux;

internal static class Program {
    [STAThread]
    static int Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace();
}

/// State that outlives a window: settings, bundled tools and the download queue.
public sealed class AppSession {
    public SettingsStore Store { get; } = new();
    public ToolPaths Tools { get; } = ToolPaths.Bundled();
    public DownloadQueue Queue { get; }
    public Dictionary<string, string> Selections { get; } = new();
    public bool Ready { get; set; }
    public string ToolSummary { get; set; } = "";
    public AppSession() { Queue = new(Tools, FreedesktopTrash.Move); }
    public static string Version => typeof(AppSession).Assembly.GetName().Version is { } v ? $"{v.Major}.{v.Minor}.{v.Build}" : "";
}

/// Opens folders and files with the desktop's default handlers.
public static class Shell {
    public static void Open(string path) {
        var start = new ProcessStartInfo(OperatingSystem.IsMacOS() ? "open" : "xdg-open") { UseShellExecute = false };
        start.ArgumentList.Add(path); Process.Start(start)?.Dispose();
    }
    /// Highlights a file in the file manager (freedesktop FileManager1), falling back to opening its folder.
    public static void Reveal(string file) {
        try {
            var start = new ProcessStartInfo("dbus-send") { UseShellExecute = false, RedirectStandardError = true };
            foreach (var a in new[] { "--session", "--print-reply", "--dest=org.freedesktop.FileManager1", "--type=method_call", "/org/freedesktop/FileManager1",
                         "org.freedesktop.FileManager1.ShowItems", "array:string:" + new Uri(file).AbsoluteUri, "string:" }) start.ArgumentList.Add(a);
            using var process = Process.Start(start)!; process.WaitForExit(3000);
            if (process.ExitCode == 0) return;
        } catch (Exception) { }
        Open(Path.GetDirectoryName(file)!);
    }
}

/// CI check: opens the window in both appearances, saves a screenshot of each, then exits.
static class SmokeTest {
    public static void Run(IClassicDesktopStyleApplicationLifetime desktop, string[] args) {
        var output = args.SkipWhile(a => a != "--smoke-test").Skip(1).FirstOrDefault() is { } dir && !dir.StartsWith("--") ? dir : AppContext.BaseDirectory;
        var app = App.Instance; var window = new MainWindow(app.Session) { Width = 1280, Height = 820 };
        // The desktop lifetime shows MainWindow once initialization finishes.
        desktop.MainWindow = window;
        async void Capture() {
            try {
                app.Session.Tools.EnsureAvailable();
                foreach (var (appearance, file) in new[] { ("dark", "Linux-smoke.png"), ("light", "Linux-smoke-light.png") }) {
                    app.ApplyAppearance(appearance, save: false);
                    await Task.Delay(700);
                    var size = new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height);
                    using var bitmap = new RenderTargetBitmap(size);
                    bitmap.Render(window); bitmap.Save(Path.Combine(output, file));
                }
                desktop.Shutdown(0);
            } catch (Exception error) { File.WriteAllText(Path.Combine(output, "smoke-test-error.txt"), error.ToString()); desktop.Shutdown(1); }
        }
        window.Opened += (_, _) => DispatcherTimer.RunOnce(Capture, TimeSpan.FromMilliseconds(800));
    }
}
