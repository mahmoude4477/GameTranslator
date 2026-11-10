"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.CliPassCode = void 0;
const clime_1 = require("clime");
const pass_code_1 = require("../credentials/pass-code");
class CliPassCode {
    constructor(value) {
        this.value = value;
    }
    static cast(input) {
        try {
            return new CliPassCode(pass_code_1.parsePassCodeString(input));
        }
        catch (e) {
            if (e instanceof Error) {
                throw new clime_1.ExpectedError(e.message);
            }
            else {
                throw new clime_1.ExpectedError(`${e}`);
            }
        }
    }
}
exports.CliPassCode = CliPassCode;
