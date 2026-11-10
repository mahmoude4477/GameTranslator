export declare function delayMillis(millis: number): Promise<void>;
/**
 * Drop-in replacement for AsyncSink that also handles early cancellation,
 * calling the function installed in onCancel.
 */
export declare class CancellableAsyncSink<T> implements AsyncIterator<T>, AsyncIterable<T> {
    private readonly sink;
    onCancel?: () => void;
    end(error?: Error): void;
    next(): Promise<IteratorResult<T, any>>;
    return(value: T): Promise<{
        value: T;
        done: boolean;
    }>;
    write(value: T): void;
    [Symbol.asyncIterator](): this;
}
