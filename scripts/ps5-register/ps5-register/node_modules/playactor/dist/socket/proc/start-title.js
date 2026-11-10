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
exports.StartTitleProc = void 0;
const helpers_1 = require("../helpers");
const boot_1 = require("../packets/outgoing/boot");
const types_1 = require("../packets/types");
class StartTitleProc {
    constructor(titleId) {
        this.titleId = titleId;
    }
    perform(socket) {
        return __awaiter(this, void 0, void 0, function* () {
            yield helpers_1.performRpc(socket, new boot_1.BootPacket(this.titleId), types_1.PacketType.BootResult);
        });
    }
}
exports.StartTitleProc = StartTitleProc;
