import SwiftUI
import AppKit

struct SettingsView: View {
    @EnvironmentObject private var tools: ToolLocator
    @AppStorage("maxConcurrent") private var maxConcurrent = 2
    @AppStorage("playSound") private var playSound = true
    @AppStorage("appearance") private var appearance: AppAppearance = .system
    @State private var updateOutput = ""
    @State private var updating = false

    var body: some View {
        Form {
            Section("Appearance") {
                HStack(spacing: 12) {
                    ForEach(AppAppearance.allCases) { option in
                        AppearanceOption(option: option, selected: appearance == option) {
                            appearance = option
                        }
                    }
                }
                .padding(.vertical, 4)
            }

            Section("yt-dlp") {
                LabeledContent("Using", value: tools.ytdlpPath ?? "Not found")
                LabeledContent("Version", value: tools.ytdlpVersion)
                HStack {
                    TextField("Custom location", text: $tools.ytdlpOverride, prompt: Text("Auto-detect"))
                        .onSubmit { tools.refresh() }
                    Button("Browse…") {
                        if let path = pickExecutable() { tools.ytdlpOverride = path; tools.refresh() }
                    }
                }
                HStack {
                    Button("Update yt-dlp") {
                        updating = true
                        updateOutput = ""
                        Task { @MainActor in
                            updateOutput = await tools.updateYtDlp()
                            updating = false
                        }
                    }
                    .disabled(tools.ytdlpPath == nil || updating)
                    if updating { ProgressView().controlSize(.small) }
                }
                if !updateOutput.isEmpty {
                    Text(updateOutput)
                        .font(.system(.caption, design: .monospaced))
                        .textSelection(.enabled)
                        .foregroundStyle(.secondary)
                }
                Hint("Sites change often. If downloads start failing, update yt-dlp first. Homebrew installs: run `brew upgrade yt-dlp`.")
            }

            Section("FFmpeg") {
                LabeledContent("Using", value: tools.ffmpegPath ?? "Not found")
                LabeledContent("Version", value: tools.ffmpegVersion)
                HStack {
                    TextField("Custom location", text: $tools.ffmpegOverride, prompt: Text("Auto-detect"))
                        .onSubmit { tools.refresh() }
                    Button("Browse…") {
                        if let path = pickExecutable() { tools.ffmpegOverride = path; tools.refresh() }
                    }
                }
            }

            Section("Queue") {
                Stepper("Simultaneous downloads: \(maxConcurrent)", value: $maxConcurrent, in: 1...6)
                Toggle("Play a sound when a download finishes", isOn: $playSound)
            }

            Section {
                HStack {
                    Button("Scan for tools again") { tools.refresh() }
                    Spacer()
                    Text("Install both with: brew install yt-dlp ffmpeg")
                        .font(.system(.caption, design: .monospaced))
                        .foregroundStyle(.secondary)
                        .textSelection(.enabled)
                }
            }
        }
        .formStyle(.grouped)
        .scrollContentBackground(.hidden)
        .background(CosmicBackground())
        .frame(width: 600, height: 680)
        .onChange(of: appearance) { $0.apply() }
    }

    private func pickExecutable() -> String? {
        let panel = NSOpenPanel()
        panel.canChooseFiles = true
        panel.canChooseDirectories = false
        panel.allowsMultipleSelection = false
        panel.showsHiddenFiles = true
        panel.directoryURL = URL(fileURLWithPath: "/opt/homebrew/bin")
        return panel.runModal() == .OK ? panel.url?.path : nil
    }
}

/// A clickable preview card showing a miniature of the window in that appearance.
private struct AppearanceOption: View {
    let option: AppAppearance
    let selected: Bool
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            VStack(spacing: 8) {
                preview
                    .frame(height: 64)
                    .clipShape(RoundedRectangle(cornerRadius: 9, style: .continuous))
                    .overlay(
                        RoundedRectangle(cornerRadius: 9, style: .continuous)
                            .strokeBorder(selected ? AnyShapeStyle(Theme.ribbonDiagonal) : AnyShapeStyle(Theme.hairline),
                                          lineWidth: selected ? 2 : 1)
                    )
                    .shadow(color: selected ? Theme.violet.opacity(0.35) : .clear, radius: 6)
                Label(option.label, systemImage: option.symbol)
                    .font(.system(size: 12, weight: selected ? .semibold : .regular))
                    .foregroundStyle(selected ? .primary : .secondary)
            }
            .frame(maxWidth: .infinity)
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
    }

    @ViewBuilder private var preview: some View {
        switch option {
        case .light: mini(dark: false)
        case .dark: mini(dark: true)
        case .system:
            HStack(spacing: 0) {
                mini(dark: false)
                mini(dark: true)
            }
        }
    }

    private func mini(dark: Bool) -> some View {
        let bg: [Color] = dark
            ? [Color(red: 0.13, green: 0.08, blue: 0.28), Color(red: 0.03, green: 0.03, blue: 0.10)]
            : [Color(red: 0.97, green: 0.94, blue: 1.00), Color(red: 0.90, green: 0.94, blue: 1.00)]
        let ink = dark ? Color.white.opacity(0.12) : Color.black.opacity(0.08)
        return ZStack(alignment: .topLeading) {
            LinearGradient(colors: bg, startPoint: .topLeading, endPoint: .bottomTrailing)
            VStack(alignment: .leading, spacing: 5) {
                HStack(spacing: 4) {
                    Capsule().fill(ink).frame(height: 7)
                    Capsule().fill(Theme.play).frame(width: 16, height: 7)
                }
                HStack(spacing: 4) {
                    RoundedRectangle(cornerRadius: 3).fill(ink)
                    RoundedRectangle(cornerRadius: 3).fill(ink).frame(width: 22)
                }
                Capsule().fill(Theme.ribbon).frame(width: 30, height: 3)
            }
            .padding(8)
        }
    }
}
