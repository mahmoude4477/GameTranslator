import { IDiscoveredDevice } from "../discovery/model";
import { ICredentialRequester, ICredentials } from "./model";
export declare class RootMissingError extends Error {
}
/**
 * The RootManagingCredentialRequester wraps another ICredentialRequester
 * and ensures that root access is available before delegating to that
 * requester, and relinquishes root access afterward by setting the
 * process UID to the provided `restoreUserId`.
 */
export declare class RootManagingCredentialRequester implements ICredentialRequester {
    private readonly delegate;
    private readonly restoreUserId?;
    constructor(delegate: ICredentialRequester, restoreUserId?: number | undefined);
    requestForDevice(device: IDiscoveredDevice): Promise<ICredentials>;
}
