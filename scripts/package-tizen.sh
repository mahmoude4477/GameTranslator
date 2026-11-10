#!/bin/sh
# SPDX-License-Identifier: AGPL-3.0-only
# Samsung TV uygulamasını dist/tv-tizen/ altında toplar: ortak dosyalar tv-webos'tan, Tizen'e özgü olanlar
# tv-tizen'den gelir. TV'ye kurmak için Samsung sertifikasıyla imzalanmalıdır (docs/tv-samsung.md).
# TIZEN_PROFILE verilirse Tizen Studio'nun `tizen` aracıyla imzalı .wgt da üretir.
set -e
cd "$(dirname "$0")/.."
OUT=dist/tv-tizen/app
rm -rf dist/tv-tizen
mkdir -p "$OUT"
cp tv-webos/app.js tv-webos/i18n.js tv-webos/config.js tv-webos/index.html tv-webos/style.css tv-webos/largeIcon.png "$OUT/"
cp tv-webos/icon.png "$OUT/panel-icon.png"
cp tv-tizen/platform.js tv-tizen/config.xml tv-tizen/icon.png "$OUT/"
# Panel başlığındaki küçük simge webOS'taki gibi icon.png adını kullanamaz: Tizen'in simgesi büyüktür.
# Samsung'un ürün API'leri (ekran koruyucuyu kapatmak için webapis.appcommon) platform.js'ten önce yüklenir.
sed -e 's/src="icon.png"/src="panel-icon.png"/' \
    -e 's|  <script src="platform.js"></script>|  <script src="$WEBAPIS/webapis/webapis.js"></script>\n  <script src="platform.js"></script>|' \
    tv-webos/index.html > "$OUT/index.html"
# Masaüstü uygulamalarına gömülen imzasız paket: GameTranslator kurarken TV'ye özel sertifikayla imzalar.
VERSION=$(sed -n 's/.* version="\([0-9.]*\)" viewmodes.*/\1/p' tv-tizen/config.xml | head -1)
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
echo "İmzasız paket: dist/tv-app/gametranslator-samsung_$VERSION.wgt"
if [ -n "$TIZEN_PROFILE" ]; then
  tizen package -t wgt -s "$TIZEN_PROFILE" -o dist/tv-tizen -- "$OUT"
  ls dist/tv-tizen/*.wgt
else
  echo "İmzasız kaynak hazır: $OUT (imza için TIZEN_PROFILE verin)"
fi
