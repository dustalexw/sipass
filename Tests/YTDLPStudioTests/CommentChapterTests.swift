import XCTest
@testable import YTDLPStudio

final class CommentChapterTests: XCTestCase {
    let trackList = "0:00 Intro\n0:02 First song\n0:04 Last song"
    func fixture(chapters: [[String: Any]] = []) -> [String: Any] {
        ["id": "video-one", "title": "Test album", "duration": 6.0, "chapters": chapters,
         "comments": [["id": "comment-one", "author": "Listener", "like_count": 10, "text": trackList]]]
    }

    func testParserFormatsAndFinalEnd() {
        let chapters = CommentChapterParser.parse("Track list\n[0:00] Intro\n1. 0:02 First song\nLast song – 0:04", duration: 6)
        XCTAssertEqual(chapters.map(\.title), ["Intro", "First song", "Last song"])
        XCTAssertEqual(chapters.map(\.start), [0, 2, 4])
        XCTAssertEqual(chapters.map(\.end), [2, 4, 6])
        let hours = CommentChapterParser.parse("0:00 Intro\n1:02:03 Middle\n2:00:00 End", duration: 8000)
        XCTAssertEqual(hours.map(\.start), [0, 3723, 7200])
    }

    func testRejectsInvalidAndUnrelatedTimestamps() {
        for text in ["0:00 One\n0:00 Two\n0:04 Three", "0:04 One\n0:02 Two\n0:05 Three",
                     "0:00 One\n0:02 Two\n0:06 Three", "0:00 One\n0:99 Two\n0:04 Three",
                     "0:00 One\n0:02 Two", "I love 0:00 so much\n0:02 Two\n0:04 Three",
                     "0:00 One to 0:01\n0:02 Two\n0:04 Three"] {
            XCTAssertTrue(CommentChapterParser.parse(text, duration: 6).isEmpty, text)
        }
        XCTAssertTrue(CommentChapterParser.parse(trackList, duration: .infinity).isEmpty)
        let opening = CommentChapterParser.parse("0:01 One\n0:02 Two\n0:04 Three", duration: 6)
        XCTAssertEqual(opening.first?.title, "Opening")
        XCTAssertEqual(opening.first?.start, 0)
    }

    func testSourcePriorityAndFailurePolicies() throws {
        let existing: [[String: Any]] = [["title": "Original", "start_time": 0.0, "end_time": 6.0]]
        XCTAssertFalse(CommentChapterService.needsComments(fixture(chapters: existing)))
        XCTAssertTrue(CommentChapterService.needsComments(["entries": [fixture(chapters: existing), fixture()]]))
        let kept = try CommentChapterService.prepare(fixture(chapters: existing), source: .commentsIfMissing,
                                                     selections: [:], keepComments: false, log: { _ in })
        XCTAssertEqual((kept[0]["chapters"] as? [[String: Any]])?.first?["title"] as? String, "Original")
        let replaced = try CommentChapterService.prepare(fixture(chapters: existing), source: .comments,
                                                         selections: [:], keepComments: false, log: { _ in })
        XCTAssertEqual((replaced[0]["chapters"] as? [[String: Any]])?.count, 3)
        XCTAssertNil(replaced[0]["comments"])
        var missing = fixture(); missing["comments"] = []
        XCTAssertThrowsError(try CommentChapterService.prepare(missing, source: .comments, selections: [:], keepComments: false, log: { _ in }))
        XCTAssertNoThrow(try CommentChapterService.prepare(missing, source: .commentsIfMissing, selections: [:], keepComments: false, log: { _ in }))
        XCTAssertThrowsError(try CommentChapterService.prepare(fixture(), source: .comments, selections: ["video-one": "deleted-comment"], keepComments: false, log: { _ in }))
    }

