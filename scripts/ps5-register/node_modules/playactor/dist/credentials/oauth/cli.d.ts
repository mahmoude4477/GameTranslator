import { IInputOutput } from "../../cli/io";
import { OauthStrategy } from "./model";
export declare class CliOauthStrategy implements OauthStrategy {
    private io;
    private autoOpenUrls;
    constructor(io: IInputOutput, autoOpenUrls?: boolean);
    performLogin(url: string): Promise<string>;
    private printLoginInstructions;
    private openSafely;
}
