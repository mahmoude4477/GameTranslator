"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.OskControlPacket = void 0;
const base_1 = require("../base");
const types_1 = require("../types");
class OskControlPacket extends base_1.OutgoingPacket {
    constructor(command) {
        super();
        this.command = command;
        this.type = types_1.PacketType.OskControl;
        this.totalLength = 12;
    }
    fillBuffer(builder) {
        builder.writeInt(this.command);
    }
}
exports.OskControlPacket = OskControlPacket;
