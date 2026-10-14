import type { editor } from 'monaco-editor';

export const DEFAULT_HEIGHT = '200px';
export const DEFAULT_LANGUAGE = 'typescript';
export const MONACO_THEME_NAME = 'qtx-theme';

export const DEFAULT_EDITOR_OPTIONS: editor.IStandaloneEditorConstructionOptions = {
  minimap: { enabled: false },
  scrollBeyondLastLine: false,
  lineNumbers: 'on',
  roundedSelection: false,
  scrollbar: {
    vertical: 'visible',
    horizontal: 'visible',
    alwaysConsumeMouseWheel: false,
  },
  tabSize: 2,
  automaticLayout: true,
  acceptSuggestionOnEnter: 'on',
  acceptSuggestionOnCommitCharacter: true,
};
