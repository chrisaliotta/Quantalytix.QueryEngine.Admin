import type { Monaco } from '@monaco-editor/react';
import { MONACO_THEME_NAME } from '../constants';
import { getCssVarValue } from './getCssVarValue';
import { isDarkBackground } from './isDarkBackground';
import { normalizeColorForMonaco } from './normalizeColorForMonaco';

export function applyMonacoTheme(monaco: Monaco) {
  if (typeof window === 'undefined') return;

  const backgroundRaw = getCssVarValue('--background', { fallback: '#1e1e1e' });
  const foregroundRaw = getCssVarValue('--foreground', { fallback: '#d4d4d4' });
  const primaryRaw = getCssVarValue('--primary', { fallback: foregroundRaw });
  const mutedForegroundRaw = getCssVarValue('--muted-foreground', { fallback: '#808080' });

  const background = normalizeColorForMonaco(backgroundRaw);
  const foreground = normalizeColorForMonaco(foregroundRaw);
  const primary = normalizeColorForMonaco(primaryRaw);
  const mutedForeground = normalizeColorForMonaco(mutedForegroundRaw);

  const isDark = isDarkBackground(background);

  try {
    monaco.editor.defineTheme(MONACO_THEME_NAME, {
      base: isDark ? 'vs-dark' : 'vs',
      inherit: true,
      rules: [],
      colors: {
        'editor.background': background,
        'editor.foreground': foreground,
        'editorCursor.foreground': primary,
        'editorLineNumber.foreground': mutedForeground,
        'editorLineNumber.activeForeground': foreground,
        'editor.selectionBackground': primary,
        'editor.inactiveSelectionBackground': mutedForeground,
      },
    });

    monaco.editor.setTheme(MONACO_THEME_NAME);
  } catch (error) {
    console.warn('Failed to apply Monaco theme from CSS variables:', error);
  }
}
