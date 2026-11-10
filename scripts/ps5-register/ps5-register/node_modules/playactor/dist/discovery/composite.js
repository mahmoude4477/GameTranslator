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
exports.CompositeDiscoveryNetwork = void 0;
class CompositeDiscoveryNetwork {
    constructor(delegates) {
        this.delegates = delegates;
    }
    close() {
        for (const delegate of this.delegates) {
            delegate.close();
        }
    }
    ping(deviceIp) {
        return __awaiter(this, void 0, void 0, function* () {
            yield Promise.all(this.delegates.map(d => d.ping(deviceIp)));
        });
    }
    send(recipientAddress, recipientPort, type, data) {
        return __awaiter(this, void 0, void 0, function* () {
            yield Promise.all(this.delegates.map(d => d.send(recipientAddress, recipientPort, type, data)));
        });
    }
    sendBuffer(recipientAddress, recipientPort, message) {
        return __awaiter(this, void 0, void 0, function* () {
            yield Promise.all(this.delegates.map(d => d.sendBuffer(recipientAddress, recipientPort, message)));
        });
    }
}
exports.CompositeDiscoveryNetwork = CompositeDiscoveryNetwork;
