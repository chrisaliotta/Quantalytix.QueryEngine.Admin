import type { JsonEditorTokens } from './types';

export const DEFAULT_TOKENS: JsonEditorTokens = {
  editor: {
    color: 'var(--foreground)',
    backgroundColor: 'var(--background)',
    fontFamily: 'var(--font-mono)',
  },
  cursor: {
    borderLeftColor: 'var(--foreground)',
  },
  activeLineGutter: {
    backgroundColor: 'var(--muted-foreground)',
  },
  gutters: {
    backgroundColor: 'var(--background)',
    color: 'var(--foreground)',
    border: 'solid var(--border)',
  },
};
