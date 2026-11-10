import { Command } from "clime";
import { KeyPress } from "../../socket/proc/remote-control";
import { DeviceOptions } from "../options";
/** Public for testing */
export declare function parseKeys(keys: string[]): KeyPress[];
export default class extends Command {
    execute(keys: string[], deviceSpec: DeviceOptions): Promise<void>;
}
