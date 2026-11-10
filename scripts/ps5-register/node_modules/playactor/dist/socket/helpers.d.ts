import { IDeviceSocket, IPacket } from "./model";
import { IResultPacket } from "./packets/base";
export declare function receiveWhere(socket: IDeviceSocket, predicate: (packet: IPacket) => boolean): Promise<IPacket>;
export declare function receiveType<T extends IPacket>(socket: IDeviceSocket, type: number): Promise<T>;
export declare class RpcError extends Error {
    readonly result: number;
    readonly code?: string | undefined;
    constructor(result: number, code?: string | undefined);
}
export declare function performRpc<R extends IResultPacket>(socket: IDeviceSocket, request: IPacket, ...resultTypes: number[]): Promise<R>;
