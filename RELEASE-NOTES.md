YT-DLP Studio 2.2.1 ships a redesigned interface on macOS and Windows, and a new Linux edition.

### What's new in 2.2.1
- **New look on every platform**, taken from the app icon: a deep-space backdrop, frosted panels with an iridescent edge, and a glowing Download button.
- **Compact link field** with paste, analyze and clear buttons built in. It grows only when you paste several links. Press Enter to add to the queue; Shift+Enter (Option-Return on macOS) starts a new line.
- **Neater icons**: coloured tiles for each options page and each download's status, matching round action buttons, and gradient progress bars.
- **Light mode.** Choose System, Light or Dark under **Settings → Appearance** (gear button or Ctrl+, on Windows and Linux; ⌘, on macOS). Switching keeps running downloads going.
- **New: Linux edition** for x64 desktops, with the same features as Windows and bundled yt-dlp, FFmpeg and Deno.

Download features are unchanged from 2.1.1.

### macOS: download and start
Download **YT-DLP-Studio-2.2.1-macOS.zip**, unzip it, and move **YT-DLP Studio.app** to Applications. Universal app for Apple Silicon and Intel Macs, macOS 13 or later. It is ad-hoc signed, so the first time, right-click the app and choose **Open**. Requires yt-dlp and FFmpeg: `brew install yt-dlp ffmpeg`.

### Windows: download and start
Download **YT-DLP-Studio-2.2.1-Windows-x64.zip**, extract the entire ZIP, and open **YTDLPStudio.exe** inside the **YT-DLP Studio** folder. Supports Windows 10 22H2 and Windows 11 on x64 PCs. Keep the executable alongside its bundled files.

### Linux: download and start
Download **YT-DLP-Studio-2.2.1-Linux-x64.tar.gz**, extract it, and run **YTDLPStudio** inside the **YT-DLP Studio** folder. Run `./install.sh` once to add it to your applications menu. For x64 desktop distributions under X11 or XWayland. .NET, yt-dlp, FFmpeg, ffprobe and Deno are bundled.

### Verification
Each platform build runs the shared test suite against real yt-dlp and FFmpeg processing (chapter metadata and track tags, encoding, trimming, normalization, cancellation and retry), launches the interface, and saves screenshots of both appearances on Windows and Linux. The macOS build also verifies the universal binary, signature and version. Live YouTube extraction remains dependent on YouTube availability, authentication, and upstream yt-dlp support.

The 2.2.0 pre-release was a macOS-only preview of this redesign.
