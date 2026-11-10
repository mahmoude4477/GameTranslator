/// <reference types="node" />
import { BaseCryptoStrategy } from "./base";
export declare class LegacyCryptoStrategy extends BaseCryptoStrategy {
    protected generatePinSeed(_padding: Buffer, pinNumber: number): Buffer;
    protected signPadding(nonce: Buffer, padding: Buffer): void;
    protected generateAuthSeed(key: Buffer, serverNonce: Buffer): Buffer;
    protected transformServerNonceForAuth(serverNonce: Buffer): Buffer;
}
