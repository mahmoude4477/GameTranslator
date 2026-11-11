#!/bin/sh
# SPDX-License-Identifier: AGPL-3.0-only
# يجمع تطبيق تلفزيون Samsung تحت dist/tv-tizen/: الملفات المشتركة من tv/webos، والخاصة بـ Tizen
# من tv/tizen. لتثبيته على التلفزيون يجب توقيعه بشهادة Samsung (docs/tv-samsung.md).
# إذا أُعطي TIZEN_PROFILE ينتج أيضًا ملف .wgt موقَّعًا بأداة `tizen` من Tizen Studio.
set -e
cd "$(dirname "$0")/.."
OUT=dist/tv-tizen/app
rm -rf dist/tv-tizen
mkdir -p "$OUT"
cp tv/webos/app.js tv/webos/i18n.js tv/webos/config.js tv/webos/style.css tv/webos/largeIcon.png "$OUT/"
cp tv/webos/icon.png "$OUT/panel-icon.png"
cp tv/tizen/platform.js tv/tizen/config.xml tv/tizen/icon.png "$OUT/"
# الأيقونة الصغيرة في عنوان اللوحة لا يمكنها استخدام الاسم icon.png كما في webOS: أيقونة Tizen كبيرة.
# واجهات منتجات Samsung (webapis.appcommon لإيقاف شاشة التوقف) تُحمَّل قبل platform.js.
sed -e 's/src="icon.png"/src="panel-icon.png"/' \
    -e 's|  <script src="platform.js"></script>|  <script src="$WEBAPIS/webapis/webapis.js"></script>\n  <script src="platform.js"></script>|' \
    tv/webos/index.html > "$OUT/index.html"
# الحزمة غير الموقَّعة المضمَّنة في تطبيقات سطح المكتب: يوقّعها GameTranslator عند التثبيت بشهادة خاصة بالتلفزيون.
VERSION=$(sed -n 's/.* version="\([0-9.]*\)" viewmodes.*/\1/p' tv/tizen/config.xml | head -1)
mkdir -p dist/tv-app
rm -f dist/tv-app/gametranslator-samsung_*.wgt
PY=$(command -v python3 || command -v python)
"$PY" - "$OUT" "dist/tv-app/gametranslator-samsung_$VERSION.wgt" <<'PYEOF'
import os, sys, zipfile
src, dst = sys.argv[1], sys.argv[2]
with zipfile.ZipFile(dst, 'w', zipfile.ZIP_DEFLATED) as z:
    for root, _, files in os.walk(src):
        for name in sorted(files):
            path = os.path.join(root, name)
            z.write(path, os.path.relpath(path, src).replace(os.sep, '/'))
PYEOF
echo "الحزمة غير الموقَّعة: dist/tv-app/gametranslator-samsung_$VERSION.wgt"
if [ -n "$TIZEN_PROFILE" ]; then
  tizen package -t wgt -s "$TIZEN_PROFILE" -o dist/tv-tizen -- "$OUT"
  ls dist/tv-tizen/*.wgt
else
  echo "المصدر غير الموقَّع جاهز: $OUT (للتوقيع حدِّد TIZEN_PROFILE)"
fi
