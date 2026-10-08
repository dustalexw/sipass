import SwiftUI
import AppKit

final class AppDelegate: NSObject, NSApplicationDelegate {
    func applicationDidFinishLaunching(_ notification: Notification) {
        // Needed when launched via `swift run` (no bundle); harmless inside the .app.
        NSApp.setActivationPolicy(.regular)
        NSApp.activate(ignoringOtherApps: true)
        AppAppearance(rawValue: UserDefaults.standard.string(forKey: "appearance") ?? "")?.apply()
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
        UserDefaults.standard.register(defaults: ["maxConcurrent": 2, "playSound": true,
                                                  "appearance": AppAppearance.system.rawValue])
    }

    var body: some Scene {
        WindowGroup("Sipass") {
            ContentView()
                .environmentObject(store)
                .environmentObject(tools)
                .environmentObject(downloads)
                .frame(minWidth: 1100, minHeight: 700)
                .tint(Theme.accent)
        }
        .windowStyle(.hiddenTitleBar)
        .commands {
            CommandGroup(replacing: .newItem) {}
        }

        Settings {
            SettingsView()
                .environmentObject(tools)
                .tint(Theme.accent)
        }
    }
}
