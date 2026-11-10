"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.PacketReadState = exports.PlaintextCodec = exports.defaultSocketConfig = void 0;
exports.defaultSocketConfig = {
    maxRetries: 5,
    retryBackoffMillis: 500,
    connectTimeoutMillis: 15000,
};
exports.PlaintextCodec = {
    encode(packet) {
        return packet;
    },
    decode(packet) {
        return packet;
    },
};
var PacketReadState;
(function (PacketReadState) {
    PacketReadState[PacketReadState["PENDING"] = 0] = "PENDING";
    PacketReadState[PacketReadState["DONE"] = 1] = "DONE";
})(PacketReadState = exports.PacketReadState || (exports.PacketReadState = {}));
