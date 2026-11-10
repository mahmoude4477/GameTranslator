"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.RemotePlayPasscodeResponsePacket = void 0;
const packets_1 = require("../packets");
class RemotePlayPasscodeResponsePacket extends packets_1.RemotePlayOutgoingPacket {
    constructor(passCode) {
        super(packets_1.RemotePlayCommand.ProvidePasscode, Buffer.from(passCode));
    }
}
exports.RemotePlayPasscodeResponsePacket = RemotePlayPasscodeResponsePacket;
