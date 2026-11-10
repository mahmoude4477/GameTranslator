#!/bin/sh
# SPDX-License-Identifier: AGPL-3.0-only
# TV uygulamasını dist/tv-app/ altında paketler. Mac ve Windows uygulamaları bu paketi içerir;
# geliştirici modu açık TV'ye kendileri kurar.
set -e
cd "$(dirname "$0")/.."
rm -rf dist/tv-app
mkdir -p dist/tv-app
ares-package tv-webos -o dist/tv-app -n
ls dist/tv-app/*.ipk
