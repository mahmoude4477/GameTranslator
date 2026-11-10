/// <reference types="node" />
import { OutgoingPacket } from "../base";
import { PacketBuilder } from "../builder";
import { PacketType } from "../types";
export declare class HandshakePacket extends OutgoingPacket {
    private readonly key;
    private readonly seed;
    type: PacketType;
    totalLength: number;
    constructor(key: Buffer, seed: Buffer);
    fillBuffer(builder: PacketBuilder): void;
}
