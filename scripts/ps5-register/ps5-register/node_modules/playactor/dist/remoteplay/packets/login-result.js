"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.RemotePlayLoginResultPacket = void 0;
const base_1 = require("../../socket/packets/base");
const login_result_1 = require("../../socket/packets/incoming/login-result");
const toErrorCode = {
    1: login_result_1.LoginResultError.PASSCODE_IS_UNMATCHED,
};
class RemotePlayLoginResultPacket extends base_1.IncomingPacket {
    constructor(data) {
        var _a;
        super();
        this.type = data.readInt16BE(4);
        this.result = data.readUInt8(8);
        if (this.result !== 0) {
            this.errorCode = (_a = toErrorCode[this.result]) !== null && _a !== void 0 ? _a : "UNKNOWN_ERROR";
        }
    }
}
exports.RemotePlayLoginResultPacket = RemotePlayLoginResultPacket;
