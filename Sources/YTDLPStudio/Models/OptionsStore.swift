import Foundation
import Combine

struct Preset: Codable, Identifiable, Equatable {
    var id = UUID()
    var name: String
    var options: DownloadOptions
}

final class OptionsStore: ObservableObject {
    @Published var options: DownloadOptions {
        didSet { persist(options, key: Self.optionsKey) }
    }
    @Published private(set) var userPresets: [Preset] {
        didSet { persist(userPresets, key: Self.presetsKey) }
    }

    private static let optionsKey = "currentOptions.v1"
    private static let presetsKey = "userPresets.v1"

    init() {
        options = Self.load(DownloadOptions.self, key: Self.optionsKey) ?? DownloadOptions()
        userPresets = Self.load([Preset].self, key: Self.presetsKey) ?? []
    }

    /// Applies a preset but keeps the user's chosen output folder.
    func apply(_ preset: Preset) {
        var o = preset.options
        o.outputDirectory = options.outputDirectory
        options = o
    }

    func saveCurrent(as name: String) {
        let clean = name.trimmed
        guard !clean.isEmpty else { return }
        if let idx = userPresets.firstIndex(where: { $0.name == clean }) {
            userPresets[idx].options = options
        } else {
            userPresets.append(Preset(name: clean, options: options))
        }
    }

    func delete(_ preset: Preset) {
        userPresets.removeAll { $0.id == preset.id }
    }

    func reset() {
        let dir = options.outputDirectory
        options = DownloadOptions()
        options.outputDirectory = dir
    }

    // MARK: Persistence

    private func persist<T: Encodable>(_ value: T, key: String) {
        if let data = try? JSONEncoder().encode(value) {
            UserDefaults.standard.set(data, forKey: key)
        }
    }

    private static func load<T: Decodable>(_ type: T.Type, key: String) -> T? {
        guard let data = UserDefaults.standard.data(forKey: key) else { return nil }
        return try? JSONDecoder().decode(type, from: data)
    }

    // MARK: Built-in presets

    static let builtInPresets: [Preset] = {
        func make(_ name: String, _ edit: (inout DownloadOptions) -> Void) -> Preset {
            var o = DownloadOptions()
            edit(&o)
            return Preset(name: name, options: o)
        }
        return [
            make("Best quality (MKV)") { o in
                o.container = .mkv
                o.preferCompatibleStreams = false
            },
            make("Compatible MP4 · H.264 · 1080p") { o in
                o.container = .mp4
                o.maxResolution = .p1080
                o.videoCodec = .h264
                o.audioCodec = .aac
            },
            make("MP3 320 kbps") { o in
                o.mode = .audio
                o.audioFormat = .mp3
                o.audioQuality = .k320
            },
            make("Lossless FLAC") { o in
                o.mode = .audio
                o.audioFormat = .flac
            },
            make("Podcast · Opus mono, normalized") { o in
                o.mode = .audio
                o.audioFormat = .opus
                o.audioQuality = .k64
                o.channels = .mono
                o.normalizeAudio = true
            },
            make("Album · split chapters to MP3") { o in
                o.mode = .audio
                o.audioFormat = .mp3
                o.audioQuality = .q0
                o.splitChapters = true
                o.chaptersInFolder = true
            },
            make("Ad-free · SponsorBlock cut") { o in
                o.sponsorBlockMode = .remove
                o.sponsorCategories = [.sponsor, .selfpromo, .interaction, .intro, .outro]
            },
            make("Shrink for sharing · HEVC 720p") { o in
                o.maxResolution = .p1080
                o.encodeEnabled = true
                o.encoder = .vtHEVC
                o.hwBitrateMbps = 2.5
                o.scale = .h720
                o.encodeAudioBitrate = .b128
            },
            make("Archive everything") { o in
                o.container = .mkv
                o.preferCompatibleStreams = false
                o.writeSubs = true
                o.subLangs = "all,-live_chat"
                o.subFormat = .best
                o.embedSubs = true
                o.writeInfoJSON = true
                o.writeDescription = true
                o.writeThumbnail = true
                o.useArchive = true
                o.filenameTemplate = .uploaderFolder
            }
        ]
    }()
}
