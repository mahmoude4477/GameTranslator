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
exports.WriteOnlyStorage = void 0;
/**
 * Write-only storage delegates to another [ICredentialStorage]
 * for writes, but only returns credentials from that storage
 * *after* it has written to them. This is useful if you want to
 * force authentication.
 */
class WriteOnlyStorage {
    constructor(delegate) {
        this.delegate = delegate;
        this.hasWritten = false;
    }
    read(deviceId) {
        return __awaiter(this, void 0, void 0, function* () {
            if (this.hasWritten) {
                return this.delegate.read(deviceId);
            }
            return null;
        });
    }
    write(deviceId, credentials) {
        return __awaiter(this, void 0, void 0, function* () {
            yield this.delegate.write(deviceId, credentials);
            this.hasWritten = true;
        });
    }
}
exports.WriteOnlyStorage = WriteOnlyStorage;
