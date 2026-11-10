import { OutgoingPacket } from "../base";
import { PacketBuilder } from "../builder";
import { PacketType } from "../types";
export interface ILoginConfig {
    appLabel: string;
    model: string;
    osVersion: string;
    /** 4-byte user security code */
    passCode: string;
    /** authentication code from the device */
    pinCode: string;
    appendHostnameToModel: boolean;
}
export declare class LoginPacket extends OutgoingPacket {
    private readonly userCredential;
    readonly type = PacketType.Login;
    readonly totalLength = 384;
    private readonly config;
    readonly info = 1025;
    constructor(userCredential: string, config?: Partial<ILoginConfig>);
    fillBuffer(builder: PacketBuilder): void;
}
