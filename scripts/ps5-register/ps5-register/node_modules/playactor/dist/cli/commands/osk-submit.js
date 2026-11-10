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
let default_1 = class default_1 extends clime_1.Command {
    execute(text = undefined, deviceSpec) {
        return __awaiter(this, void 0, void 0, function* () {
            const device = yield deviceSpec.findDevice();
            const connection = yield device.openConnection(deviceSpec.connectionConfig);
            try {
                if (!connection.openKeyboard) {
                    throw new Error("osk-submit not supported for this device");
                }
                const osk = yield connection.openKeyboard();
                if (text) {
                    yield osk.setText(text);
                }
                yield osk.submit();
            }
            finally {
                yield connection.close();
            }
        });
    }
};
__decorate([
    __param(0, clime_1.param({
        description: "Text with which to replace any existing input",
        name: "text",
        type: String,
    })),
    __metadata("design:type", Function),
    __metadata("design:paramtypes", [Object, options_1.DeviceOptions]),
    __metadata("design:returntype", Promise)
], default_1.prototype, "execute", null);
default_1 = __decorate([
    clime_1.command({
        brief: "Submit the on-screen keyboard",
        description: `
Submit the on-screen keyboard, optionally providing text to fill
the input field with.
    `.trim(),
    })
], default_1);
exports.default = default_1;
