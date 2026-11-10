import { CredentialManager } from "../credentials";
import { IDiscoveredDevice, IDiscoveryConfig, IDiscoveryNetworkFactory, INetworkConfig } from "../discovery/model";
import { IConnectionConfig, IDevice } from "./model";
export declare class PendingDevice implements IDevice {
    readonly description: string;
    private readonly predicate;
    private readonly networkConfig;
    private readonly discoveryConfig;
    private readonly discoveryFactory;
    private readonly credentials;
    private delegate?;
    constructor(description: string, predicate: (device: IDiscoveredDevice) => boolean, networkConfig?: INetworkConfig, discoveryConfig?: Partial<IDiscoveryConfig>, discoveryFactory?: IDiscoveryNetworkFactory, credentials?: CredentialManager);
    discover(config?: INetworkConfig): Promise<IDiscoveredDevice>;
    wake(): Promise<void>;
    openConnection(config?: IConnectionConfig): Promise<import("../connection/model").IDeviceConnection>;
    private resolve;
}
