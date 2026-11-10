"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.OskChangeStringPacket = void 0;
const base_1 = require("../base");
const types_1 = require("../types");
const defaultOptions = {
    preEditIndex: 0,
    preEditLength: 0,
    editIndex: 0,
    editLength: 0,
};
class OskChangeStringPacket extends base_1.OutgoingPacket {
    constructor(text, options = {}) {
        super();
        this.options = options;
        this.type = types_1.PacketType.OskChangeString;
        this.textBuffer = Buffer.from(text, "utf16le");
        this.totalLength = OskChangeStringPacket.minLength
            + this.textBuffer.length;
    }
    fillBuffer(builder) {
        var _a;
        const opts = Object.assign(Object.assign({}, defaultOptions), this.options);
        const caretIndex = (_a = opts.caretIndex) !== null && _a !== void 0 ? _a : opts.editIndex + this.textBuffer.length;
        builder
            .writeInt(opts.preEditIndex)
            .writeInt(opts.preEditLength)
            .writeInt(caretIndex)
            .writeInt(opts.editIndex)
            .writeInt(opts.editLength)
            .write(this.textBuffer);
    }
}
exports.OskChangeStringPacket = OskChangeStringPacket;
OskChangeStringPacket.minLength = 28;
