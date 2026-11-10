#!/bin/sh
# SPDX-License-Identifier: AGPL-3.0-only
# third-party/patches altındaki chiaki-ng yamalarını uygular; zaten uygulanmışsa atlar (Mac ve Windows derlemesi kullanır).
set -e
cd "$(dirname "$0")/.."
for patch in third-party/patches/chiaki-*.patch; do
  if git -C third-party/chiaki-ng apply --reverse --check "../../$patch" 2>/dev/null; then
    echo "Zaten uygulanmış: $patch"
  else
    git -C third-party/chiaki-ng apply "../../$patch"
    echo "Uygulandı: $patch"
  fi
done
