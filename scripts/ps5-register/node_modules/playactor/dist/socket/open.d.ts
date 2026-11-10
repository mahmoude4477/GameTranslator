import { ICredentials } from "../credentials/model";
import { IDiscoveredDevice, INetworkConfig } from "../discovery/model";
import { IWakerNetworkFactory } from "../waker/model";
import { IDeviceSocket, ISocketConfig } from "./model";
import { ILoginConfig } from "./packets/outgoing/login";
/**
 * If thrown when trying to authenticate, the device has probably
 * closed the socket and, because we're not authenticated, will
 * be unable to do so.
 */
export declare class ConnectionRefusedError extends Error {
    constructor();
}
export declare function openSocket(wakerFactory: IWakerNetworkFactory, device: IDiscoveredDevice, credentials: ICredentials, socketConfig?: Partial<ISocketConfig>, networkConfig?: INetworkConfig, loginConfig?: Partial<ILoginConfig>): Promise<IDeviceSocket>;
