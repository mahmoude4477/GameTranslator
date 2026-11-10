/// <reference types="node" />
import { IncomingResultPacket } from "../base";
import { PacketType } from "../types";
export declare enum LoginResultError {
    /**
     * The device playactor is running on has not yet been registered
     * with the PlayStation; a pin code displayed on the PlayStation
     * needs to be input as part of the LoginProc to complete
     * registration.
     */
    PIN_IS_NEEDED = "PIN_IS_NEEDED",
    PASSCODE_IS_NEEDED = "PASSCODE_IS_NEEDED",
    PASSCODE_IS_UNMATCHED = "PASSCODE_IS_UNMATCHED",
    LOGIN_MGR_BUSY = "LOGIN_MGR_BUSY"
}
export declare class LoginResultPacket extends IncomingResultPacket {
    type: PacketType;
    constructor(data: Buffer);
}
