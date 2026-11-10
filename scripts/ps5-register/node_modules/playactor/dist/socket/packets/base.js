"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.EmptyOutgoingPacket = exports.IncomingResultPacket = exports.OutgoingPacket = exports.IncomingPacket = void 0;
const builder_1 = require("./builder");
const errors_1 = require("./errors");
class IncomingPacket {
    toBuffer() {
        throw new Error("Incoming packet doesn't support toBuffer");
    }
}
exports.IncomingPacket = IncomingPacket;
class OutgoingPacket {
    toBuffer() {
        const builder = new builder_1.PacketBuilder(this.totalLength)
            .writeInt(this.type);
        this.fillBuffer(builder);
        return builder.build();
    }
}
exports.OutgoingPacket = OutgoingPacket;
class IncomingResultPacket extends IncomingPacket {
    constructor(data, toErrorCode = errors_1.resultsToErrorCodes) {
        var _a;
        super();
        this.data = data;
        this.type = data.readInt32LE(4);
        this.result = data.readInt32LE(8);
        if (this.result !== 0) {
            this.errorCode = (_a = toErrorCode[this.result]) !== null && _a !== void 0 ? _a : "UNKNOWN_ERROR";
        }
    }
    toBuffer() {
        return this.data;
    }
}
exports.IncomingResultPacket = IncomingResultPacket;
class EmptyOutgoingPacket extends OutgoingPacket {
    constructor() {
        super(...arguments);
        this.totalLength = 8;
    }
    fillBuffer() {
        // nop
    }
}
exports.EmptyOutgoingPacket = EmptyOutgoingPacket;
