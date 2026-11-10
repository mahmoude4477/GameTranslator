/// <reference types="node" />
import { IPacket } from "../socket/model";
export declare enum RemotePlayCommand {
    Standby = 80,
    Login = 5,
    Heartbeat = 510,
    ProvidePasscode = 32772
}
export declare enum RemotePlayResponseType {
    Passcode = 4,
    Login = 5,
    Heartbeat = 254
}
export declare class RemotePlayIncomingPacket implements IPacket {
    readonly type: number;
    private readonly buffer;
    constructor(type: number, buffer: Buffer);
    toBuffer(): Buffer;
}
export declare class RemotePlayOutgoingPacket implements IPacket {
    readonly type: number;
    private readonly payload?;
    constructor(type: number, payload?: Buffer | undefined);
    toBuffer(): Buffer;
}
