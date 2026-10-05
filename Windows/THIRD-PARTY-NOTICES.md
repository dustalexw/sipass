# Bundled programs

The app invokes these programs as separate child processes. Their license texts are included in this directory in the portable distribution. See `dependencies.lock.json` for exact release URLs and archive hashes, and `SHA256-MANIFEST.json` for hashes of every bundled file.

- **yt-dlp 2026.08.19**: [source and release](https://github.com/yt-dlp/yt-dlp/tree/2026.08.19). The standalone Windows binary includes Python, yt-dlp-ejs, and other dependencies; their notices are in `yt-dlp-THIRD-PARTY.txt`.
- **FFmpeg and ffprobe 9.0.2**, Gyan full static Windows build, GPLv3: [FFmpeg source revision](https://github.com/FFmpeg/FFmpeg/commit/946fcce07b), [binary release](https://github.com/GyanD/codexffmpeg/releases/tag/9.0.2), [build information and library list](https://www.gyan.dev/ffmpeg/builds/). The original license, documentation and build README from the upstream archive are retained alongside the programs. The FFmpeg source is GPLv3; included external libraries retain their respective licenses.
- **Deno 2.9.7**, MIT: [source and notices](https://github.com/denoland/deno/tree/v2.9.7).
- **.NET runtime and Windows Forms**, MIT: [runtime source](https://github.com/dotnet/runtime), [Windows Forms source](https://github.com/dotnet/winforms). Included .NET license and third-party notices are preserved in this directory.

The dependencies are unmodified upstream builds. Rebuilding or redistributing them requires retaining their notices and satisfying the licenses of the upstream components. This file is a component inventory, not a change to the app repository's license.
