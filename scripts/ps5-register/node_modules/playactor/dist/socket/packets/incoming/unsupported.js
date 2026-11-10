"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.UnsupportedIncomingPacket = void 0;
const base_1 = require("../base");
class UnsupportedIncomingPacket extends base_1.IncomingPacket {
    constructor(data) {
        super();
        this.type = data.readInt32LE(4);
    }
}
exports.UnsupportedIncomingPacket = UnsupportedIncomingPacket;
