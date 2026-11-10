"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.PacketBuilder = void 0;
const intLength = 4;
const shortLength = 2;
class PacketBuilder {
    constructor(length) {
        this.offset = 0;
        this.buffer = Buffer.alloc(length, 0);
        this.writeInt(length);
    }
    write(data) {
        data.copy(this.buffer, this.offset);
        this.offset += data.byteLength;
        return this;
    }
    writePadded(data, length, encoding) {
        const toWrite = !length || data.length <= length
            ? data
            : data.substring(0, length);
        const written = encoding
            ? this.buffer.write(toWrite, this.offset, encoding)
            : this.buffer.write(toWrite, this.offset);
        this.offset += written;
        if (length) {
            // 1 byte at a time is inefficient, but simple
            const padding = length - written;
            for (let i = 0; i < padding; ++i) {
                this.buffer.writeUInt8(0, this.offset + i);
            }
            this.offset += padding;
        }
        return this;
    }
    writeShort(value) {
        this.buffer.writeInt16LE(value, this.offset);
        this.offset += shortLength;
        return this;
    }
    writeInt(value) {
        this.buffer.writeInt32LE(value, this.offset);
        this.offset += intLength;
        return this;
    }
    build() {
        return this.buffer;
    }
}
exports.PacketBuilder = PacketBuilder;
