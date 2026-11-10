"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.DeviceType = exports.DiscoveryMessageType = exports.isDiscoveryKey = exports.outgoingDiscoveryKeys = exports.DeviceStatus = exports.defaultDiscoveryConfig = exports.DiscoveryVersions = void 0;
exports.DiscoveryVersions = {
    PS4: "00020020",
    PS5: "00030010",
};
exports.defaultDiscoveryConfig = {
    pingIntervalMillis: 5000,
    timeoutMillis: 30000,
    uniqueDevices: true,
};
var DeviceStatus;
(function (DeviceStatus) {
    DeviceStatus["STANDBY"] = "STANDBY";
    DeviceStatus["AWAKE"] = "AWAKE";
})(DeviceStatus = exports.DeviceStatus || (exports.DeviceStatus = {}));
const discoveryKeysArrray = [
    "host-id",
    "host-name",
    "host-request-port",
    "host-type",
    "system-version",
    "device-discovery-protocol-version",
];
const discoveryKeys = new Set(discoveryKeysArrray);
exports.outgoingDiscoveryKeys = new Set([
    ...discoveryKeysArrray,
    "app-type",
    "auth-type",
    "client-type",
    "model",
    "user-credential",
]);
function isDiscoveryKey(s) {
    return discoveryKeys.has(s);
}
exports.isDiscoveryKey = isDiscoveryKey;
var DiscoveryMessageType;
(function (DiscoveryMessageType) {
    DiscoveryMessageType["SRCH"] = "SRCH";
    DiscoveryMessageType["WAKEUP"] = "WAKEUP";
    DiscoveryMessageType["DEVICE"] = "DEVICE";
})(DiscoveryMessageType = exports.DiscoveryMessageType || (exports.DiscoveryMessageType = {}));
var DeviceType;
(function (DeviceType) {
    DeviceType["PS4"] = "PS4";
    DeviceType["PS5"] = "PS5";
})(DeviceType = exports.DeviceType || (exports.DeviceType = {}));
