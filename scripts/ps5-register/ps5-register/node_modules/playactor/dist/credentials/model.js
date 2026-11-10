"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.isRemotePlay = void 0;
function isRemotePlay(credentials) {
    return credentials["auth-type"] === "R";
}
exports.isRemotePlay = isRemotePlay;
