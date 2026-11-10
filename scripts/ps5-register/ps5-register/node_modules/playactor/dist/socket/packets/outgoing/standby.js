"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.StandbyPacket = void 0;
const base_1 = require("../base");
const types_1 = require("../types");
class StandbyPacket extends base_1.EmptyOutgoingPacket {
    constructor() {
        super(...arguments);
        this.type = types_1.PacketType.Standby;
    }
}
exports.StandbyPacket = StandbyPacket;
