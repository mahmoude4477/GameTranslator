"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.UnsupportedDeviceError = void 0;
class UnsupportedDeviceError extends Error {
    constructor() {
        super("Device doesn't support connection");
    }
}
exports.UnsupportedDeviceError = UnsupportedDeviceError;
