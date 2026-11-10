import { EmptyOutgoingPacket } from "../base";
import { PacketType } from "../types";
export declare class ByePacket extends EmptyOutgoingPacket {
    readonly type = PacketType.Bye;
}
