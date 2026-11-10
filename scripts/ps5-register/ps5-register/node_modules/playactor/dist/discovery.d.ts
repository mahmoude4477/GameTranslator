import { IDiscoveredDevice, IDiscoveryConfig, IDiscoveryNetworkFactory, INetworkConfig } from "./discovery/model";
export declare class Discovery {
    private readonly networkFactory;
    private readonly discoveryConfig;
    constructor(config?: Partial<IDiscoveryConfig>, networkFactory?: IDiscoveryNetworkFactory);
    discover(networkConfig?: INetworkConfig, discoveryConfig?: Partial<IDiscoveryConfig>): AsyncIterable<IDiscoveredDevice>;
}
