"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.OskStartPacket = void 0;
const base_1 = require("../base");
const types_1 = require("../types");
class OskStartPacket extends base_1.EmptyOutgoingPacket {
    constructor() {
        super(...arguments);
        this.type = types_1.PacketType.OskStart;
    }
}
exports.OskStartPacket = OskStartPacket;
