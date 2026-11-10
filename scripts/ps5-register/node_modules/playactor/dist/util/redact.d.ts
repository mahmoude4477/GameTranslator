/// <reference types="node" />
/**
 * Redact a value, unless we've explicitly requested that values be printed
 * unredacted. For maps/records, we will ONLY redact specific keys (but nested
 * records will also have redact() called on them); string values passed in
 * will always be completely redacted.  Arrays will have redact() called on
 * each value.
 *
 * Any collections passed to `redact` will be deep-copied, to avoid munging
 * data that has yet to be sent across the wire.
 */
export declare function redact(value: string): string;
export declare function redact(value: Buffer): string;
export declare function redact(value: Record<string, unknown>): Record<string, unknown>;
export declare function redact(value: object): object;
export declare function redact<T>(value: T[]): T[];
