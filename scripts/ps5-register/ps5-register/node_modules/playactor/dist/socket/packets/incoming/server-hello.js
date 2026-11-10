"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.ServerHelloPacket = void 0;
const base_1 = require("../base");
const errors_1 = require("../errors");
const types_1 = require("../types");
class ServerHelloPacket extends base_1.IncomingPacket {
    constructor(data) {
        super();
        this.type = types_1.PacketType.Hello;
        this.version = data.readInt32LE(8);
        this.result = data.readInt32LE(12);
        this.option = data.readInt32LE(16);
        this.seed = data.slice(20, 36);
        this.errorCode = errors_1.resultToErrorCode(this.result);
    }
}
exports.ServerHelloPacket = ServerHelloPacket;
