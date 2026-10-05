import Foundation
@testable import Core

// MARK: - Tiny test framework

var passed = 0, failed = 0
func check(_ cond: Bool, _ name: String, _ detail: @autoclosure () -> String = "") {
    if cond { passed += 1; print("  ✓ \(name)") }
    else { failed += 1; print("  ✗ \(name)  \(detail())") }
}
func section(_ s: String) { print("\n▸ \(s)") }

func blocking<T>(_ op: @escaping () async throws -> T) -> Result<T, Error> {
    let sem = DispatchSemaphore(value: 0)
    var res: Result<T, Error>!
    Task.detached { do { res = .success(try await op()) } catch { res = .failure(error) }; sem.signal() }
    sem.wait()
    return res
}

func runSync(_ exe: String, _ args: [String]) -> ProcessResult {
    (try? blocking { try await Shell.run(exe, args, environment: ProcessInfo.processInfo.environment) }.get())
        ?? ProcessResult(status: -1, stdout: "", stderr: "launch failed")
}

/// Pump the main run loop until the condition is true or we time out.
func waitUntil(_ timeout: TimeInterval, _ cond: () -> Bool) -> Bool {
    let deadline = Date().addingTimeInterval(timeout)
    while !cond() && Date() < deadline { RunLoop.main.run(until: Date().addingTimeInterval(0.05)) }
    return cond()
}

struct Probe {
    let vcodec: String?, acodec: String?, height: Int?, channels: Int?, sampleRate: Int?, duration: Double?
}
func probe(_ path: String) -> Probe {
    let r = runSync("/usr/bin/ffprobe", ["-v", "error", "-print_format", "json", "-show_streams", "-show_format", path])
    let obj = (try? JSONSerialization.jsonObject(with: Data(r.stdout.utf8))) as? [String: Any] ?? [:]
    let streams = obj["streams"] as? [[String: Any]] ?? []
    let v = streams.first {
        ($0["codec_type"] as? String) == "video" &&
        ((($0["disposition"] as? [String: Any])?["attached_pic"] as? Int) ?? 0) == 0
    }
    let a = streams.first { ($0["codec_type"] as? String) == "audio" }
    return Probe(vcodec: v?["codec_name"] as? String,
                 acodec: a?["codec_name"] as? String,
                 height: v?["height"] as? Int,
                 channels: a?["channels"] as? Int,
                 sampleRate: Int((a?["sample_rate"] as? String) ?? ""),
                 duration: Double(((obj["format"] as? [String: Any])?["duration"] as? String) ?? ""))
}

let url = "http://127.0.0.1:8766/clip.mp4"   // Range-capable server, like real video hosts
let outRoot = "/tmp/studio-out"
try? FileManager.default.removeItem(atPath: outRoot)
UserDefaults.standard.register(defaults: ["maxConcurrent": 3, "playSound": false])

// MARK: - 1. Pure helpers

