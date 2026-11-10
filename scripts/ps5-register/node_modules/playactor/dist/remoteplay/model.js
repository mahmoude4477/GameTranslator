"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.errorReasonString = exports.errorReason = exports.ErrorReason = exports.remotePlayVersionToString = exports.remotePlayVersionFor = exports.RemotePlayVersion = void 0;
const model_1 = require("../discovery/model");
var RemotePlayVersion;
(function (RemotePlayVersion) {
    RemotePlayVersion[RemotePlayVersion["PS4_8"] = 0] = "PS4_8";
    RemotePlayVersion[RemotePlayVersion["PS4_9"] = 1] = "PS4_9";
    RemotePlayVersion[RemotePlayVersion["PS4_10"] = 2] = "PS4_10";
    RemotePlayVersion[RemotePlayVersion["PS5_1"] = 3] = "PS5_1";
})(RemotePlayVersion = exports.RemotePlayVersion || (exports.RemotePlayVersion = {}));
function remotePlayVersionFor(device) {
    if (device.type === model_1.DeviceType.PS5) {
        return RemotePlayVersion.PS5_1;
    }
    const versionInt = parseInt(device.systemVersion, 10);
    if (versionInt >= 8000000) {
        return RemotePlayVersion.PS4_10;
    }
    if (versionInt >= 7000000) {
        return RemotePlayVersion.PS4_9;
    }
    return RemotePlayVersion.PS4_8;
}
exports.remotePlayVersionFor = remotePlayVersionFor;
function remotePlayVersionToString(version) {
    switch (version) {
        case RemotePlayVersion.PS4_8: return "8.0";
        case RemotePlayVersion.PS4_9: return "9.0";
        case RemotePlayVersion.PS4_10: return "10.0";
        case RemotePlayVersion.PS5_1: return "1.0";
    }
}
exports.remotePlayVersionToString = remotePlayVersionToString;
var ErrorReason;
(function (ErrorReason) {
    ErrorReason[ErrorReason["FAILED"] = 0] = "FAILED";
    ErrorReason[ErrorReason["INVALID_PSN_ID"] = 1] = "INVALID_PSN_ID";
    ErrorReason[ErrorReason["IN_USE"] = 2] = "IN_USE";
    ErrorReason[ErrorReason["CRASH"] = 3] = "CRASH";
    ErrorReason[ErrorReason["RP_VERSION"] = 4] = "RP_VERSION";
    ErrorReason[ErrorReason["UNKNOWN"] = 5] = "UNKNOWN";
})(ErrorReason = exports.ErrorReason || (exports.ErrorReason = {}));
const errorCodes = {
    "80108b09": ErrorReason.FAILED,
    "80108b02": ErrorReason.INVALID_PSN_ID,
    "80108b10": ErrorReason.IN_USE,
    "80108b15": ErrorReason.CRASH,
    "80108b11": ErrorReason.RP_VERSION,
    "80108bff": ErrorReason.UNKNOWN,
};
function errorReason(errorCode) {
    var _a;
    return (_a = errorCodes[errorCode.toLowerCase()]) !== null && _a !== void 0 ? _a : ErrorReason.UNKNOWN;
}
exports.errorReason = errorReason;
const errorReasonStrings = {
    [ErrorReason.FAILED]: "Registration failed, probably invalid PIN",
    [ErrorReason.INVALID_PSN_ID]: "Invalid PSN ID",
    [ErrorReason.IN_USE]: "Remote is already in use",
    [ErrorReason.CRASH]: "Remote Play on Console crashed",
    [ErrorReason.RP_VERSION]: "RP-Version mismatch",
    [ErrorReason.UNKNOWN]: "Other Error",
};
function errorReasonString(errorCode) {
    var _a;
    const reason = errorReason(errorCode);
    return (_a = errorReasonStrings[reason]) !== null && _a !== void 0 ? _a : errorReasonStrings[ErrorReason.UNKNOWN];
}
exports.errorReasonString = errorReasonString;
