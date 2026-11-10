import { PendingDevice } from "./device/pending";
import { IDiscoveryConfig, INetworkConfig } from "./discovery/model";
export declare type Config = INetworkConfig & Partial<IDiscoveryConfig>;
/**
 * IDevice factories
 */
export declare const Device: {
    /**
     * Return a Device that talks to the first one found on the network
     */
    any(config?: Config): PendingDevice;
    /**
     * Create a Device that talks to the device with the given address
     */
    withAddress(address: string, config?: Config): PendingDevice;
    /**
     * Create a Device that talks to the first device on the network
     * with the given ID
     */
    withId(id: string, config?: Config): PendingDevice;
};
