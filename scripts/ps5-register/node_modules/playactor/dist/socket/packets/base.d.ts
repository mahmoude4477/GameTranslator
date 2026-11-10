/// <reference types="node" />
import { IPacket } from "../model";
import { PacketBuilder } from "./builder";
export declare abstract class IncomingPacket implements IPacket {
    abstract type: number;
    toBuffer(): Buffer;
}
export declare abstract class OutgoingPacket implements IPacket {
    abstract type: number;
    abstract totalLength: number;
    abstract fillBuffer(builder: PacketBuilder): void;
    toBuffer(): Buffer;
}
export interface IResultPacket extends IPacket {
    result: number;
    errorCode?: string;
}
export declare class IncomingResultPacket extends IncomingPacket implements IResultPacket {
    private readonly data;
    readonly type: number;
    readonly result: number;
    readonly errorCode?: string;
    constructor(data: Buffer, toErrorCode?: {
        [result: number]: string;
    });
    toBuffer(): Buffer;
}
export declare abstract class EmptyOutgoingPacket extends OutgoingPacket {
    readonly totalLength = 8;
    fillBuffer(): void;
}
