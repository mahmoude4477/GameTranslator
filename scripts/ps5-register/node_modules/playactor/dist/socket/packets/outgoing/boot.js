"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.BootPacket = void 0;
const base_1 = require("../base");
const types_1 = require("../types");
class BootPacket extends base_1.OutgoingPacket {
    constructor(titleId) {
        super();
        this.titleId = titleId;
        this.type = types_1.PacketType.Boot;
        this.totalLength = 24;
    }
    fillBuffer(builder) {
        builder.writePadded(this.titleId, 16);
    }
}
exports.BootPacket = BootPacket;
