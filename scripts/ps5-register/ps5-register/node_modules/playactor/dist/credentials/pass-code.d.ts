export declare enum PassCodeKey {
    Left = 1,
    Up = 2,
    Right = 3,
    Down = 4,
    R1 = 5,
    R2 = 6,
    L1 = 7,
    L2 = 8,
    Triangle = 9,
    Square = 0
}
/**
 * Given a sequence of passcode keys, convert them into the
 * actual passcode numerical string value for use in ILoginConfig
 */
export declare function parsePassCodeKeys(...keys: PassCodeKey[]): string;
/**
 * Given a raw value, which may either be a string of PassCodeKey names
 * or a literal passcode string, return the validated passcode string
 * value, for use in ILoginConfig.
 */
export declare function parsePassCodeString(input: string): string;
