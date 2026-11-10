"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.ClientHelloPacket = void 0;
const base_1 = require("../base");
const types_1 = require("../types");
class ClientHelloPacket extends base_1.OutgoingPacket {
    constructor(protocolVersion) {
        super();
        this.protocolVersion = protocolVersion;
        this.type = types_1.PacketType.Hello;
        this.totalLength = 28;
    }
    fillBuffer(builder) {
        builder
            .writeShort(this.protocolVersion.minor)
            .writeShort(this.protocolVersion.major);
        // NOTE: 16 bytes of "seed id"; just using 0 seems fine
    }
}
exports.ClientHelloPacket = ClientHelloPacket;
