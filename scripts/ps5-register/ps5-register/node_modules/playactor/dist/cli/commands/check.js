"use strict";
var __decorate = (this && this.__decorate) || function (decorators, target, key, desc) {
    var c = arguments.length, r = c < 3 ? target : desc === null ? desc = Object.getOwnPropertyDescriptor(target, key) : desc, d;
    if (typeof Reflect === "object" && typeof Reflect.decorate === "function") r = Reflect.decorate(decorators, target, key, desc);
    else for (var i = decorators.length - 1; i >= 0; i--) if (d = decorators[i]) r = (c < 3 ? d(r) : c > 3 ? d(target, key, r) : d(target, key)) || r;
    return c > 3 && r && Object.defineProperty(target, key, r), r;
};
var __metadata = (this && this.__metadata) || function (k, v) {
    if (typeof Reflect === "object" && typeof Reflect.metadata === "function") return Reflect.metadata(k, v);
};
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
const clime_1 = require("clime");
const options_1 = require("../options");
const model_1 = require("../../discovery/model");
const exit_codes_1 = require("../exit-codes");
class DeviceStatusSignal extends Error {
    constructor(status) {
        super();
        this.status = status;
    }
    print() {
        switch (this.status) {
            case null:
                process.exit(exit_codes_1.ExitCode.DeviceNotFound);
                break;
            case model_1.DeviceStatus.AWAKE:
                process.exit(exit_codes_1.ExitCode.DeviceAwake);
                break;
            case model_1.DeviceStatus.STANDBY:
                process.exit(exit_codes_1.ExitCode.DeviceStandby);
                break;
        }
    }
}
let default_1 = class default_1 extends clime_1.Command {
    execute(options) {
        return __awaiter(this, void 0, void 0, function* () {
            try {
                const device = yield options.findDevice();
                const info = yield device.discover();
                options.logResult(info);
                throw new DeviceStatusSignal(info.status);
            }
            catch (e) {
                if (e instanceof DeviceStatusSignal || e instanceof clime_1.ExpectedError) {
                    throw e;
                }
                options.logError(e);
                throw new DeviceStatusSignal(null);
            }
        });
    }
};
__decorate([
    clime_1.metadata,
    __metadata("design:type", Function),
    __metadata("design:paramtypes", [options_1.DeviceOptions]),
    __metadata("design:returntype", Promise)
], default_1.prototype, "execute", null);
default_1 = __decorate([
    clime_1.command({
        brief: "Detect a device and its state on the network",
        description: `
Detect a device and its state on the network. If found, information
about it will be printed to the console.

In addition, the status of the device can be determined by the exit code
of this process:

    0 - The device was found and is awake
    1 - The device was found and it is in standby
    2 - The device was not found
    `.trim(),
    })
], default_1);
exports.default = default_1;