    func testManualSelectionAndPlaylistIsolation() throws {
        var info = fixture()
        var comments = info["comments"] as! [[String: Any]]
        comments.append(["id": "comment-two", "author": "Other", "like_count": 1, "text": "0:00 A\n0:02 B\n0:04 C"])
        info["comments"] = comments
        var second = fixture(); second["id"] = "video-two"
        let result = try CommentChapterService.prepare(["entries": [info, second]], source: .comments,
                    selections: ["video-one": "comment-two"], keepComments: true, log: { _ in })
        XCTAssertEqual(result.count, 2)
        XCTAssertEqual((result[0]["chapters"] as? [[String: Any]])?.first?["title"] as? String, "A")
        XCTAssertEqual((result[1]["chapters"] as? [[String: Any]])?.first?["title"] as? String, "Intro")
        XCTAssertNotNil(result[0]["comments"])
    }

    func testExistingSettingsAndLoadCommand() throws {
        var options = DownloadOptions(); options.outputDirectory = "/tmp/test folder"; options.playlistItems = "3,5"
        var saved = try JSONSerialization.jsonObject(with: JSONEncoder().encode(options)) as! [String: Any]
        saved.removeValue(forKey: "chapterSource")
        let migrated = try XCTUnwrap(OptionsStore.mergedWithDefaults(saved))
        XCTAssertEqual(migrated.chapterSource, .youtube)
        XCTAssertEqual(migrated.outputDirectory, options.outputDirectory)
        let args = CommandBuilder.arguments(for: options, urls: ["VIDEO"], infoFile: "/tmp/chapters.json")
        XCTAssertTrue(args.contains("--load-info-json")); XCTAssertTrue(args.contains("--no-clean-info-json"))
        XCTAssertFalse(args.contains("VIDEO")); XCTAssertFalse(args.contains("-I"))
    }
}

final class CommentChapterIntegrationTests: XCTestCase {
    private func run(_ executable: String, _ args: [String]) throws -> String {
        let process = Process(); let output = Pipe()
        process.executableURL = URL(fileURLWithPath: executable); process.arguments = args
        process.standardOutput = output; process.standardError = output
        try process.run()
        let text = String(decoding: output.fileHandleForReading.readDataToEndOfFile(), as: UTF8.self)
        process.waitUntilExit()
        XCTAssertEqual(process.terminationStatus, 0, text)
        return text
    }
    private func wait(_ job: DownloadJob, timeout: Double = 25) {
        let deadline = Date().addingTimeInterval(timeout)
        while !job.status.isDone && Date() < deadline {
            RunLoop.main.run(until: Date().addingTimeInterval(0.05))
        }
        XCTAssertTrue(job.status.isDone, job.log.joined(separator: "\n"))
    }

