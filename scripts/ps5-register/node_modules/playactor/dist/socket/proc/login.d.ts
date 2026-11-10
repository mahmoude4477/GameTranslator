import { ICredentials } from "../../credentials/model";
import { IDeviceProc, IDeviceSocket } from "../model";
import { ILoginConfig } from "../packets/outgoing/login";
export declare class LoginProc implements IDeviceProc {
    private readonly credentials;
    private readonly config;
    constructor(credentials: ICredentials, config?: Partial<ILoginConfig>);
    perform(socket: IDeviceSocket): Promise<void>;
}
