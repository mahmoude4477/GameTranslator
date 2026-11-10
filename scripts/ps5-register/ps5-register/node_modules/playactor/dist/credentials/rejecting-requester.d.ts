import { ICredentialRequester, ICredentials } from "./model";
/**
 * A simple ICredentialRequester implementation that always returns
 * a rejected Promise when credentials are requested.
 */
export declare class RejectingCredentialRequester implements ICredentialRequester {
    private readonly message;
    constructor(message?: string);
    requestForDevice(): Promise<ICredentials>;
}
