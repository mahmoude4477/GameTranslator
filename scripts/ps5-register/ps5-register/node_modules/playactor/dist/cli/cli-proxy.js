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
exports.SudoCliProxy = exports.CliProxyError = void 0;
const child_process_1 = require("child_process");
/**
 * Represents an error encountered by the "proxied" subprocess.  When
 * "printed" by the CLI framework, it will print the error message to
 * stderr and exit this process with the same exit code with which the
 * subprocess exited.
 */
class CliProxyError extends Error {
    constructor(message, errorCode) {
        super(message);
        this.errorCode = errorCode;
    }
    print(stdout, stderr) {
        stderr.write(this.message);
        process.exit(this.errorCode);
    }
}
exports.CliProxyError = CliProxyError;
/**
 * An implementation of ICliProxy that prefixes the provided invocation
 * with `sudo` (or whatever other program you prefer)
 */
class SudoCliProxy {
    constructor(sudo = "sudo") {
        this.sudo = sudo;
    }
    invoke(invocation) {
        return __awaiter(this, void 0, void 0, function* () {
            const result = yield child_process_1.spawnSync(this.sudo, invocation, {
                stdio: "inherit",
            });
            if (result.error) {
                if (result.error.errno) {
                    throw new CliProxyError(result.error.message, result.error.errno);
                }
                throw new CliProxyError(result.error.message, 0);
            }
        });
    }
}
exports.SudoCliProxy = SudoCliProxy;
