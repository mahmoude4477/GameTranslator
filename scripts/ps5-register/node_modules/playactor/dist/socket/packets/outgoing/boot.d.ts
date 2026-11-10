import { OutgoingPacket } from "../base";
import { PacketBuilder } from "../builder";
import { PacketType } from "../types";
export declare class BootPacket extends OutgoingPacket {
    readonly titleId: string;
    readonly type = PacketType.Boot;
    readonly totalLength = 24;
    constructor(titleId: string);
    fillBuffer(builder: PacketBuilder): void;
}
