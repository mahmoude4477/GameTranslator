import { ICredentials, ICredentialStorage } from "./model";
export declare function determineDefaultFile(): string;
export declare class DiskCredentialsStorage implements ICredentialStorage {
    readonly filePath: string;
    constructor(filePath?: string);
    read(deviceId: string): Promise<ICredentials | null>;
    write(deviceId: string, credentials: ICredentials): Promise<void>;
    private readCredentialsMap;
}
