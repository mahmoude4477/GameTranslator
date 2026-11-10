import { DeviceType, IDiscoveredDevice } from "../discovery/model";
import { ICredentialRequester, ICredentials } from "./model";
export interface IDeviceStrategyMap {
    [DeviceType.PS4]: ICredentialRequester;
    [DeviceType.PS5]: ICredentialRequester;
}
export declare class DeviceTypeStrategyCredentialRequester implements ICredentialRequester {
    private readonly strategies;
    constructor(strategies: IDeviceStrategyMap);
    requestForDevice(device: IDiscoveredDevice): Promise<ICredentials>;
}
