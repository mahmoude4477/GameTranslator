"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.ExitCode = void 0;
var ExitCode;
(function (ExitCode) {
    ExitCode[ExitCode["DeviceAwake"] = 0] = "DeviceAwake";
    ExitCode[ExitCode["DeviceStandby"] = 1] = "DeviceStandby";
    ExitCode[ExitCode["DeviceNotFound"] = 2] = "DeviceNotFound";
    ExitCode[ExitCode["PassCodeNeeded"] = 4] = "PassCodeNeeded";
    ExitCode[ExitCode["PassCodeUnmatched"] = 5] = "PassCodeUnmatched";
})(ExitCode = exports.ExitCode || (exports.ExitCode = {}));
