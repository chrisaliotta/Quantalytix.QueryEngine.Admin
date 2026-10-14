import type { editor, languages, Position } from 'monaco-editor';
import type { ReactNode } from 'react';
import type { Monaco } from '@monaco-editor/react';

export interface AutocompleteConfig {
  triggerCharacters: string[];
  provideCompletionItems: (
    model: editor.ITextModel,
    position: Position,
    monaco: Monaco
  ) => languages.ProviderResult<languages.CompletionList>;
}

export interface MonacoSurfaceProps {
  value: string;
  onChange: (value: string) => void;
  height?: string;
  language?: string;
  autocompleteConfig?: AutocompleteConfig;
  options?: editor.IStandaloneEditorConstructionOptions;
  onMount?: (editor: editor.IStandaloneCodeEditor, monaco: Monaco) => void;
  /**
   * When set, adds an execute button. Clicking it calls `onExecute` with the
   * editor's current value - the caller decides how to run it (e.g. through
   * a sandboxed evaluator) and how to surface the result; CodeEditor itself
   * has no opinion on execution.
   */
  onExecute?: (value: string) => void;
  /** Label for the execute button. Defaults to "Run". */
  executeButtonLabel?: string;
}

export interface CodeEditorProps extends MonacoSurfaceProps {
  label?: string | ReactNode;
  labelClassName?: string;
  showFullscreenButton?: boolean;
  fullscreenTitle?: string;
}
