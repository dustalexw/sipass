// swift-tools-version: 5.9
import PackageDescription
let package = Package(
  name: "LinuxHarness",
  targets: [
    .target(name: "Combine"),
    .target(name: "AppKit"),
    .target(name: "Core", dependencies: ["Combine", "AppKit"]),
    .executableTarget(name: "CoreTests", dependencies: ["Core"]),
  ])
