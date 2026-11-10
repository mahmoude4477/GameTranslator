/// <reference types="node" />
import { IncomingPacket, IResultPacket } from "../../socket/packets/base";
export declare class RemotePlayLoginResultPacket extends IncomingPacket implements IResultPacket {
    readonly type: number;
    readonly result: number;
    readonly errorCode?: string | undefined;
    constructor(data: Buffer);
}
