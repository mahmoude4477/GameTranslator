import { IDeviceProc, IDeviceSocket } from "../model";
export declare class StandbyProc implements IDeviceProc {
    perform(socket: IDeviceSocket): Promise<void>;
}
