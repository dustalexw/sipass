### What's new in 2.1.1
- Fixed clipped and cut-off text, buttons, and queue controls on displays scaled above 100% (common on laptops and Parallels). The window, dialogs, and options now scale with Windows display settings and when moved between monitors.
- Labels containing "&" (such as "Format & quality") display correctly.

YT-DLP Studio now has a native Windows desktop edition with a self-contained portable package.

### Download and start
Download **YT-DLP-Studio-2.1.1-Windows-x64.zip**, extract the entire ZIP, and open **YTDLPStudio.exe** inside the **YT-DLP Studio** folder. Supports Windows 10 22H2 and Windows 11 on x64 PCs. Keep the executable alongside its bundled files.

### Included
- Native Windows interface with download queue, saved presets, format selection, logs, cancellation, and keyboard shortcuts.
- Chapters from YouTube's description, comments, or comments when description chapters are missing; preview and choose a timestamp comment.
- Audio extraction, chapter splitting and music tags, trimming, normalization, subtitles, metadata, thumbnails, playlists, and separate video encoding.
- The psychedelic space icon adapted to Windows.
- Bundled .NET runtime, yt-dlp (including its Python runtime), FFmpeg, ffprobe, and Deno. No separate dependency installation or PATH setup required.
- Version-pinned dependency downloads, checksum verification, license notices, and a packaged file checksum manifest.

### Verification
The Windows build runs media integration checks with the bundled tools, including chapter metadata and track tags, H.264 encoding, audio trimming, normalization, sample rate, cancellation, and retry. It also launches and renders the native interface. Live YouTube extraction remains dependent on YouTube availability, authentication, and upstream yt-dlp support.

This release adds the Windows edition. The existing macOS edition remains available in [v2.0.0](https://github.com/dustalexw/ytdlp-studio/releases/tag/v2.0.0).
