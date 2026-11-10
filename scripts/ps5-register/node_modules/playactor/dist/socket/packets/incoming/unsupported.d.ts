/// <reference types="node" />
import { IncomingPacket } from "../base";
export declare class UnsupportedIncomingPacket extends IncomingPacket {
    type: number;
    constructor(data: Buffer);
}
