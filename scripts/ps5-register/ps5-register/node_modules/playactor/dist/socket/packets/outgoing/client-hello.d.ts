import { IProtocolVersion } from "../../model";
import { OutgoingPacket } from "../base";
import { PacketBuilder } from "../builder";
import { PacketType } from "../types";
export declare class ClientHelloPacket extends OutgoingPacket {
    private readonly protocolVersion;
    readonly type = PacketType.Hello;
    readonly totalLength = 28;
    constructor(protocolVersion: IProtocolVersion);
    fillBuffer(builder: PacketBuilder): void;
}
