"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.parseMessage = void 0;
const model_1 = require("./model");
const STATUS_CODE_STANDBY = "620";
function parseMessage(raw) {
    const rawString = raw.toString();
    const lines = rawString.split("\n");
    if (!lines.length) {
        throw new Error(`Invalid message: no lines: ${raw.toString()}`);
    }
    const result = {
        type: model_1.DiscoveryMessageType.DEVICE,
    };
    const statusLine = lines[0];
    if (statusLine.startsWith("HTTP")) {
        // device response
        const status = statusLine.substring(statusLine.indexOf(" ") + 1);
        result.statusLine = status;
        const [code, message] = status.split(" ");
        result.statusCode = code;
        result.statusMessage = message;
        result.status = code === STATUS_CODE_STANDBY
            ? model_1.DeviceStatus.STANDBY
            : model_1.DeviceStatus.AWAKE;
    }
    else {
        const method = statusLine.substring(0, statusLine.indexOf(" "));
        if (undefined === model_1.DiscoveryMessageType[method]) {
            throw new Error(`Unexpected discovery message: ${method}`);
        }
        result.type = method;
    }
    for (let i = 1; i < lines.length; ++i) {
        const line = lines[i];
        const [key, value] = line.split(/:[ ]*/);
        if (value) {
            result[key.toLowerCase()] = value;
        }
    }
    return result;
}
exports.parseMessage = parseMessage;
