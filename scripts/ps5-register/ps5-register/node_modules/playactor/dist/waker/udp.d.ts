/// <reference types="node" />
import { IDiscoveredDevice, INetworkConfig } from "../discovery/model";
import { IWakerNetwork, IWakerNetworkFactory } from "./model";
export declare const wakePortsByType: {
    PS4: number;
    PS5: number;
};
export declare class UdpWakerNetwork implements IWakerNetwork {
    readonly config: INetworkConfig;
    private socket?;
    constructor(config: INetworkConfig);
    close(): void;
    sendTo(device: IDiscoveredDevice, message: Buffer): Promise<void>;
    private open;
}
export declare class UdpWakerNetworkFactory implements IWakerNetworkFactory {
    create(config: INetworkConfig): UdpWakerNetwork;
}
