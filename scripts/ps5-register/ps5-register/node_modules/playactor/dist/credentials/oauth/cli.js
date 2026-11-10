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
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.CliOauthStrategy = void 0;
const open_1 = __importDefault(require("open"));
class CliOauthStrategy {
    constructor(io, autoOpenUrls = true) {
        this.io = io;
        this.autoOpenUrls = autoOpenUrls;
    }
    performLogin(url) {
        return __awaiter(this, void 0, void 0, function* () {
            if (this.autoOpenUrls) {
                this.io.logInfo("In a moment, we will attempt to open a browser window for you to login to your PSN account.");
                this.printLoginInstructions();
                yield this.io.prompt("Hit ENTER to continue");
                try {
                    yield this.openSafely(url);
                }
                catch (e) {
                    this.io.logInfo("Unable to open the browser for you. This is fine; please manually open the following URL:");
                    this.io.logInfo(`  ${url}`);
                }
            }
            else {
                this.io.logInfo("Open the following URL in a web browser to login to your PSN account.");
                this.printLoginInstructions();
                this.io.logInfo(`  ${url}`);
            }
            this.io.logInfo("These URLs may contain sensitive information; if sharing debug logs anywhere, PLEASE make sure to redact them first!");
            return this.io.prompt("URL> ");
        });
    }
    printLoginInstructions() {
        this.io.logInfo("When the page shows \"redirect\", copy the URL from your browser's address bar and paste it here.");
    }
    openSafely(url) {
        return __awaiter(this, void 0, void 0, function* () {
            const proc = yield open_1.default(url);
            return new Promise((resolve, reject) => {
                proc.on("error", reject);
                proc.on("exit", resolve);
            });
        });
    }
}
exports.CliOauthStrategy = CliOauthStrategy;
