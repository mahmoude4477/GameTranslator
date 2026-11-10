/// <reference types="node" />
import { IDiscoveryNetwork } from "./model";
export declare class CompositeDiscoveryNetwork implements IDiscoveryNetwork {
    private readonly delegates;
    constructor(delegates: IDiscoveryNetwork[]);
    close(): void;
    ping(deviceIp?: string): Promise<void>;
    send(recipientAddress: string, recipientPort: number, type: string, data?: Record<string, unknown>): Promise<void>;
    sendBuffer(recipientAddress: string, recipientPort: number, message: Buffer): Promise<void>;
}
