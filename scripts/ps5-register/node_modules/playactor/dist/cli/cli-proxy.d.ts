/// <reference types="node" />
import { Printable } from "clime";
/**
 * An ICliProxy can execute some CLI program passed to it with
 * arguments.  Declared as an interface here mostly so that it can be
 * stubbed in tests
 */
export interface ICliProxy {
    invoke(invocation: string[]): Promise<void>;
}
/**
 * Represents an error encountered by the "proxied" subprocess.  When
 * "printed" by the CLI framework, it will print the error message to
 * stderr and exit this process with the same exit code with which the
 * subprocess exited.
 */
export declare class CliProxyError extends Error implements Printable {
    readonly errorCode: number;
    constructor(message: string, errorCode: number);
    print(stdout: NodeJS.WritableStream, stderr: NodeJS.WritableStream): void;
}
/**
 * An implementation of ICliProxy that prefixes the provided invocation
 * with `sudo` (or whatever other program you prefer)
 */
export declare class SudoCliProxy {
    private readonly sudo;
    constructor(sudo?: string);
    invoke(invocation: string[]): Promise<void>;
}
