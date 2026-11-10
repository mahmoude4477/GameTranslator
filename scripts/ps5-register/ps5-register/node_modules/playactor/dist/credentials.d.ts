import { ICredentialRequester, ICredentialStorage } from "./credentials/model";
import { IDiscoveredDevice } from "./discovery/model";
export declare class CredentialsError extends Error {
}
export declare class CredentialManager {
    private readonly requester;
    private readonly storage;
    constructor(requester?: ICredentialRequester, storage?: ICredentialStorage);
    getForDevice(device: IDiscoveredDevice): Promise<import("./credentials/model").ICredentials>;
}
