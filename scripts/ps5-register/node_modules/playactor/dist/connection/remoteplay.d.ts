import { IDeviceSocket } from "../socket/model";
import { IDeviceConnection } from "./model";
export declare class RemotePlayDeviceConnection implements IDeviceConnection {
    private socket;
    constructor(socket: IDeviceSocket);
    get isConnected(): boolean;
    close(): Promise<void>;
    standby(): Promise<void>;
}
