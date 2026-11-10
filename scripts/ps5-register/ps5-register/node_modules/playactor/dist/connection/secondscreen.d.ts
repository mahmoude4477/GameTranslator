import { IDeviceConnection } from "./model";
import { IDiscoveredDevice } from "../discovery/model";
import { IDeviceSocket } from "../socket/model";
import { KeyPress } from "../socket/proc/remote-control";
export declare class SecondScreenDeviceConnection implements IDeviceConnection {
    private readonly resolveDevice;
    private readonly socket;
    constructor(resolveDevice: () => Promise<IDiscoveredDevice>, socket: IDeviceSocket);
    get isConnected(): boolean;
    /**
     * End the connection with the device
     */
    close(): Promise<void>;
    /**
     * Attempt to control the on-screen keyboard for a text field on the
     * screen. If there is no such text field, this method will reject
     * with an error.
     */
    openKeyboard(): Promise<import("../keyboard").OnScreenKeyboard>;
    /**
     * Send a sequence of keypress events
     */
    sendKeys(events: KeyPress[]): Promise<void>;
    /**
     * Put the device into standby mode
     */
    standby(): Promise<void>;
    /**
     * Attempt to start an app or game by its "title ID"
     */
    startTitleId(titleId: string, config?: {
        autoQuitExisting?: boolean;
    }): Promise<void>;
}
