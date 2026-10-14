import type { CSSProperties, ReactNode } from 'react';

export type JsonEditorViewMode = 'code' | 'tree';

/**
 * Tokens to customize CodeMirror styling per element.
 * Maps to CodeMirror DOM parts; see https://codemirror.net/examples/styling/
 */
export interface JsonEditorTokens {
  /** Styles for the outer `.cm-editor` element (background, border, etc.) */
  editor?: CSSProperties;
  /** Styles for `.cm-content` (document content area; e.g. padding, font) */
  gutters?: CSSProperties;
  /** Styles for `.cm-gutter` (single gutter column) */
  cursor?: CSSProperties;
  /** Styles for `.cm-activeLineGutter` (single gutter column) */
  activeLineGutter?: CSSProperties;
}

export interface JsonEditorProps {
  /** Label for the editor */
  label?: string;
  /** Optional info shown next to the label in an icon tooltip (plain text or rich content). */
  labelTooltip?: ReactNode;
  /** Extra classes for the label tooltip panel (merged with defaults). */
  labelTooltipClassName?: string;
  /** Current JSON string value */
  value: string;
  /** Called when the user edits the JSON (code mode) or when tree view is not editable, this may be read-only */
  onChange?: (value: string) => void;
  /** Whether the editor is editable (code mode only) */
  editable?: boolean;
  /** Additional CSS class */
  className?: string;
  /** Whether to show collapsible fold gutters in code view */
  foldGutter?: boolean;
  /** Whether to show a checkbox to toggle the code editor visibility */
  showToggle?: boolean;
  /** Whether to show the JSON view */
  showJson?: boolean;
  /** Whether to show the toolbar button that formats the JSON content */
  showFormatButton?: boolean;
  /** Whether to show the copy-to-clipboard control in the top-right of the editor */
  showCopyButton?: boolean;
  /** Called when the user toggles the code editor visibility */
  onToggle?: () => void;
  /** Fires when CodeMirror JSON diagnostics change (e.g. parse errors from `jsonParseLinter`). Use to disable submit/navigation while the document is invalid. */
  onValidationChange?: (isValid: boolean) => void;
  /** CSS variables to customize editor layout/theme (e.g. background, border, font) */
  tokens?: JsonEditorTokens;
}
