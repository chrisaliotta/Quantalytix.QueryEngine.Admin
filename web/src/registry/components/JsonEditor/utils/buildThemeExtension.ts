import React from 'react';
import { EditorView } from '@codemirror/view';
import type { JsonEditorTokens } from '../types';

export function buildThemeExtension(tokens: JsonEditorTokens) {
  const themeSpec: Record<string, React.CSSProperties> = {};
  if (tokens.editor && Object.keys(tokens.editor).length > 0) themeSpec['&'] = tokens.editor;
  if (tokens.gutters && Object.keys(tokens.gutters).length > 0)
    themeSpec['.cm-gutters'] = tokens.gutters;
  if (tokens.cursor && Object.keys(tokens.cursor).length > 0)
    themeSpec['&.cm-focused .cm-cursor'] = tokens.cursor;
  if (tokens.activeLineGutter && Object.keys(tokens.activeLineGutter).length > 0)
    themeSpec['.cm-activeLineGutter'] = tokens.activeLineGutter;
  return Object.keys(themeSpec).length > 0 ? EditorView.theme(themeSpec as any) : null;
}
