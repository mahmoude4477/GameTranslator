#!/usr/bin/env bash
# SPDX-License-Identifier: AGPL-3.0-only
# Türkçe dublajın ses motoru: Kyutai'nin pocket-tts programı (MIT, https://github.com/kyutai-labs/pocket-tts).
# Sabit sürüm indirilir, özeti denetlenir: <çıktı>/pocket-tts (Mac, Apple Silicon) ya da pocket-tts.exe (Windows).
# Kullanım: fetch-pocket-tts.sh [mac|windows] [çıktı klasörü, varsayılan dist/tts]
# Mac'te ve Windows'taki Git Bash'te (GitHub Actions) çalışır.
set -euo pipefail
cd "$(dirname "$0")/.."

RELEASE=v4.0.1
case "${1:-mac}" in
  mac)
    ASSET=pocket-tts-3.3.0-aarch64-apple-darwin.tar.gz
    SHA=ee9ca6c7c000ee72c5846e3a8ba01d08f56636d94a614d2259c3f12b8fd69afa
    NAME=pocket-tts ;;
  windows)
    ASSET=pocket-tts-3.3.0-x86_64-pc-windows-msvc.zip
    SHA=98753041f2e65744b989776f87bccb93f1f574a08afe1890c5427ec94cd6309c
    NAME=pocket-tts.exe ;;
  *) echo "Kullanım: $0 [mac|windows] [çıktı klasörü]"; exit 1 ;;
esac

OUT=${2:-dist/tts}
WORK=$(mktemp -d)
trap 'rm -rf "$WORK"' EXIT
curl -sfL -o "$WORK/$ASSET" "https://github.com/kyutai-labs/pocket-tts/releases/download/$RELEASE/$ASSET"
if command -v sha256sum > /dev/null; then ACTUAL=$(sha256sum "$WORK/$ASSET" | cut -d' ' -f1)
else ACTUAL=$(shasum -a 256 "$WORK/$ASSET" | cut -d' ' -f1); fi
[[ "$ACTUAL" == "$SHA" ]] || { echo "pocket-tts özeti tutmadı: $ACTUAL"; exit 1; }
case "$ASSET" in
  *.zip) unzip -q -o "$WORK/$ASSET" -d "$WORK/x" ;;
  *) mkdir -p "$WORK/x" && tar xzf "$WORK/$ASSET" -C "$WORK/x" ;;
esac
FOUND=$(find "$WORK/x" -type f -name "$NAME" | head -1)
[[ -n "$FOUND" ]] || { echo "$NAME pakette bulunamadı."; exit 1; }
mkdir -p "$OUT"
rm -f "$OUT/$NAME"
mv "$FOUND" "$OUT/$NAME"
chmod +x "$OUT/$NAME"
echo "Hazır: $OUT/$NAME ($(du -h "$OUT/$NAME" | cut -f1))"
