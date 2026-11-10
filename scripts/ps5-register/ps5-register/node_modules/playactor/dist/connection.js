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
Object.defineProperty(exports, "__esModule", { value: true });
exports.openConnection = exports.openSecondScreen = exports.openRemotePlay = void 0;
const remoteplay_1 = require("./connection/remoteplay");
const secondscreen_1 = require("./connection/secondscreen");
const model_1 = require("./credentials/model");
const session_1 = require("./remoteplay/session");
const open_1 = require("./socket/open");
function openRemotePlay(waker, device, discovered, config, creds) {
    return __awaiter(this, void 0, void 0, function* () {
        yield waker.wake(discovered);
        const session = yield session_1.openSession(discovered, config, creds);
        return new remoteplay_1.RemotePlayDeviceConnection(session);
    });
}
exports.openRemotePlay = openRemotePlay;
function openSecondScreen(waker, device, discovered, config, creds) {
    return __awaiter(this, void 0, void 0, function* () {
        const socket = yield open_1.openSocket(waker.networkFactory, discovered, creds, config.socket, config.network, config.login);
        return new secondscreen_1.SecondScreenDeviceConnection(device.resolve.bind(device), socket);
    });
}
exports.openSecondScreen = openSecondScreen;
function openConnection(waker, device, discovered, config, creds) {
    if (model_1.isRemotePlay(creds)) {
        return openRemotePlay(waker, device, discovered, config, creds);
    }
    return openSecondScreen(waker, device, discovered, config, creds);
}
exports.openConnection = openConnection;
