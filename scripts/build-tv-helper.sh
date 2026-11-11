#!/bin/zsh
# SPDX-License-Identifier: AGPL-3.0-only
# يبني بـ Native AOT أداة تثبيت Samsung و Android TV المساعدة المضمَّنة في تطبيق Mac (windows/src/GameTranslator.TvHelper):
# dist/tv-helper/gametranslator-tv-helper. ثم يختبر أن توقيع XML يعمل مع AOT.
set -e
setopt pipefail
cd "${0:A:h}/.."

OUT=dist/tv-helper
rm -rf "$OUT"
dotnet publish windows/src/GameTranslator.TvHelper -c Release -r osx-arm64 -o "$OUT" -nologo -v q
# حتى لا تدخل رموز تصحيح الأخطاء في الحزمة.
rm -rf "$OUT"/*.dSYM(N) "$OUT"/*.dbg(N) "$OUT"/*.pdb(N)
[[ -x "$OUT/gametranslator-tv-helper" ]] || { echo "تعذر بناء الأداة المساعدة."; exit 1; }
# نسخة .NET المبنية من المصدر في Homebrew تربط الملف بمكتبات Homebrew؛ وملف كهذا لا يعمل على Mac آخر.
# بنية الإصدار تُجرى بـ .NET من Microsoft (actions/setup-dotnet). للاختبار المحلي: ALLOW_LOCAL_LIBS=1.
if otool -L "$OUT/gametranslator-tv-helper" | grep -q /opt/homebrew && [[ -z "$ALLOW_LOCAL_LIBS" ]]; then
  echo "الأداة المساعدة مرتبطة بمكتبات Homebrew؛ ابنِها بـ .NET SDK من Microsoft."; exit 1
fi
"$OUT/gametranslator-tv-helper" self-test | grep -q '"event":"done"' || { echo "فشل اختبار الأداة المساعدة."; exit 1; }
echo "جاهز: $OUT/gametranslator-tv-helper ($(du -h "$OUT/gametranslator-tv-helper" | cut -f1))"
