import type { languages } from 'monaco-editor';

/** Monaco language id of semantic expressions, registered by `./monaco`. */
export const EXPRESSION_LANGUAGE_ID = 'qe-expression';

/** Highlights `[f.field]` / `[m.measure]` references, numbers, strings and function names. */
export const expressionTokens: languages.IMonarchLanguage = {
  tokenizer: {
    root: [
      [/\[[fm]\.[^\]]*\]/, 'variable'],
      [/\d+(\.\d+)?/, 'number'],
      [/'([^'\\]|\\.)*'/, 'string'],
      [/[A-Za-z_]\w*(?=\s*\()/, 'keyword'],
    ],
  },
};
