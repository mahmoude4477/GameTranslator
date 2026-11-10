/// <reference types="node" />
export declare class PacketBuilder {
    private buffer;
    private offset;
    constructor(length: number);
    write(data: Buffer): this;
    writePadded(data: string, length?: number, encoding?: "utf8"): this;
    writeShort(value: number): this;
    writeInt(value: number): this;
    build(): Buffer;
}
