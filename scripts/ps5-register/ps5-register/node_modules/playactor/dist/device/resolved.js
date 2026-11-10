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
var __asyncValues = (this && this.__asyncValues) || function (o) {
    if (!Symbol.asyncIterator) throw new TypeError("Symbol.asyncIterator is not defined.");
    var m = o[Symbol.asyncIterator], i;
    return m ? m.call(o) : (o = typeof __values === "function" ? __values(o) : o[Symbol.iterator](), i = {}, verb("next"), verb("throw"), verb("return"), i[Symbol.asyncIterator] = function () { return this; }, i);
    function verb(n) { i[n] = o[n] && function (v) { return new Promise(function (resolve, reject) { v = o[n](v), settle(resolve, reject, v.done, v.value); }); }; }
    function settle(resolve, reject, d, v) { Promise.resolve(v).then(function(v) { resolve({ value: v, done: d }); }, reject); }
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.ResolvedDevice = void 0;
const connection_1 = require("../connection");
const discovery_1 = require("../discovery");
const model_1 = require("./model");
class ResolvedDevice {
    constructor(wakerFactory, description, networkConfig, discoveryConfig, discoveryFactory) {
        this.wakerFactory = wakerFactory;
        this.description = description;
        this.networkConfig = networkConfig;
        this.discoveryConfig = discoveryConfig;
        this.discoveryFactory = discoveryFactory;
    }
    discover() {
        return __awaiter(this, void 0, void 0, function* () {
            return this.description;
        });
    }
    wake() {
        return __awaiter(this, void 0, void 0, function* () {
            yield this.startWaker();
        });
    }
    /**
     * discover() returns the original IDiscoveredDevice, but if
     * you need to get an updated description for any reason, this
     * method will do the job.
     */
    resolve(config) {
        var e_1, _a;
        return __awaiter(this, void 0, void 0, function* () {
            const discovery = new discovery_1.Discovery(this.discoveryConfig, this.discoveryFactory);
            try {
                for (var _b = __asyncValues(discovery.discover(config !== null && config !== void 0 ? config : this.networkConfig)), _c; _c = yield _b.next(), !_c.done;) {
                    const device = _c.value;
                    if (device.id === this.description.id) {
                        return device;
                    }
                }
            }
            catch (e_1_1) { e_1 = { error: e_1_1 }; }
            finally {
                try {
                    if (_c && !_c.done && (_a = _b.return)) yield _a.call(_b);
                }
                finally { if (e_1) throw e_1.error; }
            }
            throw new Error(`Could not resolve ${this.description.name}`);
        });
    }
    openConnection(config = {}) {
        return __awaiter(this, void 0, void 0, function* () {
            const waker = yield this.startWaker();
            const creds = yield waker.credentials.getForDevice(this.description);
            return connection_1.openConnection(waker, this, this.description, config, creds);
        });
    }
    isSupported(capability) {
        switch (capability) {
            case model_1.DeviceCapability.WAKE:
                // all devices support wake so far
                return true;
            default:
                return false;
        }
    }
    startWaker() {
        return __awaiter(this, void 0, void 0, function* () {
            const waker = this.wakerFactory.create();
            yield waker.wake(this.description);
            return waker;
        });
    }
}
exports.ResolvedDevice = ResolvedDevice;
