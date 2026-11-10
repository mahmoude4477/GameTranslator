import { IInputOutput } from "../../cli/io";
import { IDiscoveredDevice } from "../../discovery/model";
import { ICredentialRequester, ICredentials } from "../model";
import { OauthStrategy } from "./model";
interface RawAccountInfo {
    scopes: string;
    expiration: string;
    client_id: string;
    dcim_id: string;
    grant_type: string;
    user_id: string;
    user_uuid: string;
    online_id: string;
    country_code: string;
    language_code: string;
    community_domain: string;
    is_sub_account: boolean;
}
export declare function extractAccountId(accountInfo: RawAccountInfo): string;
export declare function registKeyToCredential(registKey: string): string;
export declare class OauthCredentialRequester implements ICredentialRequester {
    private io;
    private strategy;
    constructor(io: IInputOutput, strategy: OauthStrategy);
    requestForDevice(device: IDiscoveredDevice): Promise<ICredentials>;
    private performOauth;
    private registerWithDevice;
    private exchangeCodeForAccess;
}
export {};
