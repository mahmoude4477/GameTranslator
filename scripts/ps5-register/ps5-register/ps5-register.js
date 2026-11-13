#!/usr/bin/env node
/**
 * GameTranslator PS5 Remote Play Registration CLI
 * Arguments:
 *   --host <ip>
 *   --pin <8-digit pin>
 *   --account <base64 account id>
 */

const path = require('path');
const dgram = require('dgram');
const http = require('http');

const fs = require('fs');
// Resolve playactor modules from local node_modules
const scriptDir = __dirname;
const candidateDirs = [
    path.join(scriptDir, 'ps5-register', 'node_modules', 'playactor'),
    path.join(scriptDir, 'ps5-register', 'ps5-register', 'node_modules', 'playactor'),
    path.join(scriptDir, 'node_modules', 'playactor'),
    path.join(scriptDir, '..', 'scripts', 'ps5-register', 'node_modules', 'playactor'),
    path.join(scriptDir, '..', 'scripts', 'ps5-register', 'ps5-register', 'node_modules', 'playactor')
];
let playactorDir = candidateDirs.find(d => fs.existsSync(path.join(d, 'dist', 'remoteplay', 'registration.js'))) || candidateDirs[0];

let RemotePlayRegistration, DeviceType, DiscoveryVersions, RemotePlayCrypto, parseBody, parseHexBytes, CRYPTO_NONCE_LENGTH;

try {
    const regModule = require(path.join(playactorDir, 'dist/remoteplay/registration'));
    RemotePlayRegistration = regModule.RemotePlayRegistration;
    const modelModule = require(path.join(playactorDir, 'dist/discovery/model'));
    DeviceType = modelModule.DeviceType;
    DiscoveryVersions = modelModule.DiscoveryVersions;
    const cryptoModule = require(path.join(playactorDir, 'dist/remoteplay/crypto'));
    RemotePlayCrypto = cryptoModule.RemotePlayCrypto;
    const protoModule = require(path.join(playactorDir, 'dist/remoteplay/protocol'));
    parseBody = protoModule.parseBody;
    parseHexBytes = protoModule.parseHexBytes;
    CRYPTO_NONCE_LENGTH = protoModule.CRYPTO_NONCE_LENGTH;
} catch (e) {
    console.log(JSON.stringify({ event: "error", message: "تعذر تحميل المكتبات المطلوبة: " + e.message }));
    process.exit(1);
}

function parseArgs() {
    const args = process.argv.slice(2);
    const options = {};
    for (let i = 0; i < args.length; i++) {
        if (args[i] === '--host' && args[i + 1]) {
            options.host = args[++i];
        } else if (args[i] === '--pin' && args[i + 1]) {
            options.pin = args[++i];
        } else if (args[i] === '--account' && args[i + 1]) {
            options.account = args[++i];
        }
    }
    return options;
}

const CLIENT_TYPE = "dabfa2ec873de5839bee8d3f4c0239c4282c07c25c6077a2931afcf0adc0d34f";

