/// <reference types="node" />
import { IRemotePlayCredentials } from "../../credentials/model";
import { CryptoCodec } from "../../socket/crypto-codec";
import { RemotePlayVersion } from "../model";
import { ICryptoStrategy } from "./model";
export declare function generateIv(version: RemotePlayVersion, nonce: Buffer, counter: bigint): Buffer;
export declare abstract class BaseCryptoStrategy implements ICryptoStrategy {
    private readonly version;
    constructor(version: RemotePlayVersion);
    createCodecForPin(pin: string, nonce: Buffer): {
        preface: Buffer;
        codec: CryptoCodec;
    };
    protected abstract generatePinSeed(padding: Buffer, pinNumber: number): Buffer;
    protected abstract signPadding(nonce: Buffer, padding: Buffer): void;
    createCodecForAuth(creds: IRemotePlayCredentials, serverNonce: Buffer, counter: bigint): CryptoCodec;
    protected abstract generateAuthSeed(key: Buffer, serverNonce: Buffer): Buffer;
    protected abstract transformServerNonceForAuth(serverNonce: Buffer): Buffer;
}
