"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.formatDiscoveryMessage = void 0;
const model_1 = require("./discovery/model");
function formatDiscoveryMessage({ data, type, version, }) {
    let formatted = "";
    if (data) {
        for (const key of Object.keys(data)) {
            if (!model_1.outgoingDiscoveryKeys.has(key))
                continue;
            formatted += `${key}:${data[key]}\n`;
        }
    }
    return Buffer.from(`${type} * HTTP/1.1\n${formatted}device-discovery-protocol-version:${version}\n`);
}
exports.formatDiscoveryMessage = formatDiscoveryMessage;