async function run() {
    const options = parseArgs();
    if (!options.host || !options.pin || !options.account) {
        console.log(JSON.stringify({
            event: "error",
            message: "معاملات ناقصة. الاستخدام: --host <ip> --pin <8_digit_code> --account <base64_id>"
        }));
        process.exit(1);
    }

    const host = options.host.trim();
    const pin = options.pin.trim();
    const account = options.account.trim();

    if (pin.length !== 8) {
        console.log(JSON.stringify({
            event: "error",
            message: "يجب أن يتكون رمز PIN من 8 أرقام بالضبط."
        }));
        process.exit(1);
    }

    console.log(JSON.stringify({
        event: "progress",
        message: `جارٍ بدء الربط مع PS5 على العنوان ${host}...`
    }));

    const device = {
        type: DeviceType.PS5,
        address: { address: host, port: 9302 },
        hostRequestPort: 997,
        discoveryVersion: DiscoveryVersions.PS5,
        systemVersion: '14220000',
        id: 'E86E3A94B164',
        name: 'PS5'
    };

    // Step 1: Send SRC3 handshake over UDP 9295 repeatedly until RES3 is received
    const REGISTRATION_PORT = 9295;
    let socket;
    try {
        socket = dgram.createSocket('udp4');
    } catch (e) {
        console.log(JSON.stringify({ event: "error", message: "تعذر إنشاء مقبس UDP: " + e.message }));
        process.exit(1);
    }

    let searchDone = false;

    const searchPromise = new Promise((resolve, reject) => {
        const timeout = setTimeout(() => {
            if (!searchDone) {
                searchDone = true;
                try { socket.close(); } catch (_) {}
                reject(new Error("لم يرد PS5 على طلب الربط (انتهت المهلة). تأكد من أن شاشة «الإعدادات > النظام > Remote Play > ربط الجهاز» مفتوحة على PS5."));
            }
        }, 15000);

        socket.on('message', (msg, rinfo) => {
            const str = msg.toString();
            if (str.startsWith('RES3')) {
                searchDone = true;
                clearTimeout(timeout);
                try { socket.close(); } catch (_) {}
                resolve();
            }
        });

        socket.on('error', (err) => {
            if (!searchDone) {
                searchDone = true;
                clearTimeout(timeout);
                try { socket.close(); } catch (_) {}
                reject(err);
            }
        });

        socket.bind(REGISTRATION_PORT, () => {
            // Send SRC3 ping every 500ms
            const interval = setInterval(() => {
                if (searchDone) {
                    clearInterval(interval);
                    return;
                }
                socket.send(Buffer.from('SRC3'), REGISTRATION_PORT, host, (err) => {
                    if (err) {
                        clearInterval(interval);
                        clearTimeout(timeout);
                        searchDone = true;
                        try { socket.close(); } catch (_) {}
                        reject(new Error("تعذر إرسال SRC3: " + err.message));
                    }
                });
            }, 500);

            // Send immediate first packet
            socket.send(Buffer.from('SRC3'), REGISTRATION_PORT, host);
        });
    });

    try {
        await searchPromise;
    } catch (err) {
        console.log(JSON.stringify({ event: "error", message: err.message }));
        process.exit(1);
    }

    console.log(JSON.stringify({
        event: "progress",
        message: "وصلت إشارة جاهزية PS5 (RES3). جارٍ إرسال حزمة التسجيل المشفّرة..."
    }));

    // Wait 100ms for PS5 state transition
    await new Promise(r => setTimeout(r, 100));

    // Step 2: Encrypt payload with PIN
    const crypto = RemotePlayCrypto.forDeviceAndPin(device, pin);
    const payloadBuffer = crypto.createSignedPayload({
        "Client-Type": CLIENT_TYPE,
        "Np-AccountId": account
    });

    // Step 3: POST /sie/ps5/rp/sess/rgst to http://host:9295
    const postOptions = {
        hostname: host,
        port: REGISTRATION_PORT,
        path: '/sie/ps5/rp/sess/rgst',
        method: 'POST',
        headers: {
            'User-Agent': 'remoteplay Windows',
            'RP-Version': '1.0',
            'Content-Type': 'application/octet-stream',
            'Content-Length': payloadBuffer.length
        },
        timeout: 10000
    };

    const httpPromise = new Promise((resolve, reject) => {
        const req = http.request(postOptions, (res) => {
            const chunks = [];
            res.on('data', chunk => chunks.push(chunk));
            res.on('end', () => {
                const body = Buffer.concat(chunks);
                const statusCode = res.statusCode || 500;
                const reasonHeader = res.headers['rp-application-reason'] || '';

                if (statusCode >= 300) {
                    let errMsg = `خطأ في تسجيل PS5 (HTTP ${statusCode})`;
                    if (reasonHeader === '80108b09') {
                        errMsg = "رمز PIN غير صالح أو انتهت صلاحيته! احصل على رمز PIN جديد من شاشة PS5 ثم أعد المحاولة.";
                    } else if (reasonHeader === '80108b02') {
                        errMsg = "عدم تطابق حساب PSN! حساب PSN الذي أدخلته لا يخص الملف الشخصي المفتوح الآن على PS5. سجّل الدخول بالحساب نفسه المستخدم على PS5.";
                    } else if (reasonHeader === '80108b10') {
                        errMsg = "Remote Play على PS5 مستخدم الآن من جهاز آخر.";
                    } else if (reasonHeader) {
                        errMsg += ` (رمز السبب: ${reasonHeader})`;
                    }
                    return reject(new Error(errMsg));
                }

                try {
                    const decrypted = crypto.decrypt(body);
                    const parsed = parseBody(decrypted);
                    resolve(parsed);
                } catch (decErr) {
                    reject(new Error("تعذر فك تشفير رد PS5: " + decErr.message));
                }
            });
        });

        req.on('error', (err) => reject(new Error("فشل طلب HTTP إلى PS5: " + err.message)));
        req.on('timeout', () => {
            req.destroy();
            reject(new Error("لم يرد PS5 على طلب HTTP (انتهت المهلة)."));
        });

        req.write(payloadBuffer);
        req.end();
    });

    try {
        const registration = await httpPromise;
        const registKey = registration["PS5-RegistKey"] || registration["PS4-RegistKey"];
        const rpKey = registration["RP-Key"];

        if (!registKey) {
            console.log(JSON.stringify({
                event: "error",
                message: "لم يصل من PS5 مفتاح تسجيل صالح (RegistKey)."
            }));
            process.exit(1);
        }

        console.log(JSON.stringify({
            event: "done",
            success: true,
            registKey: registKey,
            rpKey: rpKey || "",
            message: "تم ربط PS5 بنجاح!"
        }));
    } catch (err) {
        console.log(JSON.stringify({ event: "error", message: err.message }));
        process.exit(1);
    }
}

run();
