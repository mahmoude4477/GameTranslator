import { Command } from "clime";
import { DeviceOptions } from "../options";
export default class extends Command {
    execute(text: string | undefined, deviceSpec: DeviceOptions): Promise<void>;
}
