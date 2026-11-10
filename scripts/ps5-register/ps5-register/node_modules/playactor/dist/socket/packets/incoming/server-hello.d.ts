/// <reference types="node" />
import { IncomingPacket, IResultPacket } from "../base";
import { PacketType } from "../types";
export declare class ServerHelloPacket extends IncomingPacket implements IResultPacket {
    type: PacketType;
    readonly version: number;
    readonly result: number;
    readonly option: number;
    readonly seed: Buffer;
    errorCode?: string;
    constructor(data: Buffer);
}
