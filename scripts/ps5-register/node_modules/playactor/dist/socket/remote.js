"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.InternalRemoteOperation = exports.RemoteOperation = void 0;
var RemoteOperation;
(function (RemoteOperation) {
    RemoteOperation[RemoteOperation["Up"] = 1] = "Up";
    RemoteOperation[RemoteOperation["Down"] = 2] = "Down";
    RemoteOperation[RemoteOperation["Right"] = 4] = "Right";
    RemoteOperation[RemoteOperation["Left"] = 8] = "Left";
    RemoteOperation[RemoteOperation["Enter"] = 16] = "Enter";
    RemoteOperation[RemoteOperation["Back"] = 32] = "Back";
    RemoteOperation[RemoteOperation["Option"] = 64] = "Option";
    RemoteOperation[RemoteOperation["PS"] = 128] = "PS";
    RemoteOperation[RemoteOperation["Cancel"] = 512] = "Cancel";
})(RemoteOperation = exports.RemoteOperation || (exports.RemoteOperation = {}));
var InternalRemoteOperation;
(function (InternalRemoteOperation) {
    InternalRemoteOperation[InternalRemoteOperation["KeyOff"] = 256] = "KeyOff";
    InternalRemoteOperation[InternalRemoteOperation["CloseRC"] = 2048] = "CloseRC";
    InternalRemoteOperation[InternalRemoteOperation["OpenRC"] = 1024] = "OpenRC";
})(InternalRemoteOperation = exports.InternalRemoteOperation || (exports.InternalRemoteOperation = {}));
