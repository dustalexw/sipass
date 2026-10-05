import SwiftUI
import AppKit

final class AppDelegate: NSObject, NSApplicationDelegate {
    func applicationDidFinishLaunching(_ notification: Notification) {
        // Needed when launched via `swift run` (no bundle); harmless inside the .app.
        NSApp.setActivationPolicy(.regular)
        NSApp.activate(ignoringOtherApps: true)
    }

    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { true }

    func applicationWillTerminate(_ notification: Notification) {
        DownloadManager.current?.cancelAll()
    }
}

@main
struct YTDLPStudioApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) private var appDelegate
    @StateObject private var store = OptionsStore()
    @StateObject private var tools = ToolLocator()
    @StateObject private var downloads = DownloadManager()

    init() {
        UserDefaults.standard.register(defaults: ["maxConcurrent": 2, "playSound": true])
    }

    var body: some Scene {
        WindowGroup("YT-DLP Studio") {
            ContentView()
                .environmentObject(store)
                .environmentObject(tools)
                .environmentObject(downloads)
                .frame(minWidth: 1100, minHeight: 700)
        }
        .commands {
            CommandGroup(replacing: .newItem) {}
        }

        Settings {
            SettingsView()
                .environmentObject(tools)
        }
    }
}
