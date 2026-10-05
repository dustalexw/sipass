import SwiftUI
import AppKit

enum OptionCategory: String, CaseIterable, Identifiable {
    case format, audio, encode, chapters, subtitles, metadata, trim, playlist, network, output, advanced
    var id: Self { self }
    var title: String {
        switch self {
        case .format: return "Format & Quality"
        case .audio: return "Audio"
        case .encode: return "FFmpeg Encode"
        case .chapters: return "Chapters & SponsorBlock"
        case .subtitles: return "Subtitles"
        case .metadata: return "Metadata & Thumbnails"
        case .trim: return "Trim"
        case .playlist: return "Playlist"
        case .network: return "Network & Login"
        case .output: return "Output"
        case .advanced: return "Advanced"
        }
    }
    var symbol: String {
        switch self {
        case .format: return "film.stack"
        case .audio: return "waveform"
        case .encode: return "wand.and.stars"
        case .chapters: return "list.bullet.rectangle"
        case .subtitles: return "captions.bubble"
        case .metadata: return "tag"
        case .trim: return "scissors"
        case .playlist: return "music.note.list"
        case .network: return "network"
        case .output: return "folder"
        case .advanced: return "terminal"
        }
    }
    func isCustomized(_ o: DownloadOptions) -> Bool {
        let d = DownloadOptions()
        switch self {
        case .format: return !o.customFormat.isEmpty || !o.customSort.isEmpty || o.maxResolution != .best || o.videoCodec != .any
        case .audio: return o.audioFiltersActive
        case .encode: return o.encodeEnabled || !o.customPPA.isEmpty
        case .chapters: return o.chapterSource != .youtube || o.splitChapters || o.sponsorBlockMode != .off || !o.removeChaptersRegex.isEmpty
        case .subtitles: return o.writeSubs || o.writeAutoSubs || o.embedSubs
        case .metadata: return o.writeThumbnail || o.writeInfoJSON || o.writeDescription || o.writeComments
        case .trim: return o.trimEnabled
        case .playlist: return o.playlistMode != .auto || !o.playlistItems.isEmpty || o.useArchive
        case .network: return !o.rateLimit.isEmpty || !o.proxy.isEmpty || o.cookieBrowser != .none || o.sleepInterval > 0
        case .output: return o.filenameTemplate != d.filenameTemplate || o.restrictFilenames
        case .advanced: return !o.extraArgs.trimmed.isEmpty
        }
    }
}

struct ContentView: View {
    @EnvironmentObject private var store: OptionsStore
    @EnvironmentObject private var tools: ToolLocator
    @EnvironmentObject private var downloads: DownloadManager

    @StateObject private var commentSearch = CommentChapterSearch()
    @State private var commentSelections: [String: String] = [:]
    @State private var commentSelectionSummary: String?
    @State private var urlText = ""
    @State private var category: OptionCategory? = .format
    @State private var inspecting: MediaInfo?
    @State private var isFetching = false
    @State private var errorMessage: String?
    @State private var showSavePreset = false
    @State private var newPresetName = ""

    private var urls: [String] {
        urlText.components(separatedBy: .newlines).map(\.trimmed).filter { !$0.isEmpty }
    }

    var body: some View {
        VStack(spacing: 0) {
            if tools.ytdlpPath == nil || tools.ffmpegPath == nil { toolBanner }
            header
            Divider()
            HSplitView {
                optionsPane
                    .frame(minWidth: 620, idealWidth: 700)
                QueueView()
                    .frame(minWidth: 340, idealWidth: 420)
            }
            Divider()
            if store.options.chapterSource != .youtube {
                Text("Comment chapters are prepared by the app before downloading. The command below shows download options only.")
                    .font(.caption).foregroundStyle(.secondary).padding(.horizontal, 12).padding(.top, 6)
            }
            CommandPreviewBar(command: CommandBuilder.displayCommand(for: store.options, urls: urls))
        }
        .sheet(item: $inspecting) { info in
            FormatInspector(info: info,
                            onUseFormat: { store.options.customFormat = $0 },
                            onDownload: { startDownload() })
        }
        .sheet(item: $commentSearch.preview) { preview in
            CommentChapterPicker(preview: preview) { commentID in
                commentSelections[preview.videoID] = commentID
                if let candidate = preview.candidates.first(where: { $0.id == commentID }) {
                    commentSelectionSummary = "Selected \(candidate.chapters.count) chapters from \(candidate.author) for \(preview.title)."
                }
            }
        }
        .onChange(of: commentSearch.error) { message in
            if let message { errorMessage = message }
        }
        .onDisappear { commentSearch.cancel() }
        .alert("Something went wrong", isPresented: Binding(get: { errorMessage != nil }, set: { if !$0 { errorMessage = nil } })) {
            Button("OK", role: .cancel) {}
        } message: {
            Text(errorMessage ?? "")
        }
        .alert("Save preset", isPresented: $showSavePreset) {
            TextField("Preset name", text: $newPresetName)
            Button("Save") { store.saveCurrent(as: newPresetName) }
            Button("Cancel", role: .cancel) {}
        } message: {
            Text("Saves every option except the output folder.")
        }
    }

