/// <reference types="node" />
import { OskActionType, OskFlags, OskInputType } from "../../osk";
import { IncomingResultPacket } from "../base";
import { PacketType } from "../types";
export declare class OskStartResultPacket extends IncomingResultPacket {
    type: PacketType;
    /**
     * oskType is a bit mask structed as:
     *
     * 0x0F0 - action type
     * 0x003 - basic input types
     * 0x300 - extended input types (meant to be >>6 to become 4, 8)
     * 0x004 - multi-line flag
     * 0x008 - password mode flag
     * 0x400 - auto capitalize flag
     */
    readonly oskType: number;
    readonly maxLength: number;
    readonly initialContent: string;
    constructor(data: Buffer);
    get actionType(): OskActionType;
    get inputType(): OskInputType;
    get flags(): OskFlags;
}
