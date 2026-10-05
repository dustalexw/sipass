import Foundation

/// yt-dlp copies the whole video's tags onto every split chapter file, so each track would show
/// up with the full video's title and no track number. This retags each chapter file in place
/// (stream copy, no re-encode) with its own title, "track N/total", and the cover art.
enum ChapterTagger {
    struct Track: Equatable {
        let path: String
        let title: String
        let artist: String?
    }

    /// Formats whose tags FFmpeg can rewrite with a stream copy.
    static let taggableExtensions: Set<String> = ["mp3", "m4a", "flac", "opus", "ogg"]

    /// Parses one line written by `--print-to-file after_move:%(chapters)j`.
    static func tracks(fromChaptersJSON line: String, options o: DownloadOptions) -> [Track] {
        guard let data = line.data(using: .utf8),
              let chapters = (try? JSONSerialization.jsonObject(with: data)) as? [[String: Any]] else { return [] }
        return chapters.compactMap { chapter in
            guard let path = chapter["filepath"] as? String else { return nil }
            var title = (chapter["title"] as? String) ?? ""
            var artist: String?
            if o.cleanTitles { title = clean(title) }
            if o.splitArtistTitle, let pair = splitArtist(title) {
                artist = pair.artist
                title = pair.title
            }
            if title.isEmpty {
                title = ((path as NSString).lastPathComponent as NSString).deletingPathExtension
            }
            return Track(path: path, title: title, artist: artist)
        }
    }

    static func clean(_ title: String) -> String {
        guard let re = try? NSRegularExpression(pattern: CommandBuilder.junkTitlePattern) else { return title }
        let range = NSRange(title.startIndex..., in: title)
        return re.stringByReplacingMatches(in: title, range: range, withTemplate: "").trimmed
    }

    static func splitArtist(_ title: String) -> (artist: String, title: String)? {
        guard let re = try? NSRegularExpression(pattern: CommandBuilder.artistTitlePattern),
              let m = re.firstMatch(in: title, range: NSRange(title.startIndex..., in: title)),
              let a = Range(m.range(at: 1), in: title),
              let t = Range(m.range(at: 2), in: title) else { return nil }
        return (String(title[a]).trimmed, String(title[t]).trimmed)
    }

    /// Retags every track. The cover is taken from the full-length file because splitting
    /// Ogg files has to drop it. Returns log lines.
    static func retag(_ tracks: [Track], mainFile: String?, ffmpeg: String,
                      environment: [String: String]) async -> [String] {
        var log: [String] = []
        let fm = FileManager.default
        let work = fm.temporaryDirectory.appendingPathComponent("ytdlp-studio-tag-\(UUID().uuidString)")
        try? fm.createDirectory(at: work, withIntermediateDirectories: true)
        defer { try? fm.removeItem(at: work) }

        // 1. Pull the embedded cover out of the main file, if it has one.
        var cover: (path: String, mime: String)?
        if let main = mainFile, fm.fileExists(atPath: main) {
            let raw = work.appendingPathComponent("cover.img").path
            let r = try? await Shell.run(ffmpeg, ["-hide_banner", "-loglevel", "error", "-nostdin", "-y",
                                                  "-i", main, "-map", "0:v:0", "-c", "copy",
                                                  "-f", "image2", raw], environment: environment)
            if r?.status == 0, let data = fm.contents(atPath: raw), data.count > 8 {
                let isPNG = data.starts(with: [0x89, 0x50, 0x4E, 0x47])
                let named = work.appendingPathComponent(isPNG ? "cover.png" : "cover.jpg").path
                if (try? fm.moveItem(atPath: raw, toPath: named)) != nil {
                    cover = (named, isPNG ? "image/png" : "image/jpeg")
                }
            }
        }

        // 2. Rewrite each track's tags.
        let total = tracks.count
        for (index, track) in tracks.enumerated() {
            let ext = (track.path as NSString).pathExtension.lowercased()
            guard taggableExtensions.contains(ext), fm.fileExists(atPath: track.path) else { continue }
            let temp = (track.path as NSString).deletingPathExtension + ".tagging." + ext
            let isOgg = (ext == "opus" || ext == "ogg")
            let coverInput = isOgg ? nil : cover

            var args = ["-hide_banner", "-loglevel", "error", "-nostdin", "-y", "-i", track.path]
            if let c = coverInput { args += ["-i", c.path] }
            args += ["-map", "0:a"]
            if coverInput != nil { args += ["-map", "1:0"] }
            args += ["-c", "copy"]
            // Ogg stores tags on the audio stream: lift them to the file and clear the stream copy,
            // otherwise the old per-stream title would win over the new one.
            args += isOgg ? ["-map_metadata", "0:s:a:0", "-map_metadata:s:a", "-1"] : ["-map_metadata", "0"]
            if coverInput != nil { args += ["-disposition:v:0", "attached_pic"] }
            if ext == "mp3" { args += ["-id3v2_version", "3"] }
            args += ["-metadata", "title=\(track.title)", "-metadata", "track=\(index + 1)/\(total)"]
            if let artist = track.artist { args += ["-metadata", "artist=\(artist)"] }
            if isOgg, let c = cover, let picture = flacPictureBase64(path: c.path, mime: c.mime) {
                args += ["-metadata", "METADATA_BLOCK_PICTURE=\(picture)"]
            }
            args.append(temp)

            let result = try? await Shell.run(ffmpeg, args, environment: environment)
            if result?.status == 0, fm.fileExists(atPath: temp) {
                do {
                    try fm.removeItem(atPath: track.path)
                    try fm.moveItem(atPath: temp, toPath: track.path)
                    log.append("[ChapterTagger] \(index + 1)/\(total) \(track.title)")
                } catch {
                    log.append("[ChapterTagger] Could not replace \(track.path): \(error.localizedDescription)")
                }
            } else {
                try? fm.removeItem(atPath: temp)
                let reason = result?.stderr.trimmed.split(separator: "\n").last.map(String.init) ?? "FFmpeg failed to start"
                log.append("[ChapterTagger] Skipped \((track.path as NSString).lastPathComponent): \(reason)")
            }
        }
        return log
    }

    /// Ogg/Opus cover art is a base64 FLAC picture block in the METADATA_BLOCK_PICTURE tag.
    static func flacPictureBase64(path: String, mime: String) -> String? {
        guard let image = FileManager.default.contents(atPath: path) else { return nil }
        var block = Data()
        func u32(_ value: Int) {
            var be = UInt32(value).bigEndian
            withUnsafeBytes(of: &be) { block.append(contentsOf: $0) }
        }
        let mimeData = Data(mime.utf8)
        u32(3)                      // picture type: front cover
        u32(mimeData.count); block.append(mimeData)
        u32(0)                      // description length
        u32(0); u32(0); u32(0); u32(0) // width, height, depth, colours: unknown is allowed
        u32(image.count); block.append(image)
        return block.base64EncodedString()
    }
}
