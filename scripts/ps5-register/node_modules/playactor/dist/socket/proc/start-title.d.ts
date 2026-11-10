import { IDeviceProc, IDeviceSocket } from "../model";
export declare class StartTitleProc implements IDeviceProc {
    private readonly titleId;
    constructor(titleId: string);
    perform(socket: IDeviceSocket): Promise<void>;
}
