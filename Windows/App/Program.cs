using YtdlpStudio.Core;
namespace YtdlpStudio.Windows;

internal static class Program {
    [STAThread]
    static void Main(string[] args) {
        ApplicationConfiguration.Initialize();
        if (args.Contains("--smoke-test")) {
            try {
                ToolPaths.Bundled().EnsureAvailable();
                using var form = new MainForm();
                form.Show();
                form.Size = new Size(1280, 820);
                form.PerformLayout();
                Application.DoEvents();
                using var bitmap = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                bitmap.Save(Path.Combine(AppContext.BaseDirectory, "Windows-smoke.png"), System.Drawing.Imaging.ImageFormat.Png);
                form.Hide();
                using var image = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (image == null) throw new InvalidOperationException("Application icon missing.");
                Environment.ExitCode = 0;
            } catch (Exception error) { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "smoke-test-error.txt"), error.ToString()); Environment.ExitCode = 1; }
            return;
        }
        Application.ThreadException += (_, e) => MessageBox.Show(e.Exception.Message, "YT-DLP Studio", MessageBoxButtons.OK, MessageBoxIcon.Error);
        Application.Run(new MainForm());
    }
}