    func testQueueEmbedsSplitsAndRetagsCommentChapters() throws {
        let yt = "/opt/homebrew/bin/yt-dlp", ff = "/opt/homebrew/bin/ffmpeg", probe = "/opt/homebrew/bin/ffprobe"
        guard FileManager.default.isExecutableFile(atPath: yt), FileManager.default.isExecutableFile(atPath: ff) else {
            throw XCTSkip("Integration test needs yt-dlp and FFmpeg in /opt/homebrew/bin.")
        }
        let dir = FileManager.default.temporaryDirectory.appendingPathComponent("comment-chapter-test-\(UUID())")
        try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: dir) }
        let audio = dir.appendingPathComponent("source.wav")
        _ = try run(ff, ["-v", "error", "-f", "lavfi", "-i", "sine=frequency=440:duration=6", audio.path])
        var metadata: [String: Any] = [
            "_type": "video", "id": "video-one", "title": "Test album", "duration": 6.0,
            "extractor": "youtube", "extractor_key": "Youtube", "webpage_url": "https://www.youtube.com/watch?v=video-one",
            "url": audio.absoluteString, "ext": "wav", "protocol": "file",
            "formats": [["format_id": "audio", "url": audio.absoluteString, "ext": "wav", "protocol": "file", "vcodec": "none", "acodec": "pcm_s16le"]],
            "comments": [["id": "comment-one", "author": "Listener", "text": "0:00 Intro\n0:02 First song\n0:04 Last song"]]
        ]
        let info = dir.appendingPathComponent("fixture.json")
        try JSONSerialization.data(withJSONObject: metadata).write(to: info)
        let wrapper = dir.appendingPathComponent("yt-dlp-fixture")
        // Replace only the online extraction step; downloading and all postprocessing use real tools.
        try """
        #!/bin/sh
        for arg in "$@"; do
          if [ "$arg" = "--dump-single-json" ]; then
            cat '\(info.path)'
            exit 0
          fi
        done
        exec '\(yt)' "$@"
        """.write(to: wrapper, atomically: true, encoding: .utf8)
        try FileManager.default.setAttributes([.posixPermissions: 0o755], ofItemAtPath: wrapper.path)
        var options = DownloadOptions()
        options.mode = .audio; options.audioFormat = .m4a
        options.chapterSource = .comments; options.splitChapters = true
        options.embedThumbnail = false; options.musicTags = true
        options.cleanTitles = false
        options.outputDirectory = dir.appendingPathComponent("output").path
        options.extraArgs = "--ignore-config --enable-file-urls"
        let manager = DownloadManager()
        let tools = ToolSnapshot(ytdlp: wrapper.path, ffmpeg: ff, environment: ProcessInfo.processInfo.environment)
        manager.enqueue(urls: ["https://www.youtube.com/watch?v=video-one"], options: options, tools: tools,
                        commentSelections: ["video-one": "comment-one"])
        let job = try XCTUnwrap(manager.jobs.first)
        wait(job)
        XCTAssertEqual(job.status, .finished, job.log.joined(separator: "\n"))
        XCTAssertEqual(job.outputFiles.count, 4, job.log.joined(separator: "\n"))
        for path in job.outputFiles {
            XCTAssertTrue(FileManager.default.fileExists(atPath: path))
            let raw = try run(probe, ["-v", "error", "-show_chapters", "-show_format", "-of", "json", path])
            let obj = try JSONSerialization.jsonObject(with: Data(raw.utf8)) as! [String: Any]
            if path.contains("001 - Intro") {
                let tags = (obj["format"] as? [String: Any])?["tags"] as? [String: Any]
                XCTAssertEqual(tags?["title"] as? String, "Intro")
                XCTAssertTrue((tags?["track"] as? String ?? "").hasPrefix("1"))
            } else if !path.contains("/Test album/") {
                let chapters = obj["chapters"] as? [[String: Any]] ?? []
                XCTAssertEqual(chapters.count, 3)
                XCTAssertEqual((chapters.first?["tags"] as? [String: Any])?["title"] as? String, "Intro")
            }
        }
        // Retry must keep the chosen comment and reject disappearance before downloading.
        metadata["comments"] = []
        try JSONSerialization.data(withJSONObject: metadata).write(to: info)
        manager.retry(job); wait(job)
        if case .failed(let message) = job.status { XCTAssertTrue(message.contains("selected comment")) }
        else { XCTFail("Missing selection should stop the job: \(job.status)") }
    }

    func testCancellationDuringCommentFetching() throws {
        let dir = FileManager.default.temporaryDirectory.appendingPathComponent("comment-cancel-test-\(UUID())")
        try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: dir) }
        let wrapper = dir.appendingPathComponent("slow-fetch")
        try "#!/bin/sh\nexec /bin/sleep 30\n".write(to: wrapper, atomically: true, encoding: .utf8)
        try FileManager.default.setAttributes([.posixPermissions: 0o755], ofItemAtPath: wrapper.path)
        var options = DownloadOptions(); options.chapterSource = .comments; options.outputDirectory = dir.path
        let manager = DownloadManager()
        manager.enqueue(urls: ["VIDEO"], options: options,
                        tools: ToolSnapshot(ytdlp: wrapper.path, ffmpeg: nil, environment: ProcessInfo.processInfo.environment))
        let job = try XCTUnwrap(manager.jobs.first)
        manager.cancel(job); wait(job, timeout: 8)
        XCTAssertEqual(job.status, .cancelled)
    }
}
