"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.Device = void 0;
const pending_1 = require("./device/pending");
/**
 * IDevice factories
 */
exports.Device = {
    /**
     * Return a Device that talks to the first one found on the network
     */
    any(config = {}) {
        return new pending_1.PendingDevice("any", () => true, config, config);
    },
    /**
     * Create a Device that talks to the device with the given address
     */
    withAddress(address, config = {}) {
        return new pending_1.PendingDevice(`with address ${address}`, device => device.address.address === address, config, config);
    },
    /**
     * Create a Device that talks to the first device on the network
     * with the given ID
     */
    withId(id, config = {}) {
        return new pending_1.PendingDevice(`with id ${id}`, device => device.id === id, config, config);
    },
};
