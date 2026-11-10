/// <reference types="node" />
import { DiscoveryVersion } from "./discovery/model";
export declare function formatDiscoveryMessage({ data, type, version, }: {
    data?: Record<string, unknown>;
    type: string;
    version: DiscoveryVersion;
}): Buffer;
