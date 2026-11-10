"use strict";
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.redact = void 0;
const debug_1 = __importDefault(require("debug"));
const keysToRedact = new Set([
    "AP-Bssid",
    "host-id",
    "deviceHostId",
    "passCode",
    "Np-AccountId",
    "PS5-Mac",
    "PS5-RegistKey",
    "RP-RegistKey",
    "RP-Key",
]);
function redactString(s) {
    return "█".repeat(s.length);
}
function redactBuffer(b) {
    return `<Buffer (${b.length} bytes) ${redactString("REDACTED")}>`;
}
function redactRecord(record) {
    return Object.keys(record).reduce((m, key) => {
        const value = m[key];
        if (value && typeof value === "string" && keysToRedact.has(key)) {
            // eslint-disable-next-line no-param-reassign
            m[key] = redactString(value);
        }
        else if (value && typeof value === "object") {
            // eslint-disable-next-line no-param-reassign
            m[key] = redactRecord(value);
        }
        return m;
    }, Object.assign({}, record));
}
function redact(value) {
    if (debug_1.default.enabled("playactor-unredacted")) {
        return value;
    }
    if (Array.isArray(value)) {
        return value.map(redact);
    }
    if (typeof value === "string") {
        return redactString(value);
    }
    if (Buffer.isBuffer(value)) {
        return redactBuffer(value);
    }
    if (typeof value === "object" && value != null) {
        return redactRecord(value);
    }
    return value;
}
exports.redact = redact;
