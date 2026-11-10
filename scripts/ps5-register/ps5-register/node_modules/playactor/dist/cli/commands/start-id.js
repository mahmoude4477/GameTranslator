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
var __param = (this && this.__param) || function (paramIndex, decorator) {
    return function (target, key) { decorator(target, key, paramIndex); }
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
class StartTitleOptions extends options_1.DeviceOptions {
    constructor() {
        super(...arguments);
        this.dontAutoQuit = false;
    }
}
__decorate([
    clime_1.option({
        name: "no-auto-quit",
        description: "Don't quit an already running app",
        toggle: true,
    }),
    __metadata("design:type", Object)
], StartTitleOptions.prototype, "dontAutoQuit", void 0);
let default_1 = class default_1 extends clime_1.Command {
    /* eslint-disable @typescript-eslint/indent */
    execute(titleId, deviceSpec) {
        return __awaiter(this, void 0, void 0, function* () {
            const device = yield deviceSpec.findDevice();
            const connection = yield device.openConnection(deviceSpec.connectionConfig);
            try {
                if (!connection.startTitleId) {
                    throw new Error("start-id not supported for this device");
                }
                yield connection.startTitleId(titleId, {
                    autoQuitExisting: !deviceSpec.dontAutoQuit,
                });
            }
            finally {
                yield connection.close();
            }
        });
    }
};
__decorate([
    __param(0, clime_1.param({
        required: true,
    })),
    __metadata("design:type", Function),
    __metadata("design:paramtypes", [String, StartTitleOptions]),
    __metadata("design:returntype", Promise)
], default_1.prototype, "execute", null);
default_1 = __decorate([
    clime_1.command({
        description: "Start an app or game by its Title ID",
    })
], default_1);
exports.default = default_1;
