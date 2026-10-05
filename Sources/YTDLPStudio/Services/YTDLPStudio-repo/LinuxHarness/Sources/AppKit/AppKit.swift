// Linux stand-in for the three AppKit calls used by DownloadManager.
import Foundation
public final class NSWorkspace {
    public static let shared = NSWorkspace()
    public func open(_ url: URL) {}
    public func activateFileViewerSelecting(_ urls: [URL]) {}
}
public final class NSSound {
    public init?(named: String) { return nil }
    public func play() {}
}

// macOS-only Foundation API; emulate by moving into a temp "Trash".
extension FileManager {
    public func trashItem(at url: URL, resultingItemURL: UnsafeMutablePointer<NSURL?>?) throws {
        let trash = URL(fileURLWithPath: NSTemporaryDirectory()).appendingPathComponent("Trash")
        try createDirectory(at: trash, withIntermediateDirectories: true)
        let dest = trash.appendingPathComponent(UUID().uuidString + "-" + url.lastPathComponent)
        try moveItem(at: url, to: dest)
    }
}
