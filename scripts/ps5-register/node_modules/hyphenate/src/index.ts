const breakerRegex = /[A-Z]+(?=[A-Z][a-z]|$)|[A-Z]|(\d+)|([a-z])/g;

export interface Options {
  connector?: string;
  lowerCase?: boolean;
}

export function hyphenate(
  original: string,
  {connector = '-', lowerCase = false}: Options = {},
): string {
  let precededByNumber = false;

  return original
    .split(/[^a-z\d]+/i)
    .map(
      part =>
        part &&
        part.replace(
          breakerRegex,
          (text: string, num: string, lowerChar: string, index: number) => {
            if (lowerCase) {
              text = text.toLowerCase();
            }

            if (index) {
              if (precededByNumber) {
                precededByNumber = !!num;
                return connector + text;
              }

              precededByNumber = !!num;

              if (lowerChar) {
                return text;
              } else {
                return connector + text;
              }
            } else {
              return text;
            }
          },
        ),
    )
    .filter(part => !!part)
    .join(connector);
}

export default hyphenate;
