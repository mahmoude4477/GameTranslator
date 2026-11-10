import { EmptyOutgoingPacket } from "../base";
import { PacketType } from "../types";
export declare class OskStartPacket extends EmptyOutgoingPacket {
    readonly type = PacketType.OskStart;
}
