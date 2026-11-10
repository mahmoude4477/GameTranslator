#!/bin/zsh
# SPDX-License-Identifier: AGPL-3.0-only
# chiaki-ng'nin Remote Play kütüphanesini (libchiaki) macOS için statik derler.
# Çıktı: third-party/build/macos/{include,lib}
# Gerekenler: Xcode, Homebrew ile cmake ninja pkg-config protobuf openssl@3 json-c (libevent ve miniupnpc kaynaktan derlenir)
set -e
cd "${0:A:h}/.."

ROOT="$PWD"
SRC="$ROOT/third-party/chiaki-ng"
BUILD="$ROOT/third-party/build/macos-cmake"
OUT="$ROOT/third-party/build/macos"
VENV="$ROOT/third-party/build/venv"
export DEVELOPER_DIR="${DEVELOPER_DIR:-/Applications/Xcode.app/Contents/Developer}"
BREW="$(brew --prefix)"

# nanopb'nin kod üreteci Python'da protobuf paketini ister; sisteme dokunmamak için ayrı ortam.
if [[ ! -x "$VENV/bin/python3" ]]; then
  python3 -m venv "$VENV"
  "$VENV/bin/pip" install --quiet protobuf grpcio-tools
fi

git -C "$SRC" submodule update --init --depth 1 third-party/nanopb third-party/jerasure third-party/gf-complete third-party/curl
"$ROOT/scripts/patch-chiaki.sh"

# En eski desteklenen macOS. Homebrew'un hazır kütüphaneleri derlendiği makinenin macOS'unu hedefler
# (macOS 27'de derlenen libevent 27'de gelen pipe2'yi çağırır, uygulama 26'da açılmaz). Bu yüzden libevent
# ve miniupnpc burada kaynaktan, bu sürümü hedefleyerek derlenir.
MIN="26.0"
DEPS="$ROOT/third-party/build/deps"
rm -rf "$DEPS" && mkdir -p "$DEPS/src" "$DEPS/prefix"
fetch() { curl -fsSL --retry 3 "$2" | tar -xz -C "$DEPS/src" && mv "$DEPS/src/$3" "$DEPS/src/$1"; }
fetch libevent https://codeload.github.com/libevent/libevent/tar.gz/refs/tags/release-2.1.12-stable libevent-release-2.1.12-stable
fetch miniupnp https://codeload.github.com/miniupnp/miniupnp/tar.gz/refs/tags/miniupnpc_2_3_3 miniupnp-miniupnpc_2_3_3
dep() {
  local name=$1 src=$2; shift 2
  cmake -S "$src" -B "$DEPS/build-$name" -G Ninja -DCMAKE_BUILD_TYPE=Release -DCMAKE_OSX_ARCHITECTURES=arm64 \
    -DCMAKE_OSX_DEPLOYMENT_TARGET=$MIN -DCMAKE_INSTALL_PREFIX="$DEPS/prefix" -DCMAKE_POLICY_VERSION_MINIMUM=3.5 "$@"
  cmake --build "$DEPS/build-$name" --target install
}
# pipe2 SDK'da görünür ama yalnız macOS 27'de var; denetimi atlatıp pipe() kullandırılır.
dep libevent "$DEPS/src/libevent" -DEVENT__LIBRARY_TYPE=STATIC -DEVENT__DISABLE_OPENSSL=ON -DEVENT__DISABLE_MBEDTLS=ON \
  -DEVENT__DISABLE_TESTS=ON -DEVENT__DISABLE_SAMPLES=ON -DEVENT__DISABLE_BENCHMARK=ON -DEVENT__DISABLE_REGRESS=ON \
  -DEVENT__HAVE_PIPE2=0
dep miniupnpc "$DEPS/src/miniupnp/miniupnpc" -DUPNPC_BUILD_STATIC=ON -DUPNPC_BUILD_SHARED=OFF -DUPNPC_BUILD_TESTS=OFF \
  -DUPNPC_BUILD_SAMPLE=OFF

export PKG_CONFIG_PATH="$DEPS/prefix/lib/pkgconfig${PKG_CONFIG_PATH:+:$PKG_CONFIG_PATH}"

# Eski önbellek başka klasörde ya da başka bağımlılıklarla oluşturulmuş olabilir; her seferinde temiz derlenir.
rm -rf "$BUILD"

