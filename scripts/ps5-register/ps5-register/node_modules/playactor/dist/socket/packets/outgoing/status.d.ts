import { OutgoingPacket } from "../base";
import { PacketBuilder } from "../builder";
import { PacketType } from "../types";
export declare class StatusPacket extends OutgoingPacket {
    private readonly status;
    readonly type = PacketType.Status;
    readonly totalLength = 12;
    constructor(status?: number);
    fillBuffer(builder: PacketBuilder): void;
}
