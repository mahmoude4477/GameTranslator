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
exports.PendingDevice = void 0;
const credentials_1 = require("../credentials");
const discovery_1 = require("../discovery");
const standard_1 = require("../discovery/standard");
const waker_1 = require("../waker");
const udp_1 = require("../waker/udp");
const resolved_1 = require("./resolved");
class PendingDevice {
    constructor(description, predicate, networkConfig = {}, discoveryConfig = {}, discoveryFactory = standard_1.StandardDiscoveryNetworkFactory, credentials = new credentials_1.CredentialManager()) {
        this.description = description;
        this.predicate = predicate;
        this.networkConfig = networkConfig;
        this.discoveryConfig = discoveryConfig;
        this.discoveryFactory = discoveryFactory;
        this.credentials = credentials;
    }
    discover(config) {
        return __awaiter(this, void 0, void 0, function* () {
            const delegate = yield this.resolve(config);
            return delegate.discover(config);
        });
    }
    wake() {
        return __awaiter(this, void 0, void 0, function* () {
            const delegate = yield this.resolve();
            return delegate.wake();
        });
    }
    openConnection(config = {}) {
        return __awaiter(this, void 0, void 0, function* () {
            const delegate = yield this.resolve();
            return delegate.openConnection(config);
        });
    }
    resolve(config) {
        var e_1, _a;
        return __awaiter(this, void 0, void 0, function* () {
            const existing = this.delegate;
            if (existing)
                return existing;
            const discovery = new discovery_1.Discovery(this.discoveryConfig, this.discoveryFactory);
            try {
                for (var _b = __asyncValues(discovery.discover(config !== null && config !== void 0 ? config : this.networkConfig)), _c; _c = yield _b.next(), !_c.done;) {
                    const device = _c.value;
                    if (this.predicate(device)) {
                        const newDelegate = new resolved_1.ResolvedDevice(new waker_1.SimpleWakerFactory({
                            credentials: this.credentials,
                            discoveryConfig: this.discoveryConfig,
                            networkFactory: new udp_1.UdpWakerNetworkFactory(),
                            discoveryFactory: standard_1.StandardDiscoveryNetworkFactory,
                        }), device, this.networkConfig, this.discoveryConfig, this.discoveryFactory);
                        this.delegate = newDelegate;
                        return newDelegate;
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
            throw new Error(`Unable to locate device: ${this.description}`);
        });
    }
}
exports.PendingDevice = PendingDevice;
