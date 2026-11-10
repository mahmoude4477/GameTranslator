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
exports.CredentialManager = exports.CredentialsError = void 0;
const disk_storage_1 = require("./credentials/disk-storage");
const rejecting_requester_1 = require("./credentials/rejecting-requester");
const model_1 = require("./discovery/model");
class CredentialsError extends Error {
}
exports.CredentialsError = CredentialsError;
class CredentialManager {
    constructor(requester = new rejecting_requester_1.RejectingCredentialRequester(), storage = new disk_storage_1.DiskCredentialsStorage()) {
        this.requester = requester;
        this.storage = storage;
    }
    getForDevice(device) {
        return __awaiter(this, void 0, void 0, function* () {
            const existing = yield this.storage.read(device.id);
            if (existing)
                return existing;
            if (device.status !== model_1.DeviceStatus.AWAKE) {
                throw new CredentialsError(`Device ${device.name} must be awake for initial registration`);
            }
            const fromRequest = yield this.requester.requestForDevice(device);
            yield this.storage.write(device.id, fromRequest);
            return fromRequest;
        });
    }
}
exports.CredentialManager = CredentialManager;
