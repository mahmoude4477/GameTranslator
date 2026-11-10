"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.HandshakePacket = void 0;
const base_1 = require("../base");
const types_1 = require("../types");
class HandshakePacket extends base_1.OutgoingPacket {
    constructor(key, seed) {
        super();
        this.key = key;
        this.seed = seed;
        this.type = types_1.PacketType.Handshake;
        this.totalLength = 280;
        if (key.length !== 256) {
            throw new Error(`Key is wrong size (was ${key.length})`);
        }
        if (seed.length > 16) {
            throw new Error(`Seed is wrong size (was ${seed.length})`);
        }
    }
    fillBuffer(builder) {
        builder
            .write(this.key)
            .write(this.seed);
    }
}
exports.HandshakePacket = HandshakePacket;
