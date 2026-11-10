import { OutgoingPacket } from "../base";
import { PacketBuilder } from "../builder";
import { PacketType } from "../types";
export interface OskChangeStringOptions {
    /**
     * Seem to be a hint to the UI to indicate what text is "currently
     * being edited," which currently means this span will be visually
     * underlined. Useful if you're creating an interactive keyboard, I
     * guess
     */
    preEditIndex: number;
    preEditLength: number;
    /**
     * not sure what the practical use of this is. It seems
     * to initially indicate what part of the original text
     * was replaced, but the result of this packet is that
     * ALL the text is replaced by `string`, no matter what
     * values are passed in here. So... 0,0 is fine?
     */
    editIndex: number;
    editLength: number;
    /** where to put the caret within `string` */
    caretIndex: number;
}
export declare class OskChangeStringPacket extends OutgoingPacket {
    private readonly options;
    private static minLength;
    readonly type = PacketType.OskChangeString;
    readonly totalLength: number;
    private readonly textBuffer;
    constructor(text: string, options?: Partial<OskChangeStringOptions>);
    fillBuffer(builder: PacketBuilder): void;
}
