"use strict";
var __awaiter = (this && this.__awaiter) || function (thisArg, _arguments, P, generator) {
    function adopt(value) { return value instanceof P ? value : new P(function (resolve) { resolve(value); }); }
    return new (P || (P = Promise))(function (resolve, reject) {
        function fulfilled(value) { try { step(generator.next(value)); } catch (e) { reject(e); } }
        function rejected(value) { try { step(generator["throw"](value)); } catch (e) { reject(e); } }
        function step(result) { result.done ? resolve(result.value) : adopt(result.value).then(fulfilled, rejected); }
        step((generator = generator.apply(thisArg, _arguments || [])).next());
    });
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.CancellableAsyncSink = exports.delayMillis = void 0;
const asyncsink_1 = require("ix/asynciterable/asyncsink");
function delayMillis(millis) {
    return new Promise(resolve => {
        setTimeout(resolve, millis);
    });
}
exports.delayMillis = delayMillis;
/**
 * Drop-in replacement for AsyncSink that also handles early cancellation,
 * calling the function installed in onCancel.
 */
class CancellableAsyncSink {
    constructor() {
        this.sink = new asyncsink_1.AsyncSink();
    }
    end(error) {
        if (error)
            this.sink.error(error);
        else
            this.sink.end();
    }
    next() {
        return this.sink.next();
    }
    return(value) {
        return __awaiter(this, void 0, void 0, function* () {
            const { onCancel } = this;
            if (onCancel)
                onCancel();
            return {
                value,
                done: true,
            };
        });
    }
    write(value) {
        this.sink.write(value);
    }
    [Symbol.asyncIterator]() {
        return this;
    }
}
exports.CancellableAsyncSink = CancellableAsyncSink;
