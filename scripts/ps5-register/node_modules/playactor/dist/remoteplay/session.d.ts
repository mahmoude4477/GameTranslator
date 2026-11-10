import { IRemotePlayCredentials } from "../credentials/model";
import { IConnectionConfig } from "../device/model";
import { IDiscoveredDevice } from "../discovery/model";
import { TcpDeviceSocket } from "../socket/tcp";
export declare function openSession(device: IDiscoveredDevice, config: IConnectionConfig, creds: IRemotePlayCredentials): Promise<TcpDeviceSocket>;
