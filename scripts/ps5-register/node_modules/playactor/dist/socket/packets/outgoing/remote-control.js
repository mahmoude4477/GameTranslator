"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.RemoteControlPacket = void 0;
const base_1 = require("../base");
const types_1 = require("../types");
class RemoteControlPacket extends base_1.OutgoingPacket {
    constructor(op, holdTimeMillis = 0) {
        super();
        this.op = op;
        this.holdTimeMillis = holdTimeMillis;
        this.type = types_1.PacketType.RemoteControl;
        this.totalLength = 16;
    }
    fillBuffer(builder) {
        builder
            .writeInt(this.op)
            .writeInt(this.holdTimeMillis);
    }
}
exports.RemoteControlPacket = RemoteControlPacket;
