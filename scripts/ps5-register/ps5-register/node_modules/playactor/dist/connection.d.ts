import { RemotePlayDeviceConnection } from "./connection/remoteplay";
import { SecondScreenDeviceConnection } from "./connection/secondscreen";
import { ICredentials, IRemotePlayCredentials } from "./credentials/model";
import { IConnectionConfig, IResolvedDevice } from "./device/model";
import { IDiscoveredDevice } from "./discovery/model";
import { Waker } from "./waker";
export declare function openRemotePlay(waker: Waker, device: IResolvedDevice, discovered: IDiscoveredDevice, config: IConnectionConfig, creds: IRemotePlayCredentials): Promise<RemotePlayDeviceConnection>;
export declare function openSecondScreen(waker: Waker, device: IResolvedDevice, discovered: IDiscoveredDevice, config: IConnectionConfig, creds: ICredentials): Promise<SecondScreenDeviceConnection>;
export declare function openConnection(waker: Waker, device: IResolvedDevice, discovered: IDiscoveredDevice, config: IConnectionConfig, creds: ICredentials): Promise<RemotePlayDeviceConnection> | Promise<SecondScreenDeviceConnection>;
