/// <reference types="node" />
import { IDeviceRegistration } from "../credentials/model";
import { IDiscoveredDevice } from "../discovery/model";
import { RemotePlayCrypto } from "./crypto";
export interface IRemotePlayCredentials {
    accountId: string;
}
export interface IRemotePlayRegistrationCredentials extends IRemotePlayCredentials {
    pin: string;
}
export declare class RemotePlayRegistration {
    createPayload(crypto: RemotePlayCrypto, device: IDiscoveredDevice, credentials: IRemotePlayRegistrationCredentials): Buffer;
    register(device: IDiscoveredDevice, credentials: IRemotePlayRegistrationCredentials): Promise<IDeviceRegistration>;
    private searchForDevice;
    private sendRegistrationRequest;
    private urlFor;
    private versionFor;
}
