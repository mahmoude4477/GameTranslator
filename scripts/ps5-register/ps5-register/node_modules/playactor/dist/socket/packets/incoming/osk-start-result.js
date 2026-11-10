"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.OskStartResultPacket = void 0;
const osk_1 = require("../../osk");
const base_1 = require("../base");
const types_1 = require("../types");
class OskStartResultPacket extends base_1.IncomingResultPacket {
    constructor(data) {
        super(data);
        /* eslint-disable no-bitwise */
        this.type = types_1.PacketType.OskStartResult;
        this.oskType = data.readInt32LE(12);
        this.maxLength = data.readInt32LE(16);
        this.initialContent = data.toString("utf16le", 20);
    }
    get actionType() {
        return ((this.oskType & 0x0F0) >>> 4);
    }
    get inputType() {
        const basic = this.oskType & 0x003;
        const extended = (this.oskType & 0x300) >>> 6;
        return (basic | extended);
    }
    get flags() {
        const mask = osk_1.OskFlags.AutoCapitalize | osk_1.OskFlags.MultiLine | osk_1.OskFlags.Password;
        return (this.oskType & mask);
    }
}
exports.OskStartResultPacket = OskStartResultPacket;
