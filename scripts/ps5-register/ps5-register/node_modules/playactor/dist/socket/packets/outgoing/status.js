"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.StatusPacket = void 0;
const base_1 = require("../base");
const types_1 = require("../types");
class StatusPacket extends base_1.OutgoingPacket {
    constructor(status = 0) {
        super();
        this.status = status;
        this.type = types_1.PacketType.Status;
        this.totalLength = 12;
    }
    fillBuffer(builder) {
        builder.writeInt(this.status);
    }
}
exports.StatusPacket = StatusPacket;
