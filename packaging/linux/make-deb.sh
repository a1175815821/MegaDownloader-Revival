#!/usr/bin/env bash
# Builds a Debian package from a published self-contained linux-x64 build.
#
#   ./packaging/linux/make-deb.sh <publish-dir> <output-dir> [version] [arch]
#
# Layout (as the release plan requires):
#   /usr/lib/megadownloader/   published files, including the native bridge
#   /usr/bin/megadownloader    launcher that runs the binary from /usr/lib
#   /usr/share/applications/   menu entry with x-scheme-handler/mega
#   /usr/share/icons/          icon at the hicolor sizes
#
# Depends only on what a self-contained build still needs on Ubuntu 24.04.
set -euo pipefail

publish_dir=${1:?usage: make-deb.sh <publish-dir> <output-dir> [version] [arch]}
output_dir=${2:?usage: make-deb.sh <publish-dir> <output-dir> [version] [arch]}
version=${3:-2.9.0}
arch=${4:-amd64}

script_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
repo_root=$(cd "$script_dir/../.." && pwd)
stage=$(mktemp -d)
trap 'rm -rf "$stage"' EXIT

lib="$stage/usr/lib/megadownloader"
bin="$stage/usr/bin"
share="$stage/usr/share"

mkdir -p "$lib" "$bin" "$share/applications" "$stage/DEBIAN"

cp -R "$publish_dir"/. "$lib/"
chmod +x "$lib/MegaDownloader"
find "$lib" -name "*.so*" -exec chmod 0644 {} +

# A launcher rather than a symlink: the data files sit next to the binary and
# Avalonia resolves them from the executable's own directory.
cat > "$bin/megadownloader" <<'LAUNCHER'
#!/bin/sh
exec /usr/lib/megadownloader/MegaDownloader "$@"
LAUNCHER
chmod 0755 "$bin/megadownloader"

install -Dm0644 "$repo_root/packaging/MegaDownloader.desktop" "$share/applications/MegaDownloader.desktop"
sed -i 's|^Exec=.*|Exec=/usr/bin/megadownloader %u|' "$share/applications/MegaDownloader.desktop"

icon="$repo_root/packaging/icons/megadownloader.png"
if [ -f "$icon" ]; then
  for size in 16 24 32 48 64 128 256; do
    install -Dm0644 "$icon" "$share/icons/hicolor/${size}x${size}/apps/megadownloader.png"
  done
fi

installed_size=$(du -sk "$stage" | cut -f1)
cat > "$stage/DEBIAN/control" <<CONTROL
Package: megadownloader
Version: $version
Section: net
Priority: optional
Architecture: $arch
Maintainer: Mega Downloader maintainers
Installed-Size: $installed_size
Depends: libx11-6, libice6, libsm6, libfontconfig1, libfreetype6, libglib2.0-0t64
Recommends: vlc
Description: Download files and folders from MEGA
 Downloads public MEGA files and folder shares, keeps a resumable queue,
 and can sign in to a MEGA account to browse the cloud drive and watch
 folders for new files.
CONTROL

cat > "$stage/DEBIAN/postinst" <<'POSTINST'
#!/bin/sh
set -e
if command -v update-desktop-database >/dev/null 2>&1; then
  update-desktop-database -q /usr/share/applications || true
fi
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
  gtk-update-icon-cache -q /usr/share/icons/hicolor || true
fi
# The package ships the handler; the association is per user, so set it for the
# account installing the package. A root install with sudo keeps SUDO_USER.
target_user=${SUDO_USER:-$USER}
if command -v xdg-mime >/dev/null 2>&1 && [ -n "$target_user" ] && [ "$target_user" != "root" ]; then
  su -s /bin/sh "$target_user" -c 'xdg-mime default MegaDownloader.desktop x-scheme-handler/mega' || true
fi
POSTINST
chmod 0755 "$stage/DEBIAN/postinst"

cat > "$stage/DEBIAN/postrm" <<'POSTRM'
#!/bin/sh
set -e
if command -v update-desktop-database >/dev/null 2>&1; then
  update-desktop-database -q /usr/share/applications || true
fi
POSTRM
chmod 0755 "$stage/DEBIAN/postrm"

mkdir -p "$output_dir"
deb="$output_dir/megadownloader_${version}_${arch}.deb"
# --root-owner-group makes the archive reproducible from a non-root build host.
dpkg-deb --root-owner-group -Zxz --build "$stage" "$deb"
echo "Built $deb"
dpkg-deb --info "$deb"
