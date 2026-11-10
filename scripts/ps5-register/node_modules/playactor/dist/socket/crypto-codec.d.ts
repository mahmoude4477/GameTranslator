/// <reference types="node" />
import { IPacketCodec } from "./model";
export declare class CryptoCodec implements IPacketCodec {
    readonly initVector: Buffer;
    readonly seed: Buffer;
    private readonly algorithm;
    readonly paddingSize = 16;
    private readonly padEncoding;
    private readonly chunkDecoding;
    private readonly cipher;
    private readonly decipher;
    private pending?;
    constructor(initVector: Buffer, seed?: Buffer, algorithm?: string);
    encode(packet: Buffer): Buffer;
    decode(packet: Buffer): Buffer;
}
