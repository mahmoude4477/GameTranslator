import { IDeviceSocket } from "./socket/model";
import { OskActionType, OskFlags, OskInputType } from "./socket/osk";
/**
 * Represents an active on-screen keyboard control session
 */
export declare class OnScreenKeyboard {
    private readonly socket;
    readonly maxLength: number;
    readonly initialContent: string;
    readonly actionType: OskActionType;
    readonly inputType: OskInputType;
    readonly flags: OskFlags;
    private isValid;
    constructor(socket: IDeviceSocket, maxLength: number, initialContent: string, actionType?: OskActionType, inputType?: OskInputType, flags?: OskFlags);
    get isActive(): boolean;
    hasFlag(flag: OskFlags): boolean;
    /**
     * Close the keyboard. This instance will become unusable, isActive
     * will return false, and all other method calls on this instance
     * will fail
     */
    close(): Promise<void>;
    /**
     * Set the current OSK text, optionally choosing a specific
     *  position for the caret.
     */
    setText(text: string, caretIndex?: number): Promise<void>;
    /**
     * "Submit" the text currently in the keyboard, like pressing the
     * "return" key. This also has the effect of `close()`.
     */
    submit(): Promise<void>;
    private ensureValid;
}
