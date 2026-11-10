"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.LoginResultPacket = exports.LoginResultError = void 0;
const base_1 = require("../base");
const types_1 = require("../types");
var LoginResultError;
(function (LoginResultError) {
    /**
     * The device playactor is running on has not yet been registered
     * with the PlayStation; a pin code displayed on the PlayStation
     * needs to be input as part of the LoginProc to complete
     * registration.
     */
    LoginResultError["PIN_IS_NEEDED"] = "PIN_IS_NEEDED";
    LoginResultError["PASSCODE_IS_NEEDED"] = "PASSCODE_IS_NEEDED";
    LoginResultError["PASSCODE_IS_UNMATCHED"] = "PASSCODE_IS_UNMATCHED";
    LoginResultError["LOGIN_MGR_BUSY"] = "LOGIN_MGR_BUSY";
})(LoginResultError = exports.LoginResultError || (exports.LoginResultError = {}));
const resultToErrorCode = {
    20: LoginResultError.PIN_IS_NEEDED,
    22: LoginResultError.PASSCODE_IS_NEEDED,
    24: LoginResultError.PASSCODE_IS_UNMATCHED,
    30: LoginResultError.LOGIN_MGR_BUSY,
};
class LoginResultPacket extends base_1.IncomingResultPacket {
    constructor(data) {
        super(data, resultToErrorCode);
        this.type = types_1.PacketType.LoginResult;
    }
}
exports.LoginResultPacket = LoginResultPacket;
