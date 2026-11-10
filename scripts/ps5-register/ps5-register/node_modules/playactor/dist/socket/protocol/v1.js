"use strict";
var __awaiter = (this && this.__awaiter) || function (thisArg, _arguments, P, generator) {
    function adopt(value) { return value instanceof P ? value : new P(function (resolve) { resolve(value); }); }
    return new (P || (P = Promise))(function (resolve, reject) {
        function fulfilled(value) { try { step(generator.next(value)); } catch (e) { reject(e); } }
        function rejected(value) { try { step(generator["throw"](value)); } catch (e) { reject(e); } }
        function step(result) { result.done ? resolve(result.value) : adopt(result.value).then(fulfilled, rejected); }
        step((generator = generator.apply(thisArg, _arguments || [])).next());
    });
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.DeviceProtocolV1 = exports.PacketReaderV1 = void 0;
const base_1 = require("../packets/base");
const login_result_1 = require("../packets/incoming/login-result");
const osk_start_result_1 = require("../packets/incoming/osk-start-result");
const server_hello_1 = require("../packets/incoming/server-hello");
const standby_result_1 = require("../packets/incoming/standby-result");
const unsupported_1 = require("../packets/incoming/unsupported");
const bye_1 = require("../packets/outgoing/bye");
const status_1 = require("../packets/outgoing/status");
const types_1 = require("../packets/types");
const base_2 = require("./base");
const PACKET_TYPE_OFFSET = 4;
class PacketReaderV1 extends base_2.TypedPacketReader {
    constructor() {
        super({
            [types_1.PacketType.Hello]: server_hello_1.ServerHelloPacket,
            [types_1.PacketType.BootResult]: base_1.IncomingResultPacket,
            [types_1.PacketType.LoginResult]: login_result_1.LoginResultPacket,
            [types_1.PacketType.OskStartResult]: osk_start_result_1.OskStartResultPacket,
            [types_1.PacketType.StandbyResult]: standby_result_1.StandbyResultPacket,
            [types_1.PacketType.ServerStatus]: base_1.IncomingResultPacket,
        });
    }
    readType(buffer) {
        return buffer.readInt32LE(PACKET_TYPE_OFFSET);
    }
    createDefaultPacket(type, buffer) {
        return new unsupported_1.UnsupportedIncomingPacket(buffer);
    }
}
exports.PacketReaderV1 = PacketReaderV1;
exports.DeviceProtocolV1 = {
    version: {
        major: 2,
        minor: 0,
    },
    createPacketReader() {
        return new PacketReaderV1();
    },
    onPacketReceived(socket, packet) {
        return __awaiter(this, void 0, void 0, function* () {
            switch (packet.type) {
                case types_1.PacketType.ServerStatus:
                    yield socket.send(new status_1.StatusPacket());
                    break;
            }
        });
    },
    requestDisconnect(socket) {
        return __awaiter(this, void 0, void 0, function* () {
            yield socket.send(new bye_1.ByePacket());
        });
    },
};
