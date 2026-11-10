import { IDiscoveredDevice } from "../discovery/model";
export declare enum RemotePlayVersion {
    PS4_8 = 0,
    PS4_9 = 1,
    PS4_10 = 2,
    PS5_1 = 3
}
export declare function remotePlayVersionFor(device: IDiscoveredDevice): RemotePlayVersion;
export declare function remotePlayVersionToString(version: RemotePlayVersion): "8.0" | "9.0" | "10.0" | "1.0";
export declare enum ErrorReason {
    FAILED = 0,
    INVALID_PSN_ID = 1,
    IN_USE = 2,
    CRASH = 3,
    RP_VERSION = 4,
    UNKNOWN = 5
}
export declare function errorReason(errorCode: string): ErrorReason;
export declare function errorReasonString(errorCode: string): string;
