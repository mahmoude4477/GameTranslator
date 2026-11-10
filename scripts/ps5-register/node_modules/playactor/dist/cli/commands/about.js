"use strict";
var __decorate = (this && this.__decorate) || function (decorators, target, key, desc) {
    var c = arguments.length, r = c < 3 ? target : desc === null ? desc = Object.getOwnPropertyDescriptor(target, key) : desc, d;
    if (typeof Reflect === "object" && typeof Reflect.decorate === "function") r = Reflect.decorate(decorators, target, key, desc);
    else for (var i = decorators.length - 1; i >= 0; i--) if (d = decorators[i]) r = (c < 3 ? d(r) : c > 3 ? d(target, key, r) : d(target, key)) || r;
    return c > 3 && r && Object.defineProperty(target, key, r), r;
};
var __metadata = (this && this.__metadata) || function (k, v) {
    if (typeof Reflect === "object" && typeof Reflect.metadata === "function") return Reflect.metadata(k, v);
};
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
exports.getAppVersion = void 0;
const chalk_1 = __importDefault(require("chalk"));
const clime_1 = require("clime");
const got_1 = __importDefault(require("got"));
const options_1 = require("../options");
function getAppVersion() {
    // eslint-disable-next-line global-require, @typescript-eslint/no-var-requires
    const { version } = require("../../../package.json");
    return version;
}
exports.getAppVersion = getAppVersion;
const API_BASE = "https://api.github.com";
const REPO_BASE = `${API_BASE}/repos/dhleong/playactor`;
const RELEASE_API_ENDPOINT = `${REPO_BASE}/releases/latest`;
class MachineUI {
    upToDate(io, current, release) {
        io.logResult({ current, latest: release.name, isLatest: true });
    }
    outdated(io, current, release) {
        io.logResult({ current, latest: release.name, isLatest: false });
    }
    unknown(io, current) {
        io.logResult({ current });
    }
}
class RichUI {
    upToDate(io, current) {
        io.logResult(chalk_1.default `playactor v{underline ${current}} ({bold.greenBright latest})`);
    }
    outdated(io, current, release) {
        io.logResult(chalk_1.default `playactor v{underline ${current}} ({bold.redBright outdated})`);
        io.logResult(chalk_1.default `Latest version: v{greenBright ${release.name}}:`);
        io.logResult(chalk_1.default `To update, run: {underline npm i -g playactor}`);
        io.logResult("");
        io.logResult(chalk_1.default `{bold Notes From Latest Release}:`);
        io.logResult(release.body);
    }
    unknown(io, current, e) {
        io.logResult(chalk_1.default `
            playactor v${current} ({magenta failed to check for updates})
        `.trim());
        if (io.enableDebug) {
            io.logError(e);
        }
        else {
            io.logError(e.message);
        }
    }
}
let default_1 = class default_1 extends clime_1.Command {
    execute(io) {
        return __awaiter(this, void 0, void 0, function* () {
            const current = getAppVersion();
            const ui = io.machineFriendly ? new MachineUI() : new RichUI();
            try {
                const data = yield got_1.default(RELEASE_API_ENDPOINT).json();
                const isLatest = data.name === current;
                if (isLatest) {
                    ui.upToDate(io, current, data);
                }
                else {
                    ui.outdated(io, current, data);
                }
            }
            catch (e) {
                ui.unknown(io, current, e);
            }
        });
    }
};
__decorate([
    clime_1.metadata,
    __metadata("design:type", Function),
    __metadata("design:paramtypes", [options_1.InputOutputOptions]),
    __metadata("design:returntype", Promise)
], default_1.prototype, "execute", null);
default_1 = __decorate([
    clime_1.command({
        brief: "Check for playactor updates, etc.",
    })
], default_1);
exports.default = default_1;
