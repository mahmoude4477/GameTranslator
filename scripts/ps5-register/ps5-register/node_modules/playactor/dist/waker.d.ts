import { CredentialManager } from "./credentials";
import { IDiscoveredDevice, IDiscoveryConfig, IDiscoveryNetworkFactory, INetworkConfig } from "./discovery/model";
import { IWakerNetworkFactory } from "./waker/model";
export declare enum WakeResult {
    ALREADY_AWAKE = 0,
    SUCCESS = 1
}
export declare class Waker {
    readonly credentials: CredentialManager;
    private readonly discoveryConfig;
    readonly networkFactory: IWakerNetworkFactory;
    private readonly discoveryFactory;
    constructor(credentials: CredentialManager, discoveryConfig: Partial<IDiscoveryConfig>, networkFactory?: IWakerNetworkFactory, discoveryFactory?: IDiscoveryNetworkFactory);
    wake(device: IDiscoveredDevice, config?: INetworkConfig): Promise<WakeResult>;
    private deviceAwakened;
}
export interface IWakerFactory {
    create(): Waker;
}
export declare class SimpleWakerFactory implements IWakerFactory {
    private readonly config;
    constructor(config: {
        credentials: CredentialManager;
        discoveryConfig: Partial<IDiscoveryConfig>;
        networkFactory: IWakerNetworkFactory;
        discoveryFactory: IDiscoveryNetworkFactory;
    });
    create(): Waker;
}
