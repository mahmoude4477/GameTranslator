"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.DeviceTypeStrategyCredentialRequester = void 0;
const model_1 = require("../discovery/model");
class DeviceTypeStrategyCredentialRequester {
    constructor(strategies) {
        this.strategies = strategies;
    }
    requestForDevice(device) {
        const strategy = this.strategies[device.type];
        return strategy.requestForDevice(device);
    }
}
exports.DeviceTypeStrategyCredentialRequester = DeviceTypeStrategyCredentialRequester;
