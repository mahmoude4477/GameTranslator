import { Command } from "clime";
import { InputOutputOptions } from "../options";
export declare function getAppVersion(): any;
export default class extends Command {
    execute(io: InputOutputOptions): Promise<void>;
}
