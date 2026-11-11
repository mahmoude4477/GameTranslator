#!/usr/bin/env python3
"""
GameTranslator Android TV Installer via ADB Shell
Pushes APK, installs for the current active Android TV user, and launches the app.
"""

import sys
import os
import argparse
import json
import base64
import socket

try:
    from adb_shell.adb_device import AdbDeviceTcp
    from adb_shell.auth.sign_pythonrsa import PythonRSASigner
except ImportError:
    print(json.dumps({
        "event": "error",
        "message": "تعذر العثور على مكتبة adb-shell المطلوبة."
    }))
    sys.exit(1)

def get_local_ip():
    try:
        s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        s.connect(("8.8.8.8", 80))
        ip = s.getsockname()[0]
        s.close()
        return ip
    except Exception:
        return "127.0.0.1"

def emit(event, **kwargs):
    payload = {"event": event}
    payload.update(kwargs)
    print(json.dumps(payload), flush=True)

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--host", required=True)
    parser.add_argument("--package", default="")
    parser.add_argument("--action", choices=["install", "test", "launch"], default="install")
    parser.add_argument("--key-dir", default=os.path.expanduser("~/Library/Application Support/GameTranslator/adb"))
    parser.add_argument("--server", default="")
    args = parser.parse_args()

    host = args.host.strip()
    action = args.action
    apk_path = args.package.strip()
    key_dir = os.path.expanduser(args.key_dir)

    key_path = os.path.join(key_dir, "adbkey")
    if not os.path.isfile(key_path):
        # Fallback to default ADB key if present
        default_key = os.path.expanduser("~/.android/adbkey")
        if os.path.isfile(default_key):
            key_path = default_key
        else:
            emit("error", message="تعذر العثور على مفتاح ADB.")
            sys.exit(1)

    try:
        with open(key_path) as f:
            signer = PythonRSASigner("", f.read())
    except Exception as e:
        emit("error", message=f"تعذرت قراءة مفتاح ADB: {e}")
        sys.exit(1)

    device = AdbDeviceTcp(host, 5555, default_transport_timeout_s=12.0)
    try:
        device.connect(rsa_keys=[signer], auth_timeout_s=8)
    except Exception as e:
        emit("error", message=f"تعذر الاتصال بالتلفزيون عبر ADB ({host}:5555). تأكد من أن التلفزيون يعمل وأن خيار «تصحيح الأخطاء عبر الشبكة (ADB)» مفعّل: {e}")
        sys.exit(1)

    current_user = device.shell("am get-current-user").strip()
    if not current_user.isdigit():
        current_user = "0"

    local_ip = args.server.strip() if args.server.strip() else f"http://{get_local_ip()}:8787"
    params_json = json.dumps({"server": local_ip, "hdmi": 1}).replace('"', '\\"')

    if action in ("test", "launch"):
        emit("progress", message="جارٍ إظهار التطبيق على شاشة التلفزيون...")
        device.shell(f'am start -n com.gametranslator.tv/.MainActivity --user {current_user} -f 0x10000000 --es params "{params_json}"')
        device.shell("input keyevent 183") # PROG_RED - trigger subtitle
        emit("done", message="✅ فُتح GameTranslator على شاشة التلفزيون وأُرسلت ترجمة تجريبية!")
        return

    # Install action
    if not apk_path or not os.path.isfile(apk_path):
        emit("error", message=f"تعذر العثور على ملف APK: {apk_path}")
        sys.exit(1)

    emit("progress", message=f"جارٍ الاتصال بالتلفزيون ({host}:5555)...")
    emit("progress", message="تم الاتصال. جارٍ نقل ملف APK إلى التلفزيون...")

    with open(apk_path, "rb") as f:
        apk_bytes = f.read()

    b64_data = base64.b64encode(apk_bytes).decode("ascii")

    # Clean old files in /data/local/tmp
    device.shell("rm -f /data/local/tmp/gt_app.b64 /data/local/tmp/gt_app.apk")

    chunk_size = 32768
    total_chunks = (len(b64_data) + chunk_size - 1) // chunk_size

    for i in range(0, len(b64_data), chunk_size):
        chunk = b64_data[i:i + chunk_size]
        device.shell(f"echo -n '{chunk}' >> /data/local/tmp/gt_app.b64")

    # Decode on device
    device.shell("base64 -d /data/local/tmp/gt_app.b64 > /data/local/tmp/gt_app.apk")
    device.shell("rm -f /data/local/tmp/gt_app.b64")

    emit("progress", message="جارٍ تثبيت التطبيق على التلفزيون...")

    # Install for all users and specifically existing for current user
    install_out = device.shell(f"pm install -r -d --user {current_user} /data/local/tmp/gt_app.apk")
    if "Failure" in install_out or "Error" in install_out:
        install_out = device.shell("pm install -r -d /data/local/tmp/gt_app.apk")

    # Ensure existing package is enabled for current user
    device.shell(f"pm install-existing --user {current_user} com.gametranslator.tv")
    device.shell("rm -f /data/local/tmp/gt_app.apk")

    emit("progress", message="جارٍ تشغيل التطبيق على التلفزيون...")

    # Force stop before starting to ensure clean launch
    device.shell("am force-stop com.gametranslator.tv")
    start_out = device.shell(f'am start -n com.gametranslator.tv/.MainActivity --user {current_user} --es params "{params_json}"')
    device.shell("input keyevent 183") # PROG_RED - trigger subtitle immediately

    emit("done", installed=True, version="1.0.0", message="✅ تم تثبيت تطبيق GameTranslator على التلفزيون وفتحه بنجاح!")

if __name__ == "__main__":
    main()
