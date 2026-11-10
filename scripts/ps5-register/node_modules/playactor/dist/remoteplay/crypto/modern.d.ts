/// <reference types="node" />
import { DeviceType } from "../../discovery/model";
import { RemotePlayVersion } from "../model";
import { BaseCryptoStrategy } from "./base";
export declare function generateSeed(deviceType: DeviceType, pin: number, initKeyOff: number): Buffer;
export declare function transformServerNonceForAuth(deviceType: DeviceType, serverNonce: Buffer): Buffer;
export declare function generateAuthSeed(deviceType: DeviceType, authKey: Buffer, serverNonce: Buffer): Buffer;
/**
 * Handles crypto for PS5s, and PS4s on RemotePlay 10.0+
 */
export declare class ModernCryptoStrategy extends BaseCryptoStrategy {
    private readonly deviceType;
    constructor(deviceType: DeviceType, version: RemotePlayVersion);
    protected generatePinSeed(padding: Buffer, pinNumber: number): Buffer;
    protected signPadding(nonce: Buffer, padding: Buffer): void;
    protected generateAuthSeed(key: Buffer, serverNonce: Buffer): Buffer;
    protected transformServerNonceForAuth(serverNonce: Buffer): Buffer;
}
