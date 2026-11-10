"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.ByePacket = void 0;
const base_1 = require("../base");
const types_1 = require("../types");
class ByePacket extends base_1.EmptyOutgoingPacket {
    constructor() {
        super(...arguments);
        this.type = types_1.PacketType.Bye;
    }
}
exports.ByePacket = ByePacket;
