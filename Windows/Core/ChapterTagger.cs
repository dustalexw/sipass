using System.Buffers.Binary;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
namespace YtdlpStudio.Core;

public sealed record ChapterTrack(string Path, string Title, string? Artist);
public static class ChapterTagger {
    public static IReadOnlyList<ChapterTrack> Tracks(string line, DownloadOptions o) {
        if (JsonNode.Parse(line) is not JsonArray chapters) return [];
        return chapters.OfType<JsonObject>().Where(c => c["filepath"] != null).Select(c => {
            string path = CommentChapters.Text(c["filepath"]); string title = CommentChapters.Text(c["title"]); string? artist = null;
            if (o.CleanTitles) title = Regex.Replace(title, CommandBuilder.JunkTitlePattern, "").Trim();
            if (o.StripTitleNumbers) title = StripNumber(title);
            if (o.SplitArtistTitle) {
                var match = Regex.Match(title, @"^(.+?)\s+[-–—]\s+(.+)$");
                if (match.Success) { artist = match.Groups[1].Value.Trim(); title = match.Groups[2].Value.Trim(); if (o.StripTitleNumbers) title = StripNumber(title); }
            }
            if (title.Length == 0) title = Path.GetFileNameWithoutExtension(path);
            return new ChapterTrack(path, title, artist);
        }).ToArray();
    }
    public static string StripNumber(string title) {
        var match = Regex.Match(title, CommandBuilder.NumberPattern("t").Replace("(?P<", "(?<"));
        return match.Success ? match.Groups["t"].Value.Trim() : title;
    }
    public static string Picture(byte[] image, string mime) {
        using var stream = new MemoryStream();
        void U32(int n) { var bytes = new byte[4]; BinaryPrimitives.WriteUInt32BigEndian(bytes, (uint)n); stream.Write(bytes); }
        var type = Encoding.UTF8.GetBytes(mime); U32(3); U32(type.Length); stream.Write(type); U32(0);
        U32(0); U32(0); U32(0); U32(0); U32(image.Length); stream.Write(image);
        return Convert.ToBase64String(stream.ToArray());
    }
    public static async Task RetagAsync(IReadOnlyList<ChapterTrack> tracks, string? main, ToolPaths tools, Action<string> log, CancellationToken cancellation) {
        var work = Path.Combine(Path.GetTempPath(), "YTDLPStudio", "tag-" + Guid.NewGuid()); Directory.CreateDirectory(work);
        try {
            string? cover = null; string? picture = null;
            if (main != null && File.Exists(main)) {
                var raw = Path.Combine(work, "cover.img");
                var result = await ProcessRunner.RunAsync(tools.Ffmpeg, ["-hide_banner", "-loglevel", "error", "-nostdin", "-y", "-i", main, "-map", "0:v:0", "-c", "copy", "-f", "image2", raw], cancellation: cancellation);
                if (result.ExitCode == 0 && File.Exists(raw) && new FileInfo(raw).Length > 8) {
                    var bytes = await File.ReadAllBytesAsync(raw, cancellation); bool png = bytes.AsSpan(0, 4).SequenceEqual(new byte[] { 0x89, 0x50, 0x4e, 0x47 });
                    cover = Path.Combine(work, png ? "cover.png" : "cover.jpg"); File.Move(raw, cover);
                    picture = Picture(bytes, png ? "image/png" : "image/jpeg");
                }
            }
            for (int i = 0; i < tracks.Count; i++) {
                cancellation.ThrowIfCancellationRequested(); var track = tracks[i]; var ext = Path.GetExtension(track.Path).ToLowerInvariant();
                if (!new[] { ".mp3", ".m4a", ".flac", ".opus", ".ogg" }.Contains(ext) || !File.Exists(track.Path)) continue;
                bool ogg = ext is ".opus" or ".ogg"; var temp = Path.Combine(Path.GetDirectoryName(track.Path)!, ".studio-tag-" + Guid.NewGuid() + ext);
                try {
                    var args = new List<string> { "-hide_banner", "-loglevel", "error", "-nostdin", "-y", "-i", track.Path };
                    if (cover != null && !ogg) args.AddRange(["-i", cover]);
                    args.AddRange(["-map", "0:a"]);
                    if (cover != null && !ogg) args.AddRange(["-map", "1:0", "-disposition:v:0", "attached_pic"]);
                    args.AddRange(["-c", "copy"]);
                    args.AddRange(ogg ? ["-map_metadata", "0:s:a:0", "-map_metadata:s:a", "-1"] : new[] { "-map_metadata", "0" });
                    if (ext == ".mp3") args.AddRange(["-id3v2_version", "3"]);
                    args.AddRange(["-metadata", "title=" + track.Title, "-metadata", $"track={i + 1}/{tracks.Count}"]);
                    if (track.Artist != null) args.AddRange(["-metadata", "artist=" + track.Artist]);
                    if (ogg && picture != null) args.AddRange(["-metadata", "METADATA_BLOCK_PICTURE=" + picture]); args.Add(temp);
                    var result = await ProcessRunner.RunAsync(tools.Ffmpeg, args, cancellation: cancellation);
                    if (result.ExitCode != 0) throw new InvalidOperationException("Could not tag " + Path.GetFileName(track.Path) + ": " + result.Stderr.Trim());
                    File.Move(temp, track.Path, true); log($"[ChapterTagger] {i + 1}/{tracks.Count} {track.Title}");
                } finally { if (File.Exists(temp)) File.Delete(temp); }
            }
        } finally { Directory.Delete(work, true); }
    }
}
