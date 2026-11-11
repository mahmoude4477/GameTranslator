#!/bin/bash
set -e

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"
ROOT_DIR="$( cd "$DIR/.." >/dev/null 2>&1 && pwd )"
APP_DIR="$ROOT_DIR/dist/GameTranslator.app"
MACOS_DIR="$APP_DIR/Contents/MacOS"
RES_DIR="$APP_DIR/Contents/Resources"

echo "=================================================="
echo "      🎮 بناء GameTranslator لنظام macOS"
echo "=================================================="

echo "🔨 جارٍ بناء تطبيق Swift الأصلي..."
swiftc -O -parse-as-library -target arm64-apple-macos14.0 "$DIR/NativeApp.swift" -o /tmp/GameTranslator_bin

echo "📦 جارٍ تجهيز حزمة GameTranslator.app..."
mkdir -p "$MACOS_DIR"
mkdir -p "$RES_DIR"

# 1. الملف التنفيذي الرئيسي
cp /tmp/GameTranslator_bin "$MACOS_DIR/GameTranslator"
chmod +x "$MACOS_DIR/GameTranslator"

# 2. Info.plist
cat << 'EOF' > "$APP_DIR/Contents/Info.plist"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
	<key>CFBundleExecutable</key>
	<string>GameTranslator</string>
	<key>CFBundleIconFile</key>
	<string>AppIcon</string>
	<key>CFBundleIdentifier</key>
	<string>com.gametranslator.app</string>
	<key>CFBundleName</key>
	<string>GameTranslator</string>
	<key>CFBundlePackageType</key>
	<string>APPL</string>
	<key>CFBundleShortVersionString</key>
	<string>1.0.0</string>
	<key>CFBundleVersion</key>
	<string>1</string>
	<key>LSMinimumSystemVersion</key>
	<string>14.0</string>
	<key>NSHighResolutionCapable</key>
	<true/>
	<key>NSScreenCaptureUsageDescription</key>
	<string>يحلّل GameTranslator صورة لعبة PS5 ويترجم الترجمة النصية تلقائيًا إلى العربية.</string>
	<key>NSLocalNetworkUsageDescription</key>
	<string>GameTranslator finds your PS5 and TV on the local network to send translated subtitles.</string>
</dict>
</plist>
EOF

# 3. الأيقونة والصور
if [ -f "$ROOT_DIR/design/AppIcon.icns" ]; then
    cp "$ROOT_DIR/design/AppIcon.icns" "$RES_DIR/AppIcon.icns"
fi

if [ -f "$DIR/Resources/game-frame.png" ]; then
    cp "$DIR/Resources/game-frame.png" "$RES_DIR/game-frame.png"
fi

# 4. حزم التلفزيون (Android TV APK، webOS IPK، Tizen WGT)
mkdir -p "$RES_DIR/tv-app"
if [ -d "$ROOT_DIR/tv/packages" ]; then
    cp -R "$ROOT_DIR/tv/packages/"* "$RES_DIR/tv-app/" 2>/dev/null || true
fi

# 5. السكربتات المساعدة وحزمة إقران PS5
mkdir -p "$RES_DIR/scripts"
if [ -f "$ROOT_DIR/scripts/android-tv-install.py" ]; then
    cp "$ROOT_DIR/scripts/android-tv-install.py" "$RES_DIR/scripts/android-tv-install.py"
    chmod +x "$RES_DIR/scripts/android-tv-install.py"
fi
if [ -f "$ROOT_DIR/scripts/gametranslator-ps5-register" ]; then
    cp "$ROOT_DIR/scripts/gametranslator-ps5-register" "$MACOS_DIR/gametranslator-ps5-register"
    chmod +x "$MACOS_DIR/gametranslator-ps5-register"
fi

# 5b. محرك Chiaki المضمّن لـ Remote Play (Helpers)
mkdir -p "$APP_DIR/Contents/Helpers"
if [ -f "$DIR/Helpers/Chiaki.app.tar.gz" ]; then
    echo "🎮 جارٍ إضافة محرك Chiaki.app المضمّن إلى الحزمة..."
    tar -xzf "$DIR/Helpers/Chiaki.app.tar.gz" -C "$APP_DIR/Contents/Helpers/"
elif [ -d "$DIR/Helpers/Chiaki.app" ]; then
    echo "🎮 جارٍ إضافة محرك Chiaki.app المضمّن إلى الحزمة..."
    cp -R "$DIR/Helpers/Chiaki.app" "$APP_DIR/Contents/Helpers/"
fi

# ضع أداة إقران PS5 داخل Resources
mkdir -p "$RES_DIR/ps5-register"
if [ -d "$ROOT_DIR/scripts/ps5-register" ]; then
    cp -R "$ROOT_DIR/scripts/ps5-register/"* "$RES_DIR/ps5-register/"
fi
if [ -f "$ROOT_DIR/scripts/ps5-register.js" ]; then
    cp "$ROOT_DIR/scripts/ps5-register.js" "$RES_DIR/ps5-register/"
fi

# انسخ الملفات التنفيذية المساعدة (TV Helper) إلى مجلد MacOS
if [ -d "$ROOT_DIR/scripts/bin" ]; then
    for b in "$ROOT_DIR/scripts/bin"/*; do
        if [ -f "$b" ]; then
            cp "$b" "$MACOS_DIR/"
            chmod +x "$MACOS_DIR/$(basename "$b")"
        fi
    done
fi

# 6. توقيع الكود (شهادة Apple Developer أو Ad-hoc)
CERT="Apple Development: 21doruksari21@gmail.com (SQH68S5G9V)"
echo "🔏 جارٍ توقيع الكود..."
for helper in "$MACOS_DIR"/*; do
    if [ "$helper" != "$MACOS_DIR/GameTranslator" ] && [ -f "$helper" ]; then
        codesign --force --sign - "$helper" 2>/dev/null || true
    fi
done

if security find-identity -v -p codesigning 2>/dev/null | grep -q "$CERT"; then
    codesign --force --sign "$CERT" "$MACOS_DIR/GameTranslator" 2>/dev/null || codesign --force --sign - "$MACOS_DIR/GameTranslator"
    codesign --force --deep --sign "$CERT" "$APP_DIR" 2>/dev/null || codesign --force --deep --sign - "$APP_DIR"
else
    codesign --force --deep --sign - "$APP_DIR"
fi

# 7. التثبيت في مجلد /Applications
echo "📲 جارٍ تثبيت /Applications/GameTranslator.app..."
rm -rf /Applications/GameTranslator.app
cp -R "$APP_DIR" /Applications/GameTranslator.app
xattr -dr com.apple.quarantine /Applications/GameTranslator.app 2>/dev/null || true

echo "📦 جارٍ تجهيز أرشيف ZIP للتوزيع..."
mkdir -p "$ROOT_DIR/dist"
cd "$ROOT_DIR/dist"
rm -f "GameTranslator-macOS-v1.0.0.zip"
zip -r -y -q "GameTranslator-macOS-v1.0.0.zip" "GameTranslator.app"

echo "✅ اكتمل البناء والتوقيع والتحزيم بنجاح!"
