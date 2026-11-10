/// <reference types="node" />
import { IDiscoveredDevice } from "../discovery/model";
import { CryptoCodec } from "../socket/crypto-codec";
import { LegacyCryptoStrategy } from "./crypto/legacy";
import { ModernCryptoStrategy } from "./crypto/modern";
export declare function pickCryptoStrategyForDevice(device: IDiscoveredDevice): LegacyCryptoStrategy | ModernCryptoStrategy;
export declare class RemotePlayCrypto {
    private readonly codec;
    private readonly preface;
    static forDeviceAndPin(device: IDiscoveredDevice, pin: string, nonce?: Buffer): RemotePlayCrypto;
    constructor(codec: CryptoCodec, preface: Buffer, nonce: Buffer);
    createSignedPayload(payload: Record<string, string>): Buffer;
    decrypt(payload: Buffer): Buffer;
    private encryptRecord;
}
