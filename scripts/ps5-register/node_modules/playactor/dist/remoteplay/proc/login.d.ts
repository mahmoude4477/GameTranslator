import { IConnectionConfig } from "../../device/model";
import { IDeviceProc, IDeviceSocket } from "../../socket/model";
export declare class RemotePlayLoginProc implements IDeviceProc {
    config: IConnectionConfig;
    constructor(config: IConnectionConfig);
    perform(socket: IDeviceSocket): Promise<void>;
}
