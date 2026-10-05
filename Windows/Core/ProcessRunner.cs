using System.Diagnostics;
using System.Text;
namespace YtdlpStudio.Core;

public sealed record ToolPaths(string Ytdlp, string Ffmpeg, string Ffprobe, string Deno) {
    public static ToolPaths Bundled(string? baseDirectory = null) {
        string tools = Path.Combine(baseDirectory ?? AppContext.BaseDirectory, "tools");
        return new(Path.Combine(tools, "yt-dlp.exe"), Path.Combine(tools, "ffmpeg.exe"), Path.Combine(tools, "ffprobe.exe"), Path.Combine(tools, "deno.exe"));
    }
    public void EnsureAvailable() {
        foreach (var path in new[] { Ytdlp, Ffmpeg, Ffprobe, Deno })
            if (!File.Exists(path)) throw new FileNotFoundException("A bundled dependency is missing: " + Path.GetFileName(path) + ". Extract the entire app ZIP to a folder before opening the app.", path);
    }
}
public sealed record ProcessResult(int ExitCode, string Stdout, string Stderr);
public static class ProcessRunner {
    public static async Task<ProcessResult> RunAsync(string executable, IEnumerable<string> arguments,
        Action<string, bool>? onLine = null, CancellationToken cancellation = default, bool captureOutput = true) {
        cancellation.ThrowIfCancellationRequested();
        var start = new ProcessStartInfo(executable) {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true,
            RedirectStandardError = true, RedirectStandardInput = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var arg in arguments) start.ArgumentList.Add(arg);
        start.Environment["PATH"] = Path.GetDirectoryName(executable) + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");
        start.Environment["PYTHONUTF8"] = "1";
        var cache = Path.Combine(AppPaths.DataDirectory, "cache");
        Directory.CreateDirectory(cache); start.Environment["DENO_DIR"] = Path.Combine(cache, "deno");
        using var process = new Process { StartInfo = start };
        process.Start(); process.StandardInput.Close();
        using var registration = cancellation.Register(() => {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { }
        });
        var stdout = new StringBuilder(); var stderr = new StringBuilder();
        async Task Drain(StreamReader reader, bool error, StringBuilder buffer) {
            while (await reader.ReadLineAsync() is { } line) {
                if (captureOutput) buffer.AppendLine(line);
                onLine?.Invoke(line, error);
            }
        }
        var output = Drain(process.StandardOutput, false, stdout); var errors = Drain(process.StandardError, true, stderr);
        await process.WaitForExitAsync(); await Task.WhenAll(output, errors);
        cancellation.ThrowIfCancellationRequested();
        return new(process.ExitCode, stdout.ToString(), stderr.ToString());
    }
}
