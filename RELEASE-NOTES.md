YT-DLP Studio is now **Sipass**. Same app, new name, on macOS, Windows and Linux.

### What's new in 2.3.0
- **New name: Sipass.** The app, its window, the logo next to the icon, the program files (`Sipass.app`, `Sipass.exe`, `Sipass`) and the downloads all use the new name.
- **Your settings carry over.** Saved options, presets and your appearance choice are kept when you update from YT-DLP Studio.
- **Linux:** run `./install.sh` again to replace the old "YT-DLP Studio" menu entry with "Sipass".

Features are unchanged from 2.2.1.

### macOS: download and start
Download **Sipass-2.3.0-macOS.zip**, unzip it, and move **Sipass.app** to Applications. You can delete the old YT-DLP Studio.app. Universal app for Apple Silicon and Intel Macs, macOS 13 or later. It is ad-hoc signed, so the first time, right-click the app and choose **Open**. Requires yt-dlp and FFmpeg: `brew install yt-dlp ffmpeg`.

### Windows: download and start
Download **Sipass-2.3.0-Windows-x64.zip**, extract the entire ZIP, and open **Sipass.exe** inside the **Sipass** folder. You can delete the old YT-DLP Studio folder. Supports Windows 10 22H2 and Windows 11 on x64 PCs. Keep the executable alongside its bundled files.

### Linux: download and start
Download **Sipass-2.3.0-Linux-x64.tar.gz**, extract it, and run **Sipass** inside the **Sipass** folder. Run `./install.sh` once to add it to your applications menu. For x64 desktop distributions under X11 or XWayland. .NET, yt-dlp, FFmpeg, ffprobe and Deno are bundled.

### Verification
Each platform build runs the shared test suite against real yt-dlp and FFmpeg processing (chapter metadata and track tags, encoding, trimming, normalization, cancellation and retry), launches the interface, and saves screenshots of both appearances on Windows and Linux. The macOS build also verifies the universal binary, signature and version. Live YouTube extraction remains dependent on YouTube availability, authentication, and upstream yt-dlp support.
