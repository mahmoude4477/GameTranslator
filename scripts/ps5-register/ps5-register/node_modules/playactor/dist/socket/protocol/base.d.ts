/// <reference types="node" />
import { IBufferReader, IPacket, IPacketReader, PacketReadState } from "../model";
export interface IOptions {
    minPacketLength?: number;
    lengthIncludesHeader?: boolean;
    littleEndian?: boolean;
}
export declare class LengthDelimitedBufferReader implements IBufferReader {
    private readonly minPacketLength;
    private readonly lengthIncludesHeader;
    private readonly littleEndian;
    private currentBuffer?;
    private actualLength?;
    private expectedLength?;
    constructor({ minPacketLength, lengthIncludesHeader, littleEndian, }?: IOptions);
    read(data: Buffer, paddingSize?: number): PacketReadState;
    get(): Buffer;
    remainder(): Buffer | undefined;
    private get currentPacketLength();
    private get currentPacketExpectedLength();
}
export interface PacketConstructor {
    new (data: Buffer): IPacket;
}
/**
 * The [TypedPacketReader] delegates most of its functionality to an
 * [IBufferReader] (by default, [LengthDelimitedBufferReader]) and
 * creates packets given a map of type to constructor
 */
export declare abstract class TypedPacketReader implements IPacketReader {
    private readonly packets;
    private readonly base;
    constructor(packets: {
        [key: number]: PacketConstructor;
    }, base?: IBufferReader);
    protected abstract readType(buffer: Buffer): number;
    protected abstract createDefaultPacket(type: number, buffer: Buffer): IPacket;
    read(data: Buffer, paddingSize?: number): PacketReadState;
    get(): IPacket;
    remainder(): Buffer | undefined;
}
