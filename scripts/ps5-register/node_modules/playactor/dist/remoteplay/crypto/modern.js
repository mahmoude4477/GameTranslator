"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.ModernCryptoStrategy = exports.generateAuthSeed = exports.transformServerNonceForAuth = exports.generateSeed = void 0;
const model_1 = require("../../discovery/model");
const base_1 = require("./base");
const keys_1 = require("./keys");
const SEED_BYTES_COUNT = 16;
// NOTE: public for testing
function generateSeed(deviceType, pin, initKeyOff) {
    /* eslint-disable no-bitwise */
    const initKey = keys_1.INIT_KEYS[deviceType];
    const seed = Buffer.alloc(SEED_BYTES_COUNT);
    for (let i = 0; i < SEED_BYTES_COUNT; ++i) {
        seed[i] = initKey[(i * 0x20 + initKeyOff) % initKey.length];
    }
    seed[0xc] ^= (pin >> 0x18) & 0xFF;
    seed[0xd] ^= (pin >> 0x10) & 0xFF;
    seed[0xe] ^= (pin >> 0x08) & 0xFF;
    seed[0xf] ^= (pin >> 0x00) & 0xFF;
    return seed;
    /* eslint-enable no-bitwise */
}
exports.generateSeed = generateSeed;
function generateAeropause(deviceType, nonce, padding) {
    /* eslint-disable no-bitwise */
    const aeroKey = keys_1.AERO_KEYS[deviceType];
    const aeroKeyOff = padding[0] >> 3;
    const wurzelbert = deviceType === model_1.DeviceType.PS5 ? (-0x2d & 0xff) : 0x29;
    const aeropause = Buffer.alloc(SEED_BYTES_COUNT);
    for (let i = 0; i < SEED_BYTES_COUNT; ++i) {
        const k = aeroKey[i * 0x20 + aeroKeyOff];
        const v = (nonce[i] ^ k) + wurzelbert + i;
        aeropause[i] = v;
    }
    return aeropause;
    /* eslint-enable no-bitwise */
}
// NOTE: public for testing
function transformServerNonceForAuth(deviceType, serverNonce) {
    /* eslint-disable no-bitwise */
    const nonceTransforms = {
        [model_1.DeviceType.PS4](v, i) { return v + 0x36 + i; },
        [model_1.DeviceType.PS5](v, i) { return v - 0x2d - i; },
    };
    const keys = keys_1.AUTH_NONCE_KEYS[deviceType];
    const keyOffset = (serverNonce[0] >> 3) * 0x70;
    const transform = nonceTransforms[deviceType];
    const nonce = Buffer.alloc(SEED_BYTES_COUNT);
    for (let i = 0; i < SEED_BYTES_COUNT; ++i) {
        const key = keys[keyOffset + i];
        nonce[i] = transform(serverNonce[i], i) ^ key;
    }
    return nonce;
    /* eslint-enable no-bitwise */
}
exports.transformServerNonceForAuth = transformServerNonceForAuth;
// NOTE: public for testing
function generateAuthSeed(deviceType, authKey, serverNonce) {
    /* eslint-disable no-bitwise */
    const transforms = {
        [model_1.DeviceType.PS4](i, key) {
            let v = key ^ authKey[i];
            v += 0x21 + i;
            return v ^ serverNonce[i];
        },
        [model_1.DeviceType.PS5](i, key) {
            let v = authKey[i];
            v += 0x18 + i;
            v ^= serverNonce[i];
            return v ^ key;
        },
    };
    const keys = keys_1.AUTH_SEED_KEYS[deviceType];
    const keyOffset = (serverNonce[7] >> 3) * 0x70;
    const transform = transforms[deviceType];
    const seed = Buffer.alloc(SEED_BYTES_COUNT);
    for (let i = 0; i < SEED_BYTES_COUNT; ++i) {
        const key = keys[keyOffset + i];
        seed[i] = transform(i, key);
    }
    return seed;
    /* eslint-enable no-bitwise */
}
exports.generateAuthSeed = generateAuthSeed;
/**
 * Handles crypto for PS5s, and PS4s on RemotePlay 10.0+
 */
class ModernCryptoStrategy extends base_1.BaseCryptoStrategy {
    constructor(deviceType, version) {
        super(version);
        this.deviceType = deviceType;
    }
    generatePinSeed(padding, pinNumber) {
        /* eslint-disable no-bitwise */
        const initKeyOff = padding[0x18D] & 0x1F;
        /* eslint-enable no-bitwise */
        return generateSeed(this.deviceType, pinNumber, initKeyOff);
    }
    signPadding(nonce, padding) {
        const aeropause = generateAeropause(this.deviceType, nonce, padding);
        const AEROPAUSE_PART1_DESTINATION = 0xc7;
        const AEROPAUSE_PART2_DESTINATION = 0x191;
        aeropause.copy(padding, AEROPAUSE_PART1_DESTINATION, 8, 16);
        aeropause.copy(padding, AEROPAUSE_PART2_DESTINATION, 0, 8);
    }
    generateAuthSeed(key, serverNonce) {
        return generateAuthSeed(this.deviceType, key, serverNonce);
    }
    transformServerNonceForAuth(serverNonce) {
        return transformServerNonceForAuth(this.deviceType, serverNonce);
    }
}
exports.ModernCryptoStrategy = ModernCryptoStrategy;