    // MARK: Header

    private var header: some View {
        VStack(spacing: 10) {
            HStack(alignment: .top, spacing: 10) {
                ZStack(alignment: .topLeading) {
                    TextEditor(text: $urlText)
                        .font(.system(.body, design: .monospaced))
                        .scrollContentBackground(.hidden)
                        .padding(6)
                    if urlText.isEmpty {
                        Text("Paste one or more links, one per line")
                            .foregroundStyle(.tertiary)
                            .padding(.horizontal, 11)
                            .padding(.vertical, 6)
                            .allowsHitTesting(false)
                    }
                }
                .frame(height: 66)
                .background(RoundedRectangle(cornerRadius: 8).fill(Color(nsColor: .textBackgroundColor)))
                .overlay(RoundedRectangle(cornerRadius: 8).stroke(Color(nsColor: .separatorColor)))

                VStack(spacing: 6) {
                    Button {
                        if let s = NSPasteboard.general.string(forType: .string) {
                            urlText = urlText.trimmed.isEmpty ? s.trimmed : urlText.trimmed + "\n" + s.trimmed
                        }
                    } label: {
                        Label("Paste", systemImage: "doc.on.clipboard").frame(width: 92)
                    }
                    Button(action: analyze) {
                        HStack(spacing: 6) {
                            if isFetching { ProgressView().controlSize(.small) }
                            else { Image(systemName: "magnifyingglass") }
                            Text("Analyze")
                        }
                        .frame(width: 92)
                    }
                    .disabled(urls.isEmpty || isFetching || tools.ytdlpPath == nil)
                    .keyboardShortcut("i", modifiers: .command)
                    .help("List available formats, chapters and playlist items (⌘I)")
                }
            }

            HStack(spacing: 12) {
                Picker("Mode", selection: $store.options.mode) {
                    ForEach(DownloadMode.allCases) { Text($0.label).tag($0) }
                }
                .pickerStyle(.segmented)
                .labelsHidden()
                .frame(width: 340)

                summaryChip

                Spacer()

                presetsMenu

                Button(action: startDownload) {
                    Label(urls.count > 1 ? "Download \(urls.count)" : "Download", systemImage: "arrow.down.circle.fill")
                        .padding(.horizontal, 6)
                }
                .buttonStyle(.borderedProminent)
                .controlSize(.large)
                .disabled(urls.isEmpty)
                .keyboardShortcut(.return, modifiers: .command)
                .help("Add to queue (⌘↩)")
            }
        }
        .padding(12)
    }

    private var summaryChip: some View {
        let o = store.options
        let text: String
        switch o.mode {
        case .audio:
            text = o.audioFormat == .best ? "Original audio" :
                "\(o.audioFormat.rawValue.uppercased())\(o.audioFormat.isLossless ? "" : " · " + o.audioQuality.label)"
        case .video, .videoOnly:
            var parts = [o.container.rawValue.uppercased(), o.maxResolution == .best ? "Best" : o.maxResolution.label]
            if o.encodeEnabled { parts.append("re-encode") }
            text = parts.joined(separator: " · ")
        }
        return Text(text)
            .font(.callout)
            .foregroundStyle(.secondary)
            .lineLimit(1)
    }

    private var presetsMenu: some View {
        Menu {
            Section("Built-in") {
                ForEach(OptionsStore.builtInPresets) { p in
                    Button(p.name) { store.apply(p) }
                }
            }
            if !store.userPresets.isEmpty {
                Section("My presets") {
                    ForEach(store.userPresets) { p in
                        Button(p.name) { store.apply(p) }
                    }
                }
                Menu("Delete preset") {
                    ForEach(store.userPresets) { p in
                        Button(p.name, role: .destructive) { store.delete(p) }
                    }
                }
            }
            Divider()
            Button("Save current settings as preset…") {
                newPresetName = ""
                showSavePreset = true
            }
            Button("Reset all options") { store.reset() }
        } label: {
            Label("Presets", systemImage: "slider.horizontal.3")
        }
        .fixedSize()
    }

