/// <reference types="node" />
import { IRemotePlayCredentials } from "../credentials/model";
import { IPacketCodec } from "../socket/model";
import { ICryptoStrategy } from "./crypto/model";
export declare class RemotePlayPacketCodec implements IPacketCodec {
    private readonly crypto;
    private readonly creds;
    private readonly serverNonce;
    private encryptCounter;
    private decryptCounter;
    constructor(crypto: ICryptoStrategy, creds: IRemotePlayCredentials, serverNonce: Buffer);
    encode(packet: Buffer): Buffer;
    decode(packet: Buffer): Buffer;
    encodeBuffer(buffer: Buffer): Buffer;
    decodeBuffer(buffer: Buffer): Buffer;
}
