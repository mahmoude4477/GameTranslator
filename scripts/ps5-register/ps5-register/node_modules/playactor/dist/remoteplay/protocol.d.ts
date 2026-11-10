/// <reference types="node" />
import { Options } from "got";
import { IDiscoveredDevice } from "../discovery/model";
import { IDeviceProtocol, IPacket } from "../socket/model";
import { TypedPacketReader } from "../socket/protocol/base";
export declare const REST_PORT = 9295;
export declare const CRYPTO_NONCE_LENGTH = 16;
export declare function padBuffer(buffer: Buffer, expectedBytes?: number): Buffer;
export declare function parseHexBytes(data: string): Buffer;
export declare function parseBody<R>(body: Buffer): R;
export declare function request(url: string, options: Options): Promise<import("got").Response<Buffer>>;
export declare function typedPath(device: IDiscoveredDevice, path: string): string;
export declare function urlWith(device: IDiscoveredDevice, path: string): string;
export declare class RemotePlayPacketReader extends TypedPacketReader {
    constructor();
    protected readType(buffer: Buffer): number;
    protected createDefaultPacket(type: number, buffer: Buffer): IPacket;
}
export declare const RemotePlayDeviceProtocol: IDeviceProtocol;
