#!/bin/sh
# SPDX-License-Identifier: AGPL-3.0-only
# Android TV APK'sını masaüstü uygulamalarına gömülecek adla dist/tv-app altına kopyalar:
# gametranslator-androidtv_<sürüm>.apk (sürüm tv-android/app/build.gradle'daki versionName).
# Kullanım: bundle-android-apk.sh <apk>
set -e
cd "$(dirname "$0")/.."
[ -f "$1" ] || { echo "APK yok: $1"; exit 1; }
VERSION=$(sed -n "s/.*versionName '\([0-9.]*\)'.*/\1/p" tv-android/app/build.gradle | head -1)
mkdir -p dist/tv-app
rm -f dist/tv-app/gametranslator-androidtv_*.apk
cp "$1" "dist/tv-app/gametranslator-androidtv_$VERSION.apk"
echo "Gömülecek APK: dist/tv-app/gametranslator-androidtv_$VERSION.apk"
