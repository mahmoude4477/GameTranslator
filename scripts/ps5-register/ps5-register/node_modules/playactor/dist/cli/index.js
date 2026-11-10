#!/usr/bin/env node
"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
const clime_1 = require("clime");
const clime_2 = require("./clime");
const root_proxy_device_1 = require("./root-proxy-device");
// clime doesn't currently have a way to ignore or hide specific args,
// so let's do it manually here:
const args = root_proxy_device_1.RootProxyDevice.removeProxiedUserId([...process.argv]);
// Clime in its core provides an object-based command-line infrastructure.
// To have it work as a common CLI, a shim needs to be applied:
const shim = new clime_1.Shim(clime_2.cli);
shim.execute(args);
