import { Command } from "clime";
import { DeviceOptions } from "../options";
declare class StartTitleOptions extends DeviceOptions {
    dontAutoQuit: boolean;
}
export default class extends Command {
    execute(titleId: string, deviceSpec: StartTitleOptions): Promise<void>;
}
export {};
