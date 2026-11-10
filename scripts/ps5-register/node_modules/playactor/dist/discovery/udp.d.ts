/// <reference types="node" />
import dgram from "dgram";
import { DiscoveryVersion, IDiscoveryNetwork, IDiscoveryNetworkFactory, INetworkConfig, OnDeviceDiscoveredHandler, OnDiscoveryMessageHandler } from "./model";
declare class UdpSocketManager {
    private readonly sockets;
    acquire(port: number): {
        socket: dgram.Socket;
        isNew?: undefined;
    } | {
        socket: dgram.Socket;
        isNew: boolean;
    };
    release(port: number): void;
}
export declare class UdpDiscoveryNetwork implements IDiscoveryNetwork {
    private readonly socketManager;
    private readonly boundPort;
    private readonly socket;
    private readonly port;
    private readonly version;
    constructor(socketManager: UdpSocketManager, boundPort: number, socket: dgram.Socket, port: number, version: DiscoveryVersion);
    close(): void;
    ping(deviceIp?: string): Promise<void>;
    send(recipientAddress: string, recipientPort: number, type: string, data?: Record<string, unknown>): Promise<void>;
    sendBuffer(recipientAddress: string, recipientPort: number, message: Buffer): Promise<void>;
}
export declare class UdpDiscoveryNetworkFactory implements IDiscoveryNetworkFactory {
    private readonly port;
    private readonly version;
    private readonly socketManager;
    constructor(port: number, version: DiscoveryVersion, socketManager?: UdpSocketManager);
    createMessages(config: INetworkConfig, onMessage: OnDiscoveryMessageHandler): UdpDiscoveryNetwork;
    createRawMessages(config: INetworkConfig, onMessage: (buffer: Buffer, rinfo: dgram.RemoteInfo) => void): UdpDiscoveryNetwork;
    createDevices(config: INetworkConfig, onDevice: OnDeviceDiscoveredHandler): UdpDiscoveryNetwork;
}
export {};
