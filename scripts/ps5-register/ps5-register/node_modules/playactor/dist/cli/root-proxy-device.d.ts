import { Printable } from "clime";
import { IConnectionConfig, IDevice } from "../device/model";
import { INetworkConfig } from "../discovery/model";
import { ICliProxy } from "./cli-proxy";
import { IInputOutput } from "./io";
export declare class RootProxiedError extends Error implements Printable {
    print(): void;
}
export interface IRootProxyConfig {
    providedCredentialsPath?: string;
    effectiveCredentialsPath: string;
    invocationArgs: string[];
    /** NOTE: Can be undefined on eg Windows, which doesn't support getuid */
    currentUserId?: number;
}
/**
 * The RootProxyDevice wraps another IDevice implementation and delegates
 * entirely to it. If `wake` or `openConnection` reject with a
 * RootMissingError, the error will be suppressed and, if possible, the
 * CLI invocation will be "proxied" into a new subprocess that will
 * request root and this process will be gracefully stopped with the same
 * exit code as the "proxied" subprocess.
 *
 * This class is meant exclusively for use with the CLI; API clients
 * should almost certainly not use this.
 */
export declare class RootProxyDevice implements IDevice {
    private readonly io;
    private readonly cliProxy;
    private readonly delegate;
    private readonly config;
    private readonly resolvePath;
    static extractProxiedUserId(args: string[]): number | undefined;
    static removeProxiedUserId(args: string[]): string[];
    constructor(io: IInputOutput, cliProxy: ICliProxy, delegate: IDevice, config: IRootProxyConfig, resolvePath?: (p: string) => string);
    discover(config?: INetworkConfig): Promise<import("../discovery/model").IDiscoveredDevice>;
    wake(): Promise<void>;
    openConnection(config?: IConnectionConfig): Promise<import("../connection/model").IDeviceConnection>;
    private tryResolveError;
    private proxyCliInvocation;
}
