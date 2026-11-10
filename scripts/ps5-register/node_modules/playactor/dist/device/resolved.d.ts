import { IDeviceConnection } from "../connection/model";
import { IDiscoveredDevice, IDiscoveryConfig, IDiscoveryNetworkFactory, INetworkConfig } from "../discovery/model";
import { IWakerFactory } from "../waker";
import { DeviceCapability, IConnectionConfig, IResolvedDevice } from "./model";
export declare class ResolvedDevice implements IResolvedDevice {
    private readonly wakerFactory;
    private readonly description;
    private readonly networkConfig;
    private readonly discoveryConfig;
    private readonly discoveryFactory;
    constructor(wakerFactory: IWakerFactory, description: IDiscoveredDevice, networkConfig: INetworkConfig, discoveryConfig: Partial<IDiscoveryConfig>, discoveryFactory: IDiscoveryNetworkFactory);
    discover(): Promise<IDiscoveredDevice>;
    wake(): Promise<void>;
    /**
     * discover() returns the original IDiscoveredDevice, but if
     * you need to get an updated description for any reason, this
     * method will do the job.
     */
    resolve(config?: INetworkConfig): Promise<IDiscoveredDevice>;
    openConnection(config?: IConnectionConfig): Promise<IDeviceConnection>;
    isSupported(capability: DeviceCapability): boolean;
    private startWaker;
}
