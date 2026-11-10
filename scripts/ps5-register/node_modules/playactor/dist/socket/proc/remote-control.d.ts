import { IDeviceProc, IDeviceSocket } from "../model";
import { RemoteOperation } from "../remote";
export interface KeyPress {
    key: RemoteOperation;
    holdTimeMillis?: number;
}
export declare class RemoteControlProc implements IDeviceProc {
    private readonly events;
    constructor(events: KeyPress[]);
    perform(socket: IDeviceSocket): Promise<void>;
    private sendKeys;
}
