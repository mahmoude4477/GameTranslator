"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.parsePassCodeString = exports.parsePassCodeKeys = exports.PassCodeKey = void 0;
var PassCodeKey;
(function (PassCodeKey) {
    PassCodeKey[PassCodeKey["Left"] = 1] = "Left";
    PassCodeKey[PassCodeKey["Up"] = 2] = "Up";
    PassCodeKey[PassCodeKey["Right"] = 3] = "Right";
    PassCodeKey[PassCodeKey["Down"] = 4] = "Down";
    PassCodeKey[PassCodeKey["R1"] = 5] = "R1";
    PassCodeKey[PassCodeKey["R2"] = 6] = "R2";
    PassCodeKey[PassCodeKey["L1"] = 7] = "L1";
    PassCodeKey[PassCodeKey["L2"] = 8] = "L2";
    PassCodeKey[PassCodeKey["Triangle"] = 9] = "Triangle";
    PassCodeKey[PassCodeKey["Square"] = 0] = "Square";
})(PassCodeKey = exports.PassCodeKey || (exports.PassCodeKey = {}));
const PASSCODE_LENGTH = 4;
const nameToKey = Object.keys(PassCodeKey)
    .reduce((m, key) => {
    // eslint-disable-next-line no-param-reassign
    m[key.toLowerCase()] = PassCodeKey[key];
    return m;
}, {});
/**
 * Given a sequence of passcode keys, convert them into the
 * actual passcode numerical string value for use in ILoginConfig
 */
function parsePassCodeKeys(...keys) {
    if (keys.length !== PASSCODE_LENGTH) {
        throw new Error(`Passcode must have length ${PASSCODE_LENGTH}`);
    }
    let result = "";
    for (const key of keys) {
        result += key;
    }
    return result;
}
exports.parsePassCodeKeys = parsePassCodeKeys;
/**
 * Given a raw value, which may either be a string of PassCodeKey names
 * or a literal passcode string, return the validated passcode string
 * value, for use in ILoginConfig.
 */
function parsePassCodeString(input) {
    if (/^[0-9]+$/.test(input)) {
        if (input.length !== PASSCODE_LENGTH) {
            throw new Error(`Passcode must be ${PASSCODE_LENGTH} numbers`);
        }
        return input;
    }
    const keys = [];
    const inputParts = input.split(/[ ]+/);
    for (const part of inputParts) {
        const key = nameToKey[part.toLowerCase()];
        if (key === undefined) {
            throw new Error(`No such passcode key: ${part}`);
        }
        keys.push(key);
    }
    return parsePassCodeKeys(...keys);
}
exports.parsePassCodeString = parsePassCodeString;
