import { OskCommand } from "../../osk";
import { OutgoingPacket } from "../base";
import { PacketBuilder } from "../builder";
import { PacketType } from "../types";
export declare class OskControlPacket extends OutgoingPacket {
    private readonly command;
    readonly type = PacketType.OskControl;
    readonly totalLength = 12;
    constructor(command: OskCommand);
    fillBuffer(builder: PacketBuilder): void;
}
