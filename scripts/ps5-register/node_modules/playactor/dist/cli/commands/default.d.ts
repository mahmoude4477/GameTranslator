import { Command, Options } from "clime";
export declare class DefaultOptions extends Options {
    showVersion: boolean;
}
export default class extends Command {
    execute(options: DefaultOptions): Promise<void>;
}
