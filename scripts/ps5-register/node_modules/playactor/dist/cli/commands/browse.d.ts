import { Command } from "clime";
import { DiscoveryOptions } from "../options";
export default class extends Command {
    execute(options: DiscoveryOptions): Promise<void>;
}
