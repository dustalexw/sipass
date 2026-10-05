import SwiftUI

struct FormatInspector: View {
    let info: MediaInfo
    var onUseFormat: (String) -> Void
    var onDownload: () -> Void

    @Environment(\.dismiss) private var dismiss
    @State private var selection = Set<FormatInfo.ID>()
    @State private var filter: Filter = .all

    enum Filter: String, CaseIterable, Identifiable {
        case all = "All", video = "Video", audio = "Audio", combined = "A+V"
        var id: String { rawValue }
    }

    private var visibleFormats: [FormatInfo] {
        switch filter {
        case .all: return info.formats
        case .video: return info.formats.filter { $0.hasVideo && !$0.hasAudio }
        case .audio: return info.formats.filter { $0.hasAudio && !$0.hasVideo }
        case .combined: return info.formats.filter { $0.hasVideo && $0.hasAudio }
        }
    }

    /// One video stream + one audio stream, joined the way yt-dlp expects (e.g. "137+140").
    private var selector: String? {
        let picked = info.formats.filter { selection.contains($0.id) }
        guard !picked.isEmpty else { return nil }
        let video = picked.first { $0.hasVideo }
        let audio = picked.first { $0.hasAudio && !$0.hasVideo }
        let ids = [video?.id, (video?.hasAudio == true ? nil : audio?.id)].compactMap { $0 }
        return ids.isEmpty ? picked.first?.id : ids.joined(separator: "+")
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            headerView

            if info.isPlaylist {
                Text("Playlist with \(info.entries.count) items").font(.headline)
                List(Array(info.entries.enumerated()), id: \.offset) { index, title in
                    HStack {
                        Text("\(index + 1)")
                            .monospacedDigit()
                            .foregroundStyle(.secondary)
                            .frame(width: 36, alignment: .trailing)
                        Text(title).lineLimit(1)
                    }
                }
                .listStyle(.inset(alternatesRowBackgrounds: true))
                Hint("Use Playlist settings to choose a range of items. Formats are picked per video using your Format settings.")
            } else {
                HStack {
                    Picker("Show", selection: $filter) {
                        ForEach(Filter.allCases) { Text($0.rawValue).tag($0) }
                    }
                    .pickerStyle(.segmented)
                    .frame(width: 280)
                    Spacer()
                    Text("Select one video and one audio stream, or a single combined format.")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
                Table(visibleFormats, selection: $selection) {
                    TableColumn("ID") { Text($0.id).font(.system(.body, design: .monospaced)) }
                        .width(min: 50, ideal: 70)
                    TableColumn("Type") { Text($0.kind) }
                        .width(50)
                    TableColumn("Ext") { Text($0.ext) }
                        .width(45)
                    TableColumn("Resolution") { Text($0.resolution) }
                        .width(min: 80, ideal: 95)
                    TableColumn("FPS") { Text($0.fps) }
                        .width(36)
                    TableColumn("Video codec") { Text($0.vcodec).lineLimit(1) }
                    TableColumn("Audio codec") { Text($0.acodec).lineLimit(1) }
                    TableColumn("Bitrate") { Text($0.bitrate).monospacedDigit() }
                        .width(60)
                    TableColumn("Size") { Text($0.size).monospacedDigit() }
                        .width(70)
                    TableColumn("Note") { Text($0.note).foregroundStyle(.secondary).lineLimit(1) }
                }

                if !info.chapters.isEmpty {
                    DisclosureGroup("\(info.chapters.count) chapters") {
                        ScrollView {
                            VStack(alignment: .leading, spacing: 3) {
                                ForEach(info.chapters) { c in
                                    HStack {
                                        Text(c.start).monospacedDigit().foregroundStyle(.secondary).frame(width: 64, alignment: .trailing)
                                        Text(c.title)
                                    }
                                    .font(.callout)
                                }
                            }
                            .frame(maxWidth: .infinity, alignment: .leading)
                        }
                        .frame(maxHeight: 120)
                    }
                }
            }

            HStack {
                if let s = selector {
                    Text("Format: ").foregroundColor(.secondary) + Text(s).font(.system(.body, design: .monospaced))
                }
                Spacer()
                Button("Close") { dismiss() }
                    .keyboardShortcut(.cancelAction)
                if !info.isPlaylist {
                    Button("Use selected format") {
                        if let s = selector { onUseFormat(s) }
                        dismiss()
                    }
                    .disabled(selector == nil)
                }
                Button(selector == nil ? "Download" : "Download selected") {
                    if let s = selector { onUseFormat(s) }
                    dismiss()
                    onDownload()
                }
                .buttonStyle(.borderedProminent)
                .keyboardShortcut(.defaultAction)
            }
        }
        .padding(18)
        .frame(minWidth: 900, idealWidth: 980, minHeight: 560, idealHeight: 640)
    }

    private var headerView: some View {
        HStack(alignment: .top, spacing: 16) {
            AsyncImage(url: info.thumbnail) { image in
                image.resizable().aspectRatio(contentMode: .fill)
            } placeholder: {
                Rectangle().fill(.quaternary)
                    .overlay(Image(systemName: info.isPlaylist ? "music.note.list" : "film").foregroundStyle(.tertiary))
            }
            .frame(width: 176, height: 99)
            .clipShape(RoundedRectangle(cornerRadius: 8))

            VStack(alignment: .leading, spacing: 6) {
                Text(info.title)
                    .font(.title3.weight(.semibold))
                    .lineLimit(2)
                    .textSelection(.enabled)
                if !info.uploader.isEmpty {
                    Text(info.uploader).foregroundStyle(.secondary)
                }
                HStack(spacing: 14) {
                    if !info.duration.isEmpty { Label(info.duration, systemImage: "clock") }
                    if !info.chapters.isEmpty { Label("\(info.chapters.count) chapters", systemImage: "list.bullet") }
                    if !info.isPlaylist { Label("\(info.formats.count) formats", systemImage: "square.stack.3d.up") }
                }
                .font(.callout)
                .foregroundStyle(.secondary)
            }
            Spacer()
        }
    }
}
