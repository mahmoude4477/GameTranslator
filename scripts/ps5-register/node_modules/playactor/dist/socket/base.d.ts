/// <reference types="node" />
import { IDeviceProtocol, IPacket, IPacketCodec } from "./model";
export declare class BufferPacketProcessor {
    private readonly protocol;
    private codec;
    private readonly onNewPacket;
    private reader?;
    private paddingSize?;
    constructor(protocol: IDeviceProtocol, codec: IPacketCodec, onNewPacket: (packet: IPacket) => void);
    onDataReceived(data: Buffer): void;
    setCodec(codec: IPacketCodec): void;
    private dispatchNewPacket;
}
