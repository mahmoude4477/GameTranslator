"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.StandardDiscoveryNetworkFactory = exports.StandardPS5DiscoveryNetworkFactory = exports.StandardPS4DiscoveryNetworkFactory = void 0;
const udp_1 = require("../waker/udp");
const composite_1 = require("./composite");
const model_1 = require("./model");
const udp_2 = require("./udp");
exports.StandardPS4DiscoveryNetworkFactory = new udp_2.UdpDiscoveryNetworkFactory(udp_1.wakePortsByType[model_1.DeviceType.PS4], model_1.DiscoveryVersions.PS4);
exports.StandardPS5DiscoveryNetworkFactory = new udp_2.UdpDiscoveryNetworkFactory(udp_1.wakePortsByType[model_1.DeviceType.PS5], model_1.DiscoveryVersions.PS5);
const standardFactories = [
    exports.StandardPS4DiscoveryNetworkFactory,
    exports.StandardPS5DiscoveryNetworkFactory,
];
exports.StandardDiscoveryNetworkFactory = {
    createDevices(config, onDevice) {
        return new composite_1.CompositeDiscoveryNetwork(standardFactories.map(factory => factory.createDevices(config, onDevice)));
    },
    createMessages(config, onMessage) {
        return new composite_1.CompositeDiscoveryNetwork(standardFactories.map(factory => factory.createMessages(config, onMessage)));
    },
    createRawMessages(config, onMessage) {
        return new composite_1.CompositeDiscoveryNetwork(standardFactories.map(factory => factory.createRawMessages(config, onMessage)));
    },
};
