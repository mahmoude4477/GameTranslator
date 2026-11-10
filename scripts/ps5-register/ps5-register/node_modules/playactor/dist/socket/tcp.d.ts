/// <reference types="node" />
import net from "net";
import { IDiscoveredDevice } from "../discovery/model";
import { IDeviceProc, IDeviceProtocol, IDeviceSocket, IPacket, IPacketCodec, ISocketConfig } from "./model";
export interface IOptions {
    refSocket: boolean;
}
export declare class TcpDeviceSocket implements IDeviceSocket {
    readonly device: IDiscoveredDevice;
    private readonly protocol;
    private readonly stream;
    private readonly options;
    readonly openedTimestamp: number;
    static connectTo(device: IDiscoveredDevice, config: ISocketConfig, options?: IOptions): Promise<TcpDeviceSocket>;
    private readonly receivers;
    private codec;
    private readonly processor;
    private stayAliveUntil;
    private isClosed;
    constructor(device: IDiscoveredDevice, protocol: IDeviceProtocol, stream: net.Socket, options?: IOptions, initialCodec?: IPacketCodec, openedTimestamp?: number);
    get protocolVersion(): import("./model").IProtocolVersion;
    get isConnected(): boolean;
    execute<R>(proc: IDeviceProc<R>): Promise<R>;
    receive(): AsyncIterable<IPacket>;
    requestKeepAlive(extraLifeMillis: number): void;
    send(packet: IPacket): Promise<void>;
    setCodec(codec: IPacketCodec): void;
    close(): Promise<void>;
    private onPacketReceived;
    private handleEnd;
}