    private var toolBanner: some View {
        HStack(spacing: 10) {
            Image(systemName: "exclamationmark.triangle.fill").foregroundStyle(.yellow)
            Text(tools.ytdlpPath == nil
                 ? "yt-dlp isn't installed or couldn't be found. Run `brew install yt-dlp ffmpeg` in Terminal, or set its location in Settings."
                 : "FFmpeg wasn't found. Merging, audio conversion and encoding need it. Run `brew install ffmpeg`, or set its location in Settings.")
                .font(.callout)
            Spacer()
            Button("Scan again") { tools.refresh() }
            Button("Settings…") { NSApp.sendAction(Selector(("showSettingsWindow:")), to: nil, from: nil) }
        }
        .padding(.horizontal, 12)
        .padding(.vertical, 8)
        .background(Color.yellow.opacity(0.15))
    }

    // MARK: Options pane

    private var optionsPane: some View {
        HStack(spacing: 0) {
            List(OptionCategory.allCases, selection: $category) { c in
                HStack {
                    Label(c.title, systemImage: c.symbol)
                    Spacer()
                    if c.isCustomized(store.options) {
                        Circle().fill(Color.accentColor).frame(width: 6, height: 6)
                    }
                }
                .tag(c)
            }
            .listStyle(.sidebar)
            .frame(width: 215)

            Divider()

            Group {
                switch category ?? .format {
                case .format: FormatOptionsView(o: $store.options)
                case .audio: AudioOptionsView(o: $store.options)
                case .encode: EncodeOptionsView(o: $store.options)
                case .chapters:
                    ChaptersOptionsView(o: $store.options,
                        onFindComments: {
                            guard let url = urls.first, let snapshot = tools.snapshot() else { return }
                            commentSearch.search(url: url, options: store.options, tools: snapshot)
                        }, onCancelSearch: { commentSearch.cancel() },
                        canSearchComments: !urls.isEmpty && tools.ytdlpPath != nil,
                        searchingComments: commentSearch.isSearching,
                        selectedCommentSummary: commentSelectionSummary)
                case .subtitles: SubtitleOptionsView(o: $store.options)
                case .metadata: MetadataOptionsView(o: $store.options)
                case .trim: TrimOptionsView(o: $store.options)
                case .playlist: PlaylistOptionsView(o: $store.options)
                case .network: NetworkOptionsView(o: $store.options)
                case .output: OutputOptionsView(o: $store.options)
                case .advanced: AdvancedOptionsView(o: $store.options)
                }
            }
            .toggleStyle(.checkbox)
            .frame(maxWidth: .infinity, maxHeight: .infinity)
        }
    }

    // MARK: Actions

    private func startDownload() {
        let list = urls
        guard !list.isEmpty else { return }
        guard let snapshot = tools.snapshot() else {
            errorMessage = "yt-dlp wasn't found. Install it with `brew install yt-dlp`, or set its path in Settings."
            return
        }
        downloads.enqueue(urls: list, options: store.options, tools: snapshot, commentSelections: commentSelections)
        urlText = ""
    }

    private func analyze() {
        guard let first = urls.first, let snapshot = tools.snapshot() else { return }
        isFetching = true
        let browser = store.options.cookieBrowser
        Task { @MainActor in
            do {
                inspecting = try await MetadataFetcher.fetch(url: first, tools: snapshot, cookieBrowser: browser)
            } catch {
                errorMessage = error.localizedDescription
            }
            isFetching = false
        }
    }
}

struct CommandPreviewBar: View {
    let command: String
    @State private var copied = false

    var body: some View {
        HStack(spacing: 10) {
            Image(systemName: "terminal").foregroundStyle(.secondary)
            ScrollView(.horizontal, showsIndicators: false) {
                Text(command)
                    .font(.system(.caption, design: .monospaced))
                    .textSelection(.enabled)
                    .lineLimit(1)
                    .fixedSize()
            }
            Button(copied ? "Copied" : "Copy command") {
                NSPasteboard.general.clearContents()
                NSPasteboard.general.setString(command, forType: .string)
                copied = true
                DispatchQueue.main.asyncAfter(deadline: .now() + 1.5) { copied = false }
            }
            .controlSize(.small)
        }
        .padding(.horizontal, 12)
        .padding(.vertical, 8)
        .background(.bar)
    }
}
