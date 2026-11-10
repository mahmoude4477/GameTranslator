import { ICredentials, ICredentialStorage } from "./model";
/**
 * Write-only storage delegates to another [ICredentialStorage]
 * for writes, but only returns credentials from that storage
 * *after* it has written to them. This is useful if you want to
 * force authentication.
 */
export declare class WriteOnlyStorage implements ICredentialStorage {
    private readonly delegate;
    private hasWritten;
    constructor(delegate: ICredentialStorage);
    read(deviceId: string): Promise<ICredentials | null>;
    write(deviceId: string, credentials: ICredentials): Promise<void>;
}