section("Tokenizer, quoting, line buffer")
check(CommandBuilder.tokenize(#"--match-filter "duration < 600" --geo-bypass 'a b' c\ d"#)
      == ["--match-filter", "duration < 600", "--geo-bypass", "a b", "c d"], "shell-style tokenizer")
check(CommandBuilder.shellQuote("plain-arg") == "plain-arg", "safe args stay unquoted")
check(CommandBuilder.shellQuote("it's %(title)s") == #"'it'\''s %(title)s'"#, "single quotes escaped")
var lb = LineBuffer()
let l1 = lb.append(Data("abc\r[dl] 1%\npart".utf8))
let l2 = lb.append(Data("ial\n".utf8))
check(l1 == ["abc", "[dl] 1%"] && l2 == ["partial"], "LineBuffer splits on CR and LF, holds partial lines")

// MARK: - 2. Tool detection

section("Tool detection")
let tools = ToolLocator()
check(tools.ytdlpPath != nil, "found yt-dlp at \(tools.ytdlpPath ?? "nil")")
check(tools.ffmpegPath != nil, "found ffmpeg at \(tools.ffmpegPath ?? "nil")")
_ = waitUntil(15) { tools.ytdlpVersion != "—" && tools.ffmpegVersion != "—" }
check(tools.ytdlpVersion.first?.isNumber == true, "read yt-dlp version: \(tools.ytdlpVersion)")
check(tools.ffmpegVersion != "—" && tools.ffmpegVersion != "Not found", "read ffmpeg version: \(tools.ffmpegVersion.prefix(30))")
guard let snap = tools.snapshot() else { print("cannot continue without tools"); exit(1) }

// MARK: - 3. Every preset + edge configs must be accepted by yt-dlp's own parser

section("yt-dlp accepts every generated command (--simulate)")
var configs: [(String, DownloadOptions)] = OptionsStore.builtInPresets.map { ($0.name, $0.options) }
do {
    var o = DownloadOptions(); o.mode = .videoOnly; o.container = .webm; o.fpsLimit = .fps30; o.videoCodec = .vp9
    configs.append(("Video-only WebM", o))
    o = DownloadOptions(); o.mode = .audio; o.audioFormat = .opus; o.normalizeAudio = true; o.volumeDB = -3.5
    o.sampleRate = .r48000; o.channels = .mono
    configs.append(("Audio with every filter", o))
    o = DownloadOptions(); o.writeSubs = true; o.writeAutoSubs = true; o.subFormat = .ass; o.embedSubs = true
    o.trimEnabled = true; o.trimStart = "5"; o.trimEnd = "1:00"; o.forceKeyframes = true
    o.sponsorBlockMode = .mark; o.sponsorCategories = Array(SponsorCategory.allCases)
    o.removeChaptersRegex = "(?i)intro"; o.thumbnailFormat = .png; o.writeThumbnail = true
    o.playlistMode = .full; o.playlistItems = "1-5,8"; o.rateLimit = "5M"; o.retries = 3; o.sleepInterval = 5
    o.filenameTemplate = .playlistFolder; o.restrictFilenames = true
    o.customPPA = "Merger+ffmpeg_o:-metadata comment=test"
    o.extraArgs = #"--match-filter "duration < 3600" --geo-bypass"#
    configs.append(("Kitchen sink (subs, trim, SponsorBlock, playlist, network…)", o))
    o = DownloadOptions(); o.sponsorBlockMode = .remove; o.sponsorCategories = Array(SponsorCategory.allCases)
    configs.append(("SponsorBlock remove, all categories", o))
    o = DownloadOptions(); o.filenameTemplate = .dateTitle; o.customFormat = "b"; o.customSort = "res,+size"
    configs.append(("Custom -f / -S", o))
}
for (name, o) in configs {
    var opts = o; opts.outputDirectory = outRoot + "/simulate"
    let args = ["--simulate"] + CommandBuilder.arguments(for: opts, urls: [url], ffmpegLocation: "/usr/bin")
    let r = runSync(snap.ytdlp, args)
    let tail = r.stderr.split(separator: "\n").suffix(2).joined(separator: " | ")
    check(r.status == 0 && !r.stderr.contains("yt-dlp: error:"), name, tail)
}

// MARK: - 4. Analyze

section("Analyze (MetadataFetcher)")
switch blocking({ try await MetadataFetcher.fetch(url: url, tools: snap, cookieBrowser: .none) }) {
case .success(let info):
    check(!info.formats.isEmpty, "lists formats (\(info.formats.count)), title “\(info.title)”")
    check(!info.isPlaylist, "detected single video")
case .failure(let e):
    check(false, "fetched metadata", e.localizedDescription)
}
if case .failure(let e) = blocking({ try await MetadataFetcher.fetch(url: "http://127.0.0.1:8766/missing.mp4", tools: snap, cookieBrowser: .none) }) {
    check(!e.localizedDescription.isEmpty, "bad link surfaces an error: \(e.localizedDescription.prefix(60))")
} else { check(false, "bad link should fail") }

// MARK: - 5. End-to-end through the real DownloadManager

let manager = DownloadManager()
func run(_ name: String, timeout: TimeInterval = 240, _ configure: (inout DownloadOptions) -> Void) -> DownloadJob {
    var o = DownloadOptions()
    o.outputDirectory = outRoot + "/" + name.replacingOccurrences(of: " ", with: "_")
    configure(&o)
    manager.enqueue(urls: [url], options: o, tools: snap)
    let job = manager.jobs.last!
    let ok = waitUntil(timeout) { job.status.isDone }
    if !ok || job.status != .finished {
        print("    status: \(job.status.label)\n    log tail:\n      " + job.log.suffix(6).joined(separator: "\n      "))
    }
    return job
}

section("E2E: video")
do {
    let j = run("mp4") { _ in }
    check(j.status == .finished, "finished")
    check(j.title == "clip", "title captured from progress template (“\(j.title)”)")
    check(j.outputFiles.count == 1 && FileManager.default.fileExists(atPath: j.outputFiles.first ?? ""),
          "final path captured via --print-to-file", "\(j.outputFiles)")
    if let f = j.outputFiles.first { let p = probe(f); check(p.vcodec == "h264" && p.height == 720, "H.264 720p MP4") }
    check(j.log.contains { $0.contains("[download]") }, "regular log lines still shown (not silenced)")
}
do {
    let j = run("mkv remux") { $0.container = .mkv }
    check(j.outputFiles.first?.hasSuffix(".mkv") == true, "remuxed to MKV", "\(j.outputFiles)")
}

do {
    let j = run("webm fallback") { $0.container = .webm }
    check(j.status == .finished && j.outputFiles.first?.hasSuffix(".mkv") == true,
          "WebM requested for H.264 source → safely falls back to MKV instead of failing", "\(j.status.label) \(j.outputFiles)")
}
do {
    let j = run("video only") { $0.mode = .videoOnly }
    check(j.status == .finished, "Video Only works on a site without separate video streams", j.status.label)
}

section("E2E: audio extraction + FFmpeg filters")
do {
    let j = run("mp3") { $0.mode = .audio; $0.audioFormat = .mp3; $0.audioQuality = .k192 }
    let f = j.outputFiles.first ?? ""
    check(f.hasSuffix(".mp3") && probe(f).acodec == "mp3", "MP3 file", "\(j.outputFiles)")
}
do {
    let j = run("opus filters") { o in
        o.mode = .audio; o.audioFormat = .opus; o.normalizeAudio = true; o.volumeDB = -2
        o.sampleRate = .r48000; o.channels = .mono
    }
    let f = j.outputFiles.first ?? ""
    let p = probe(f)
    check(p.acodec == "opus", "Opus codec", "\(j.outputFiles)")
    check(p.channels == 1, "downmixed to mono (channels=\(p.channels ?? -1))")
    check(j.log.contains { $0.contains("loudnorm") }, "loudnorm reached ffmpeg")
}
do {
    let j = run("flac") { o in o.mode = .audio; o.audioFormat = .flac; o.sampleRate = .r48000 }
    let p = probe(j.outputFiles.first ?? "")
    check(p.acodec == "flac" && p.sampleRate == 48000, "FLAC resampled to 48 kHz (\(p.sampleRate ?? -1))")
}

section("E2E: trim")
do {
    let j = run("trim") { o in o.trimEnabled = true; o.trimStart = "3"; o.trimEnd = "9"; o.forceKeyframes = true }
    let d = probe(j.outputFiles.first ?? "").duration ?? -1
    check(abs(d - 6) < 1.0, "6 s section (got \(String(format: "%.2f", d)) s)")
}

section("E2E: FFmpeg re-encode pass, every software encoder, downscale + loudnorm")
let encodeCases: [(VideoEncoder, VideoContainer, String)] = [
    (.x264, .mp4, "h264"), (.x265, .mp4, "hevc"), (.vp9, .webm, "vp9"), (.av1, .mkv, "av1"), (.prores, .mov, "prores")
]
for (enc, cont, expected) in encodeCases {
    let j = run("enc \(enc.rawValue)") { o in
        o.container = cont; o.encodeEnabled = true; o.encoder = enc; o.crf = enc.defaultCRF
        o.preset = .ultrafast; o.scale = .h480; o.normalizeAudio = true
    }
    if let f = j.outputFiles.first(where: { $0.contains(".encoded.") }) {
        let p = probe(f)
        check(p.vcodec == expected && p.height == 480, "\(enc.rawValue) → \(cont.rawValue.uppercased()): \(p.vcodec ?? "nil") \(p.height ?? 0)p, audio \(p.acodec ?? "nil")")
    } else {
        check(false, "\(enc.rawValue) produced an encoded file", j.status.label)
    }
}
do {
    let j = run("replace original") { o in
        o.encodeEnabled = true; o.encoder = .x264; o.preset = .ultrafast; o.replaceOriginal = true; o.scale = .h360
    }
    let f = j.outputFiles.first ?? ""
    check(j.outputFiles.count == 1 && !f.contains(".encoded.") && probe(f).height == 360,
          "original replaced in place", "\(j.outputFiles)")
    check(j.progress == 1, "progress ends at 100%")
}

section("E2E: queue behavior")
do {
    var o = DownloadOptions()
    o.outputDirectory = outRoot + "/cancel"
    o.encodeEnabled = true; o.encoder = .av1; o.preset = .veryslow; o.crf = 5
    manager.enqueue(urls: [url], options: o, tools: snap)
    let j = manager.jobs.last!
    _ = waitUntil(90) {
        if case .encoding = j.status { return j.progress > 0.01 }
        return j.status.isDone
    }
    let wasEncoding: Bool = { if case .encoding = j.status { return true }; return false }()
    check(wasEncoding, "encode progress parsed from ffmpeg (\(String(format: "%.0f", j.progress * 100))%)", j.status.label)
    manager.cancel(j)
    _ = waitUntil(15) { j.status.isDone }
    check(j.status == .cancelled, "cancel stops a slow AV1 encode within seconds (\(j.status.label))")
    let leftovers = ((try? FileManager.default.contentsOfDirectory(atPath: o.outputDirectory)) ?? []).filter { $0.contains(".encoded.") }
    check(leftovers.isEmpty, "partial encode cleaned up", "\(leftovers)")
    manager.retry(j)
    check(j.status.isActive, "retry restarts the job (\(j.status.label))")
    manager.cancel(j)
    _ = waitUntil(15) { j.status.isDone }
}
do {
    var o = DownloadOptions(); o.outputDirectory = outRoot + "/fail"
    manager.enqueue(urls: ["http://127.0.0.1:8766/nope.mp4"], options: o, tools: snap)
    let j = manager.jobs.last!
    _ = waitUntil(60) { j.status.isDone }
    if case .failed(let msg) = j.status { check(!msg.isEmpty, "bad link fails with a message: \(msg.prefix(70))") }
    else { check(false, "bad link reported as failed", j.status.label) }
}
func drain(_ urls: [String], into dir: String) -> (startedAtOnce: Int, jobs: [DownloadJob]) {
    var o = DownloadOptions(); o.outputDirectory = outRoot + "/" + dir
    manager.clearFinished()
    manager.enqueue(urls: urls, options: o, tools: snap)
    let batch = Array(manager.jobs.suffix(urls.count))
    let active = batch.filter { $0.status.isActive }.count
    _ = waitUntil(180) { batch.allSatisfy { $0.status.isDone } }
    return (active, batch)
}
func summary(_ jobs: [DownloadJob]) -> String {
    jobs.map { "\n      \($0.status.label) | \($0.log.suffix(2).joined(separator: " / ").prefix(200))" }.joined()
}
UserDefaults.standard.set(2, forKey: "maxConcurrent")
do {
    let base = "http://127.0.0.1:8766/"
    let r = drain(["a", "b", "c", "d"].map { base + $0 + ".mp4" }, into: "parallel")
    check(r.startedAtOnce == 2, "respects simultaneous-download limit (\(r.startedAtOnce) of 4 started)")
    check(r.jobs.allSatisfy { $0.status == .finished }, "queue drains automatically, all 4 finish", summary(r.jobs))
}
do {
    let r = drain(["http://127.0.0.1:8766/x/clip.mp4", "http://127.0.0.1:8766/y/clip.mp4"], into: "same-title")
    check(r.startedAtOnce == 2, "two different links with the same title run concurrently")
    check(r.jobs.allSatisfy { $0.status == .finished }, "…and don't corrupt each other (private temp dirs)", summary(r.jobs))
}
do {
    let r = drain([url, url], into: "duplicate")
    check(r.startedAtOnce == 1, "the same link twice is serialized, not run in parallel")
    check(r.jobs.allSatisfy { $0.status == .finished }, "…and both complete", summary(r.jobs))
}
let leftoverTemps = ((try? FileManager.default.contentsOfDirectory(atPath: NSTemporaryDirectory())) ?? [])
    .filter { $0.hasPrefix("ytdlp-studio-") }
check(leftoverTemps.isEmpty, "per-job temp folders cleaned up", "\(leftoverTemps.prefix(3))")

print("\n\(passed) passed, \(failed) failed")
exit(failed == 0 ? 0 : 1)
