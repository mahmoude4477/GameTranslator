import { IInputOutput } from "../cli/io";
import { IDiscoveredDevice, IDiscoveryNetworkFactory, INetworkConfig } from "../discovery/model";
import { ICredentialRequester, ICredentials } from "./model";
export interface IEmulatorOptions {
    hostId: string;
    hostName: string;
}
/**
 * The MimCredentialRequester works by emulating a PlayStation device
 * on the network that the PlayStation App can connect to. It then
 * acts as a "man in the middle" to intercept the credentials that
 * the app is passing.
 */
export declare class MimCredentialRequester implements ICredentialRequester {
    private readonly networkFactory;
    private readonly networkConfig;
    private readonly io?;
    private readonly emulatorOptions;
    constructor(networkFactory: IDiscoveryNetworkFactory, networkConfig: INetworkConfig, io?: IInputOutput | undefined, emulatorOptions?: Partial<IEmulatorOptions>);
    requestForDevice(device: IDiscoveredDevice): Promise<ICredentials>;
    private emulateUntilWoken;
}
