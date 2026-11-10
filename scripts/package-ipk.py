#!/usr/bin/env python3
# SPDX-License-Identifier: AGPL-3.0-only
import io
import os
import tarfile

app_dir = "tv/webos"
package_id = "com.gametranslator.tv"
version = "1.0.0"

control_content = f"""Package: {package_id}
Version: {version}
Section: misc
Priority: optional
Architecture: all
Maintainer: Sefflex
Description: GameTranslator TV app
"""

control_tar = io.BytesIO()
with tarfile.open(fileobj=control_tar, mode="w:gz") as tar:
    ti = tarfile.TarInfo(name="./control")
    ti.size = len(control_content.encode("utf-8"))
    ti.mode = 0o644
    ti.uid = 0
    ti.gid = 0
    ti.uname = "root"
    ti.gname = "root"
    tar.addfile(ti, io.BytesIO(control_content.encode("utf-8")))

data_tar = io.BytesIO()
with tarfile.open(fileobj=data_tar, mode="w:gz") as tar:
    base_prefix = f"./usr/palm/applications/{package_id}"
    for p in ["./usr", "./usr/palm", "./usr/palm/applications", base_prefix]:
        d = tarfile.TarInfo(name=p)
        d.type = tarfile.DIRTYPE
        d.mode = 0o755
        d.uid = 0
        d.gid = 0
        tar.addfile(d)

    for fname in sorted(os.listdir(app_dir)):
        fpath = os.path.join(app_dir, fname)
        if os.path.isfile(fpath):
            with open(fpath, "rb") as f:
                content = f.read()
            ti = tarfile.TarInfo(name=f"{base_prefix}/{fname}")
            ti.size = len(content)
            ti.mode = 0o644
            ti.uid = 0
            ti.gid = 0
            ti.uname = "root"
            ti.gname = "root"
            tar.addfile(ti, io.BytesIO(content))

def create_ar(filename, files):
    with open(filename, "wb") as f:
        f.write(b"!<arch>\n")
        for name, data in files:
            size = len(data)
            mode = "100644"
            header = f"{name:<16}{0:<12}{0:<6}{0:<6}{mode:<8}{size:<10}`\n".encode("ascii")
            f.write(header)
            f.write(data)
            if size % 2 != 0:
                f.write(b"\n")

os.makedirs("tv/packages", exist_ok=True)
out_path = "tv/packages/com.gametranslator.tv_1.0.0_all.ipk"
create_ar(out_path, [
    ("debian-binary", b"2.0\n"),
    ("control.tar.gz", control_tar.getvalue()),
    ("data.tar.gz", data_tar.getvalue()),
])
print(f"Created {out_path} ({os.path.getsize(out_path)} bytes)")
