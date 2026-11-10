import { IDiscoveredDevice } from "../discovery/model";
export interface ISecondScreenCredentials {
    "app-type": "c";
    "auth-type": "C";
    "client-type": "a" | "i";
    "model"?: "m" | "w";
    "user-credential": string;
}
export interface IDeviceRegistration {
    "AP-Bssid": string;
    "AP-Name": string;
    "PS5-Mac"?: string;
    "PS5-RegistKey"?: string;
    "PS5-Nickname"?: string;
    "RP-KeyType": string;
    "RP-Key": string;
    "PS4-Mac"?: string;
    "PS4-RegistKey"?: string;
    "PS4-Nickname"?: string;
}
export interface IRemotePlayCredentials {
    "app-type": "r";
    "auth-type": "R";
    "client-type": "vr";
    "model"?: "m" | "w";
    "user-credential": string;
    accountId: string;
    registration: IDeviceRegistration;
}
export declare type ICredentials = ISecondScreenCredentials | IRemotePlayCredentials;
export declare function isRemotePlay(credentials: ICredentials): credentials is IRemotePlayCredentials;
export interface ICredentialStorage {
    read(deviceId: string): Promise<ICredentials | null>;
    write(deviceId: string, credentials: ICredentials): Promise<void>;
}
export interface ICredentialRequester {
    requestForDevice(device: IDiscoveredDevice): Promise<ICredentials>;
}
