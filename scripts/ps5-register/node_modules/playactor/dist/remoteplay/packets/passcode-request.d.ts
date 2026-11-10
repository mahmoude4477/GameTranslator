import { IncomingPacket, IResultPacket } from "../../socket/packets/base";
/**
 * Sent by the server in response to a LOGIN request when the account
 * is protected by a passcode
 */
export declare class RemotePlayPasscodeRequestPacket extends IncomingPacket implements IResultPacket {
    readonly type: number;
    result: number;
}
