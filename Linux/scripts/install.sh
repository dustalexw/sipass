#!/usr/bin/env bash
# Adds YT-DLP Studio to the applications menu for the current user (no root needed).
#   ./install.sh             create the menu entry and icon
#   ./install.sh --uninstall remove them (the app folder itself is left alone)
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
data="${XDG_DATA_HOME:-$HOME/.local/share}"
desktop="$data/applications/ytdlp-studio.desktop"; icon="$data/icons/hicolor/256x256/apps/ytdlp-studio.png"
if [[ "${1:-}" == "--uninstall" ]]; then
  rm -f "$desktop" "$icon"; echo "Removed the YT-DLP Studio menu entry."; exit 0
fi
mkdir -p "$(dirname "$desktop")" "$(dirname "$icon")"
cp "$here/ytdlp-studio.png" "$icon"
cat > "$desktop" <<DESKTOP
[Desktop Entry]
Type=Application
Name=YT-DLP Studio
Comment=Download video and audio with yt-dlp
Exec="$here/YTDLPStudio"
Icon=ytdlp-studio
Terminal=false
Categories=AudioVideo;Network;
StartupWMClass=YTDLPStudio
DESKTOP
command -v update-desktop-database >/dev/null && update-desktop-database "$data/applications" >/dev/null 2>&1 || true
echo "Added YT-DLP Studio to your applications menu."
