import SwiftUI
import AppKit

struct QueueView: View {
    @EnvironmentObject private var downloads: DownloadManager
    @EnvironmentObject private var store: OptionsStore

    var body: some View {
        VStack(spacing: 0) {
            HStack {
                Text("Queue").font(.headline)
                if !downloads.jobs.isEmpty {
                    Text("\(downloads.jobs.count)")
                        .font(.caption.monospacedDigit())
                        .padding(.horizontal, 6).padding(.vertical, 1)
                        .background(Capsule().fill(Color.secondary.opacity(0.2)))
                }
                Spacer()
                Button {
                    NSWorkspace.shared.open(URL(fileURLWithPath: store.options.outputDirectory))
                } label: { Image(systemName: "folder") }
                .help("Open download folder")
                Button("Clear finished") { downloads.clearFinished() }
                    .disabled(!downloads.jobs.contains { $0.status.isDone })
            }
            .buttonStyle(.borderless)
            .padding(.horizontal, 12)
            .padding(.vertical, 10)

            Divider()

            if downloads.jobs.isEmpty {
                VStack(spacing: 10) {
                    Image(systemName: "arrow.down.circle.dotted")
                        .font(.system(size: 42, weight: .light))
                        .foregroundStyle(.tertiary)
                    Text("Nothing queued")
                        .font(.title3)
                    Text("Paste a link above and press Download.\nAnalyze shows every available format first.")
                        .multilineTextAlignment(.center)
                        .font(.callout)
                        .foregroundStyle(.secondary)
                }
                .frame(maxWidth: .infinity, maxHeight: .infinity)
            } else {
                List {
                    ForEach(downloads.jobs) { job in
                        JobRow(job: job)
                    }
                }
                .listStyle(.inset)
            }
        }
    }
}

struct JobRow: View {
    @ObservedObject var job: DownloadJob
    @EnvironmentObject private var downloads: DownloadManager
    @State private var showLog = false

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack(alignment: .top, spacing: 10) {
                statusIcon
                    .font(.title3)
                    .frame(width: 22)
                VStack(alignment: .leading, spacing: 2) {
                    Text(job.displayTitle)
                        .font(.callout.weight(.medium))
                        .lineLimit(1)
                        .truncationMode(.middle)
                    Text(job.status.label)
                        .font(.caption)
                        .foregroundStyle(statusColor)
                        .lineLimit(2)
                    if !job.detailLine.isEmpty {
                        Text(job.detailLine)
                            .font(.caption.monospacedDigit())
                            .foregroundStyle(.secondary)
                            .lineLimit(1)
                    }
                }
                Spacer(minLength: 4)
                controls
            }
            progressBar
        }
        .padding(.vertical, 4)
        .popover(isPresented: $showLog, arrowEdge: .leading) {
            LogView(job: job)
        }
        .contextMenu {
            Button("Copy link") {
                NSPasteboard.general.clearContents()
                NSPasteboard.general.setString(job.url, forType: .string)
            }
            Button("Show log") { showLog = true }
            if !job.outputFiles.isEmpty { Button("Show in Finder") { downloads.reveal(job) } }
            Divider()
            if job.status.isActive || job.status == .queued { Button("Cancel") { downloads.cancel(job) } }
            if job.status.isDone { Button("Retry") { downloads.retry(job) } }
            Button("Remove", role: .destructive) { downloads.remove(job) }
        }
    }

    @ViewBuilder private var progressBar: some View {
        switch job.status {
        case .downloading, .encoding:
            ProgressView(value: job.progress)
                .progressViewStyle(.linear)
        case .starting, .processing:
            ProgressView()
                .progressViewStyle(.linear)
        default:
            EmptyView()
        }
    }

    @ViewBuilder private var statusIcon: some View {
        switch job.status {
        case .queued:
            Image(systemName: "clock").foregroundStyle(.secondary)
        case .starting, .downloading:
            Image(systemName: "arrow.down.circle.fill").foregroundStyle(.blue)
        case .processing:
            Image(systemName: "gearshape.2.fill").foregroundStyle(.purple)
        case .encoding:
            Image(systemName: "wand.and.stars").foregroundStyle(.orange)
        case .finished:
            Image(systemName: "checkmark.circle.fill").foregroundStyle(.green)
        case .warning:
            Image(systemName: "exclamationmark.triangle.fill").foregroundStyle(.yellow)
        case .failed:
            Image(systemName: "xmark.octagon.fill").foregroundStyle(.red)
        case .cancelled:
            Image(systemName: "stop.circle").foregroundStyle(.secondary)
        }
    }

    private var statusColor: Color {
        switch job.status {
        case .failed: return .red
        case .warning: return .orange
        case .finished: return .green
        default: return .secondary
        }
    }

    private var controls: some View {
        HStack(spacing: 6) {
            Button { showLog = true } label: { Image(systemName: "doc.text.magnifyingglass") }
                .help("Show log")
            if job.status.isActive || job.status == .queued {
                Button { downloads.cancel(job) } label: { Image(systemName: "xmark.circle.fill") }
                    .help("Cancel")
            } else {
                if !job.outputFiles.isEmpty {
                    Button { downloads.reveal(job) } label: { Image(systemName: "magnifyingglass.circle") }
                        .help("Show in Finder")
                }
                Button { downloads.retry(job) } label: { Image(systemName: "arrow.clockwise.circle") }
                    .help("Retry")
                Button { downloads.remove(job) } label: { Image(systemName: "trash") }
                    .help("Remove from queue")
            }
        }
        .buttonStyle(.borderless)
        .foregroundStyle(.secondary)
    }
}

struct LogView: View {
    @ObservedObject var job: DownloadJob

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack {
                Text(job.displayTitle).font(.headline).lineLimit(1)
                Spacer()
                Button("Copy log") {
                    NSPasteboard.general.clearContents()
                    NSPasteboard.general.setString(job.log.joined(separator: "\n"), forType: .string)
                }
            }
            ScrollViewReader { proxy in
                ScrollView {
                    VStack(alignment: .leading, spacing: 0) {
                        Text(job.log.isEmpty ? "No output yet." : job.log.joined(separator: "\n"))
                            .font(.system(.caption, design: .monospaced))
                            .textSelection(.enabled)
                            .frame(maxWidth: .infinity, alignment: .leading)
                        Color.clear.frame(height: 1).id("end")
                    }
                    .padding(8)
                }
                .background(RoundedRectangle(cornerRadius: 6).fill(Color(nsColor: .textBackgroundColor)))
                .onAppear { proxy.scrollTo("end", anchor: .bottom) }
                .onChange(of: job.log.count) { _ in proxy.scrollTo("end", anchor: .bottom) }
            }
        }
        .padding(12)
        .frame(width: 680, height: 400)
    }
}
