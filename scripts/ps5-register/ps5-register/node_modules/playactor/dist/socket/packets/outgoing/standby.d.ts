import { EmptyOutgoingPacket } from "../base";
import { PacketType } from "../types";
export declare class StandbyPacket extends EmptyOutgoingPacket {
    readonly type = PacketType.Standby;
}
