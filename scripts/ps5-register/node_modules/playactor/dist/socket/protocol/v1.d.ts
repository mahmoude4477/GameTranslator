/// <reference types="node" />
import { IDeviceProtocol, IPacket } from "../model";
import { TypedPacketReader } from "./base";
export declare class PacketReaderV1 extends TypedPacketReader {
    constructor();
    protected readType(buffer: Buffer): number;
    protected createDefaultPacket(type: number, buffer: Buffer): IPacket;
}
export declare const DeviceProtocolV1: IDeviceProtocol;
