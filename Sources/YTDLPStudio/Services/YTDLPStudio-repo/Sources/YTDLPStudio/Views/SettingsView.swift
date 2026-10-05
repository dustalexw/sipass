import SwiftUI
import AppKit

struct SettingsView: View {
    @EnvironmentObject private var tools: ToolLocator
    @AppStorage("maxConcurrent") private var maxConcurrent = 2
    @AppStorage("playSound") private var playSound = true
    @State private var updateOutput = ""
    @State private var updating = false

    var body: some View {
        Form {
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
        .frame(width: 600, height: 560)
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
