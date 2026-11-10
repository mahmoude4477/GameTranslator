"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.RemotePlayPasscodeRequestPacket = void 0;
const base_1 = require("../../socket/packets/base");
const packets_1 = require("../packets");
/**
 * Sent by the server in response to a LOGIN request when the account
 * is protected by a passcode
 */
class RemotePlayPasscodeRequestPacket extends base_1.IncomingPacket {
    constructor() {
        super(...arguments);
        this.type = packets_1.RemotePlayResponseType.Passcode;
        this.result = 0;
    }
}
exports.RemotePlayPasscodeRequestPacket = RemotePlayPasscodeRequestPacket;
