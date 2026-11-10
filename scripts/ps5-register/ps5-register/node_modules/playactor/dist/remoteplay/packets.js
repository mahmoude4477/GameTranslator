"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.RemotePlayOutgoingPacket = exports.RemotePlayIncomingPacket = exports.RemotePlayResponseType = exports.RemotePlayCommand = void 0;
var RemotePlayCommand;
(function (RemotePlayCommand) {
    RemotePlayCommand[RemotePlayCommand["Standby"] = 80] = "Standby";
    RemotePlayCommand[RemotePlayCommand["Login"] = 5] = "Login";
    RemotePlayCommand[RemotePlayCommand["Heartbeat"] = 510] = "Heartbeat";
    RemotePlayCommand[RemotePlayCommand["ProvidePasscode"] = 32772] = "ProvidePasscode";
})(RemotePlayCommand = exports.RemotePlayCommand || (exports.RemotePlayCommand = {}));
var RemotePlayResponseType;
(function (RemotePlayResponseType) {
    RemotePlayResponseType[RemotePlayResponseType["Passcode"] = 4] = "Passcode";
    RemotePlayResponseType[RemotePlayResponseType["Login"] = 5] = "Login";
    RemotePlayResponseType[RemotePlayResponseType["Heartbeat"] = 254] = "Heartbeat";
})(RemotePlayResponseType = exports.RemotePlayResponseType || (exports.RemotePlayResponseType = {}));
class RemotePlayIncomingPacket {
    constructor(type, buffer) {
        this.type = type;
        this.buffer = buffer;
    }
    toBuffer() {
        return this.buffer;
    }
}
exports.RemotePlayIncomingPacket = RemotePlayIncomingPacket;
class RemotePlayOutgoingPacket {
    constructor(type, payload) {
        this.type = type;
        this.payload = payload;
    }
    toBuffer() {
        var _a, _b;
        const prelude = Buffer.alloc(8);
        prelude.writeUInt32BE((_b = (_a = this.payload) === null || _a === void 0 ? void 0 : _a.length) !== null && _b !== void 0 ? _b : 0);
        prelude.writeUInt16BE(this.type, 4);
        prelude.writeUInt16BE(0, 6);
        return this.payload
            ? Buffer.concat([prelude, this.payload])
            : prelude;
    }
}
exports.RemotePlayOutgoingPacket = RemotePlayOutgoingPacket;
