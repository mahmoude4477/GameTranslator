import { OnScreenKeyboard } from "../../keyboard";
import { IDeviceProc, IDeviceSocket } from "../model";
export declare class OpenKeyboardProc implements IDeviceProc<OnScreenKeyboard> {
    perform(socket: IDeviceSocket): Promise<OnScreenKeyboard>;
}
