"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.StandbyResultPacket = void 0;
const base_1 = require("../base");
const types_1 = require("../types");
class StandbyResultPacket extends base_1.IncomingResultPacket {
    constructor() {
        super(...arguments);
        this.type = types_1.PacketType.StandbyResult;
    }
}
exports.StandbyResultPacket = StandbyResultPacket;
