import { Command } from "clime";
import { DeviceOptions } from "../options";
export default class extends Command {
    execute(options: DeviceOptions): Promise<void>;
}
