#!/bin/bash
set -e
DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"
echo "🎮 GameTranslator macOS derleniyor..."
swiftc -O -parse-as-library -target arm64-apple-macos14.0 "$DIR/NativeApp.swift" -o /tmp/GameTranslator_bin

CERT="Apple Development: 21doruksari21@gmail.com (SQH68S5G9V)"

if [ -d "/Applications/GameTranslator.app" ]; then
    cp /tmp/GameTranslator_bin /Applications/GameTranslator.app/Contents/MacOS/GameTranslator
    codesign --force --sign "$CERT" /Applications/GameTranslator.app/Contents/MacOS/GameTranslator 2>/dev/null || codesign --force --sign - /Applications/GameTranslator.app/Contents/MacOS/GameTranslator
fi
if [ -d "$DIR/../dist/GameTranslator.app" ]; then
    cp /tmp/GameTranslator_bin "$DIR/../dist/GameTranslator.app/Contents/MacOS/GameTranslator"
    codesign --force --sign "$CERT" "$DIR/../dist/GameTranslator.app/Contents/MacOS/GameTranslator" 2>/dev/null || codesign --force --sign - "$DIR/../dist/GameTranslator.app/Contents/MacOS/GameTranslator"
fi
echo "✅ Derleme tamamlandı!"