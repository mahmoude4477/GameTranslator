/// <reference types="node" />
import { IDeviceProc, IDeviceSocket } from "../model";
export declare class HandshakeProc implements IDeviceProc {
    perform(socket: IDeviceSocket): Promise<void>;
    signWithPublicKey(bytes: Buffer): Buffer;
}
