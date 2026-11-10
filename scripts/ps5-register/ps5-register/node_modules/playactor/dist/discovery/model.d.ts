/// <reference types="node" />
import dgram from "dgram";
export declare const DiscoveryVersions: {
    readonly PS4: "00020020";
    readonly PS5: "00030010";
};
export declare type DiscoveryVersion = typeof DiscoveryVersions[keyof typeof DiscoveryVersions];
export interface IDiscoveryConfig {
    pingIntervalMillis: number;
    timeoutMillis: number;
    uniqueDevices: boolean;
    deviceIp?: string;
    deviceType?: DeviceType;
}
export declare const defaultDiscoveryConfig: IDiscoveryConfig;
export interface INetworkConfig {
    localBindAddress?: string;
    localBindPort?: number;
}
export declare enum DeviceStatus {
    STANDBY = "STANDBY",
    AWAKE = "AWAKE"
}
export interface IDeviceAddress {
    address: string;
    port: number;
    family: "IPv4" | "IPv6";
}
declare const discoveryKeysArrray: readonly ["host-id", "host-name", "host-request-port", "host-type", "system-version", "device-discovery-protocol-version"];
export declare type DiscoveryKey = typeof discoveryKeysArrray[number];
export declare const outgoingDiscoveryKeys: Set<string>;
export declare function isDiscoveryKey(s: string): s is DiscoveryKey;
export declare enum DiscoveryMessageType {
    SRCH = "SRCH",
    WAKEUP = "WAKEUP",
    DEVICE = "DEVICE"
}
export interface IDiscoveryMessage {
    type: DiscoveryMessageType;
    sender: IDeviceAddress;
    version: DiscoveryVersion;
    data: Record<DiscoveryKey | string, string>;
}
export declare enum DeviceType {
    PS4 = "PS4",
    PS5 = "PS5"
}
export interface IDiscoveredDevice {
    address: IDeviceAddress;
    hostRequestPort: number;
    extras: Record<string, string>;
    discoveryVersion: DiscoveryVersion;
    systemVersion: string;
    id: string;
    name: string;
    status: DeviceStatus;
    type: DeviceType;
}
export declare type OnDeviceDiscoveredHandler = (device: IDiscoveredDevice) => void;
export declare type OnDiscoveryMessageHandler = (message: IDiscoveryMessage) => void;
export interface IDiscoveryNetwork {
    close(): void;
    /** Request devices on the network to identify themselves. A specific
      * `deviceIp` may be provided to instead talk to a specific device */
    ping(deviceIp?: string): Promise<void>;
    send(recipientAddress: string, recipientPort: number, type: string, data?: Record<DiscoveryKey | string, unknown>): Promise<void>;
    sendBuffer(recipientAddress: string, recipientPort: number, message: Buffer): Promise<void>;
}
export interface IDiscoveryNetworkFactory {
    createRawMessages(config: INetworkConfig, onMessage: (buffer: Buffer, rinfo: dgram.RemoteInfo) => void): IDiscoveryNetwork;
    createMessages(config: INetworkConfig, onMessage: OnDiscoveryMessageHandler): IDiscoveryNetwork;
    createDevices(config: INetworkConfig, onDevice: OnDeviceDiscoveredHandler): IDiscoveryNetwork;
}
export {};
