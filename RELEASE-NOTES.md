YT-DLP Studio 2.1.1 ships the macOS app and the Windows portable edition together.

### What's new in 2.1.1
- Windows: fixed clipped and cut-off text, buttons, and queue controls on displays scaled above 100% (common on laptops and Parallels). The window, dialogs, and options now scale with Windows display settings and when moved between monitors.
- Windows: labels containing "&" (such as "Format & quality") display correctly.
- macOS: same features as 2.0.0, now built, tested, and packaged by the same verified release workflow as Windows.

### macOS: download and start
Download **YT-DLP-Studio-2.1.1-macOS.zip**, unzip it, and move **YT-DLP Studio.app** to Applications. Universal app for Apple Silicon and Intel Macs, macOS 13 or later. It is ad-hoc signed, so the first time, right-click the app and choose **Open**. Requires yt-dlp and FFmpeg: `brew install yt-dlp ffmpeg`.

### Windows: download and start
Download **YT-DLP-Studio-2.1.1-Windows-x64.zip**, extract the entire ZIP, and open **YTDLPStudio.exe** inside the **YT-DLP Studio** folder. Supports Windows 10 22H2 and Windows 11 on x64 PCs. Keep the executable alongside its bundled files.

### Windows: included
- Native Windows interface with download queue, saved presets, format selection, logs, cancellation, and keyboard shortcuts.
- Chapters from YouTube's description, comments, or comments when description chapters are missing; preview and choose a timestamp comment.
- Audio extraction, chapter splitting and music tags, trimming, normalization, subtitles, metadata, thumbnails, playlists, and separate video encoding.
- The psychedelic space icon adapted to Windows.
- Bundled .NET runtime, yt-dlp (including its Python runtime), FFmpeg, ffprobe, and Deno. No separate dependency installation or PATH setup required.
- Version-pinned dependency downloads, checksum verification, license notices, and a packaged file checksum manifest.

### Verification
The macOS build runs its test suite, including a real yt-dlp/FFmpeg chapter embedding, splitting, and tagging check, and verifies the universal binary, signature, version, and launch. The Windows build runs media integration checks with the bundled tools, including chapter metadata and track tags, H.264 encoding, audio trimming, normalization, sample rate, cancellation, and retry. It also launches and renders the native interface. Live YouTube extraction remains dependent on YouTube availability, authentication, and upstream yt-dlp support.

