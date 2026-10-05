using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
namespace YtdlpStudio.Core;

public sealed record Chapter(string Title, double Start, double End) {
    public JsonObject Json() => new() { ["title"] = Title, ["start_time"] = Start, ["end_time"] = End };
}
public sealed record CommentCandidate(string Id, string Author, string Text, int Likes, IReadOnlyList<Chapter> Chapters);
public sealed record CommentPreview(string VideoId, string Title, IReadOnlyList<CommentCandidate> Candidates);
public static class CommentChapters {
    static readonly Regex Timestamp = new(@"(?<![\d:])(?:\d{1,3}:)?\d{1,3}:\d{2}(?![\d:])");
    public static string Text(JsonNode? node) => node?.ToString() ?? "";
    public static double Number(JsonNode? node) => double.TryParse(Text(node), System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : 0;
    public static IReadOnlyList<Chapter> Parse(string text, double duration) {
        if (!double.IsFinite(duration) || duration <= 0) return [];
        var items = new List<(string Title, double Start)>();
        foreach (var line in text.Split(['\r', '\n'])) {
            var matches = Timestamp.Matches(line); if (matches.Count == 0) continue; if (matches.Count != 1) return [];
            var match = matches[0]; var parts = match.Value.Split(':').Select(int.Parse).ToArray();
            if (parts[^1] >= 60 || (parts.Length == 3 && parts[1] >= 60)) return [];
            double seconds = parts.Aggregate(0, (sum, part) => sum * 60 + part);
            if (seconds >= duration || (items.Count > 0 && seconds <= items[^1].Start)) return [];
            string Clean(string s) => s.Trim().Trim('[', ']', '(', ')', '–', '—', '-', ':', '|', '•', ' ').Trim();
            var before = Clean(line[..match.Index]); var after = Clean(line[(match.Index + match.Length)..]);
            bool number = Regex.IsMatch(before, @"^#?\d{1,3}[.)]?$");
            if (before.Length != 0 && after.Length != 0 && !number) return [];
            string title = after.Length > 0 && (before.Length == 0 || number) ? after : before;
            if (title.Length == 0) return []; items.Add((title, seconds));
        }
        if (items.Count < 3) return [];
        if (items[0].Start > 0) items.Insert(0, ("Opening", 0));
        return items.Select((item, i) => new Chapter(item.Title, item.Start, i + 1 < items.Count ? items[i + 1].Start : duration)).ToArray();
    }
    public static IReadOnlyList<CommentCandidate> Candidates(JsonObject video) {
        double duration = Number(video["duration"]);
        return (video["comments"] as JsonArray ?? []).OfType<JsonObject>().Select(c => {
            var text = Text(c["text"]);
            return new CommentCandidate(Text(c["id"]), Text(c["author"]), text, (int)Number(c["like_count"]), Parse(text, duration));
        }).Where(c => c.Id.Length > 0 && c.Chapters.Count > 0).OrderByDescending(c => c.Chapters.Count).ThenByDescending(c => c.Likes).ThenBy(c => c.Id).ToArray();
    }
    public static bool NeedsComments(JsonObject info) => info["entries"] is JsonArray entries
        ? entries.OfType<JsonObject>().Any(NeedsComments) : info["chapters"] is not JsonArray { Count: > 0 };
    public static JsonArray Prepare(JsonObject info, string source, IReadOnlyDictionary<string, string> selections, bool keepComments, Action<string>? log = null) {
        var output = new JsonArray();
        if (info["entries"] is JsonArray entries) {
            foreach (var entry in entries.OfType<JsonObject>())
                foreach (var video in Prepare(entry, source, selections, keepComments, log)) output.Add(video?.DeepClone());
            return output;
        }
        var prepared = (JsonObject)info.DeepClone();
        if (source == "comments" || (source == "commentsIfMissing" && NeedsComments(info))) {
            var candidates = Candidates(info); var id = Text(info["id"]);
            selections.TryGetValue(id, out var selectedId);
            var selected = selectedId == null ? candidates.FirstOrDefault() : candidates.FirstOrDefault(c => c.Id == selectedId);
            if (selected != null) {
                prepared["chapters"] = new JsonArray(selected.Chapters.Select(c => (JsonNode)c.Json()).ToArray());
                log?.Invoke($"Using {selected.Chapters.Count} chapters from {selected.Author} for {Text(info["title"])}.");
            } else if (selectedId != null) throw new InvalidOperationException("The selected comment was not found. Search again to select a chapter list.");
            else if (source == "comments") throw new InvalidOperationException("No valid comment chapter list found for " + Text(info["title"]) + ". Try YouTube chapters or preview another video.");
            else log?.Invoke("No comment chapter list found; downloading without chapters.");
        } else log?.Invoke("Keeping existing YouTube chapters.");
        foreach (var field in new[] { "requested_formats", "requested_downloads", "requested_subtitles" }) prepared.Remove(field);
        if (!keepComments) prepared.Remove("comments"); output.Add(prepared); return output;
    }
    public static async Task<JsonObject> FetchAsync(string url, DownloadOptions options, ToolPaths tools,
        bool includeComments, bool preview = false, Action<string, bool>? onLine = null, CancellationToken cancellation = default) {
        var args = new List<string> { "--ignore-config", "--skip-download", "--dump-single-json", "--no-clean-info-json", "--no-colors", "--ffmpeg-location", Path.GetDirectoryName(tools.Ffmpeg)!, "--js-runtimes", "deno:" + tools.Deno };
        args.AddRange(CommandBuilder.ExtractionOptions(options, !preview));
        if (preview) { args.Remove("--yes-playlist"); args.Add("--no-playlist"); }
        args.Add(includeComments ? "--write-comments" : "--no-write-comments");
        if (includeComments) args.AddRange(["--extractor-args", "youtube:comment_sort=top;max_comments=200,200,0,0"]);
        args.AddRange(["--", url]);
        var result = await ProcessRunner.RunAsync(tools.Ytdlp, args, onLine, cancellation);
        if (result.ExitCode != 0) throw new InvalidOperationException(LastError(result.Stderr, "Could not read video metadata."));
        return JsonNode.Parse(result.Stdout) as JsonObject ?? throw new InvalidOperationException("yt-dlp returned unreadable metadata.");
    }
    public static string LastError(string text, string fallback) => text.Split('\n').LastOrDefault(s => s.Contains("ERROR:"))?.Trim() ?? fallback;
    public static async Task<CommentPreview> PreviewAsync(string url, DownloadOptions options, ToolPaths tools, CancellationToken cancellation) {
        var info = await FetchAsync(url, options, tools, true, true, cancellation: cancellation);
        if (info["entries"] != null) throw new InvalidOperationException("Paste an individual video link to preview its comment chapters.");
        var candidates = Candidates(info);
        if (candidates.Count == 0) throw new InvalidOperationException("No valid list found in the first 200 top comments. Lists need three titled, increasing timestamps. Comments may be disabled.");
        return new(Text(info["id"]), Text(info["title"]), candidates);
    }
}
