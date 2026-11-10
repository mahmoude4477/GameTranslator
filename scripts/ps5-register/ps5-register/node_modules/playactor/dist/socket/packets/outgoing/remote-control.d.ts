import { InternalRemoteOperation, RemoteOperation } from "../../remote";
import { OutgoingPacket } from "../base";
import { PacketBuilder } from "../builder";
import { PacketType } from "../types";
export declare class RemoteControlPacket extends OutgoingPacket {
    private readonly op;
    private readonly holdTimeMillis;
    readonly type = PacketType.RemoteControl;
    readonly totalLength = 16;
    constructor(op: RemoteOperation | InternalRemoteOperation, holdTimeMillis?: number);
    fillBuffer(builder: PacketBuilder): void;
}
