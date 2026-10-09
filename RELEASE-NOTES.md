Thumbnails in the queue, a new sidebar look and a redesigned About page.

### What's new in 2.4.0
- **Thumbnails in the queue (macOS).** When a download finishes, its row shows a thumbnail of the saved file with the check badge over it. Nothing appears until the download has completed.
- **A new sidebar selection (macOS).** The selected category uses the app's violet-blue gradient and stays on-palette when the window is inactive, instead of turning system gray.
- **A more detailed About page.** On macOS, **Sipass → About Sipass** opens a window with the icon, version, live yt-dlp and FFmpeg versions, and links. On Windows and Linux the About section in Settings is now a card with the version, bundled tools and a Source on GitHub button (new in 2.3.1).

Downloading and processing are unchanged from 2.3.1. Your settings and presets carry over.

### macOS: download and start
Download **Sipass-2.4.0-macOS.zip**, unzip it, and move **Sipass.app** to Applications. You can delete the old YT-DLP Studio.app. Universal app for Apple Silicon and Intel Macs, macOS 13 or later. It is ad-hoc signed, so the first time, right-click the app and choose **Open**. Requires yt-dlp and FFmpeg: `brew install yt-dlp ffmpeg`.

### Windows: download and start
Download **Sipass-2.4.0-Windows-x64.zip**, extract the entire ZIP, and open **Sipass.exe** inside the **Sipass** folder. You can delete the old YT-DLP Studio folder. Supports Windows 10 22H2 and Windows 11 on x64 PCs. Keep the executable alongside its bundled files.

### Linux: download and start
Download **Sipass-2.4.0-Linux-x64.tar.gz**, extract it, and run **Sipass** inside the **Sipass** folder. Run `./install.sh` once to add it to your applications menu. For x64 desktop distributions under X11 or XWayland. .NET, yt-dlp, FFmpeg, ffprobe and Deno are bundled.

### Verification
Each platform build runs the shared test suite against real yt-dlp and FFmpeg processing (chapter metadata and track tags, encoding, trimming, normalization, cancellation and retry), launches the interface, and saves screenshots of both appearances on Windows and Linux. The macOS build also verifies the universal binary, signature and version. Live YouTube extraction remains dependent on YouTube availability, authentication, and upstream yt-dlp support.