# Arayüz, komut satırı, ses ve FFmpeg kapalı: yalnız bağlantı, kayıt ve görüntü akışı gerekiyor.
cmake -S "$SRC" -B "$BUILD" -G Ninja \
  -DCMAKE_BUILD_TYPE=Release \
  -DCMAKE_OSX_ARCHITECTURES=arm64 \
  -DCMAKE_OSX_DEPLOYMENT_TARGET=$MIN \
  -DCMAKE_PREFIX_PATH="$DEPS/prefix;$BREW/opt/openssl@3;$BREW/opt/json-c" \
  -DOPENSSL_ROOT_DIR="$BREW/opt/openssl@3" \
  -DOPENSSL_USE_STATIC_LIBS=ON \
  -DPython3_EXECUTABLE="$VENV/bin/python3" \
  -DPYTHON_EXECUTABLE="$VENV/bin/python3" \
  -DCHIAKI_ENABLE_GUI=OFF \
  -DCHIAKI_ENABLE_CLI=OFF \
  -DCHIAKI_ENABLE_TESTS=OFF \
  -DCHIAKI_ENABLE_FFMPEG_DECODER=OFF \
  -DCHIAKI_ENABLE_PI_DECODER=OFF \
  -DCHIAKI_LIB_ENABLE_OPUS=OFF \
  -DCHIAKI_ENABLE_SPEEX=OFF \
  -DCHIAKI_ENABLE_SETSU=OFF \
  -DCHIAKI_ENABLE_STEAMDECK_NATIVE=OFF \
  -DCHIAKI_ENABLE_STEAM_SHORTCUT=OFF \
  -DCHIAKI_ENABLE_BOREALIS=OFF \
  -DBUILD_SHARED_LIBS=OFF \
  -DUSE_NGHTTP2=OFF -DUSE_LIBIDN2=OFF -DCURL_ZSTD=OFF -DCURL_BROTLI=OFF -DCURL_USE_LIBPSL=OFF -DCURL_USE_LIBSSH2=OFF -DUSE_LIBRTMP=OFF -DCURL_DISABLE_LDAP=ON

cmake --build "$BUILD" --target chiaki-lib

rm -rf "$OUT"
mkdir -p "$OUT/include" "$OUT/lib"
cp -R "$SRC/lib/include/chiaki" "$OUT/include/"
cp -R "$BUILD/lib/include/chiaki/"* "$OUT/include/chiaki/" 2>/dev/null || true

# Kütüphanenin kendisi ve derleme sırasında üretilen statik bağımlılıklar.
find "$BUILD" -name "*.a" -exec cp {} "$OUT/lib/" \;
# Homebrew'dan gelen statik bağımlılıklar (uygulamaya gömülür, kullanıcıda Homebrew gerekmez).
cp "$BREW/opt/openssl@3/lib/libcrypto.a" "$BREW/opt/openssl@3/lib/libssl.a" \
   "$BREW/opt/json-c/lib/libjson-c.a" \
   "$OUT/lib/"
cp "$DEPS/prefix/lib/libevent.a" "$DEPS/prefix/lib/libminiupnpc.a" "$OUT/lib/"

# Hiçbir parça MIN'den yeni macOS istememeli; istiyorsa uygulama eski macOS'ta açılmaz.
for lib in "$OUT"/lib/*.a; do
  if otool -l "$lib" | awk '/minos/ {print $2}' | sort -uV | tail -1 | awk -v min="$MIN" '{exit !($1+0 > min+0)}'; then
    echo "HATA: $lib macOS $MIN'den yenisini hedefliyor" >&2; exit 1
  fi
done
if nm -u "$OUT"/lib/*.a 2>/dev/null | grep -qx "_pipe2"; then echo "HATA: pipe2 hâlâ kullanılıyor" >&2; exit 1; fi

# Swift'ten "import CChiaki" ile kullanılmak üzere tek statik kütüphane + başlıklar + modül haritası.
FW="$ROOT/third-party/build/CChiaki.xcframework"
STAGE="$ROOT/third-party/build/xcframework-stage"
rm -rf "$FW" "$STAGE"
mkdir -p "$STAGE/include"
libtool -static -o "$STAGE/libCChiaki.a" "$OUT"/lib/*.a
cp -R "$OUT/include/chiaki" "$STAGE/include/"
{
  echo '#include <stdbool.h>'
  for h in common log session regist discovery controller video; do echo "#include \"chiaki/$h.h\""; done
} > "$STAGE/include/CChiaki.h"
cat > "$STAGE/include/module.modulemap" <<'MAP'
module CChiaki {
  umbrella header "CChiaki.h"
  export *
}
MAP
xcodebuild -create-xcframework -library "$STAGE/libCChiaki.a" -headers "$STAGE/include" -output "$FW" >/dev/null
rm -rf "$STAGE"

echo "Hazır: $FW"
ls "$OUT/lib"
