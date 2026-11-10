/// <reference types="node" />
import { DiscoveryMessageType } from "./model";
interface IParsedMessage {
    type: DiscoveryMessageType;
    [key: string]: string;
}
export declare function parseMessage(raw: Buffer): IParsedMessage;
export {};
