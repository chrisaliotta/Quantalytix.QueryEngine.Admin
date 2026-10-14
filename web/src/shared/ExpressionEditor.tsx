import type { Monaco } from '@monaco-editor/react';
import type { languages, Position } from 'monaco-editor';
import { useEffect, useMemo, useRef } from 'react';
import type { AutocompleteConfig } from '@registry/components/CodeEditor';
import { CodeEditor } from './CodeEditor';
import { getExpressionCompletions, type ExpressionCompletion } from './expressionCompletion';
import { EXPRESSION_LANGUAGE_ID } from './expressionLanguage';

export interface ExpressionEditorProps {
  value: string;
  onChange: (value: string) => void;
  /** Field keys completed after `[f.`. */
  fieldKeys?: string[];
  /** Measure keys completed after `[m.`. */
  measureKeys?: string[];
  readOnly?: boolean;
  /** CSS height of the editor. */
  height?: string;
}

/**
 * Monaco editor (registry CodeEditor) for semantic expressions: typing `[` offers `[f.` and `[m.`,
 * then the field or measure keys, completed with the closing `]`.
 */
export function ExpressionEditor({
  value,
  onChange,
  fieldKeys = [],
  measureKeys = [],
  readOnly = false,
  height = '120px',
}: ExpressionEditorProps) {
  const keys = useRef({ fieldKeys, measureKeys });
  const modelUri = useRef<string | undefined>(undefined);

  useEffect(() => {
    keys.current = { fieldKeys, measureKeys };
  }, [fieldKeys, measureKeys]);

  const autocompleteConfig = useMemo<AutocompleteConfig>(
    () => ({
      triggerCharacters: ['[', '.'],
      provideCompletionItems: (model, position, monaco) => {
        // Completion providers are global per language, so answer only for this editor's model.
        if (model.uri.toString() !== modelUri.current) {
          return { suggestions: [] };
        }
        const line = model.getLineContent(position.lineNumber);
        const completions = getExpressionCompletions(
          line.slice(0, position.column - 1),
          keys.current.fieldKeys,
          keys.current.measureKeys
        );
        const closingBracketFollows = line[position.column - 1] === ']';
        return {
          suggestions: completions.map(completion =>
            toSuggestion(monaco, completion, position, closingBracketFollows)
          ),
        };
      },
    }),
    []
  );

  return (
    <CodeEditor
      value={value}
      onChange={onChange}
      language={EXPRESSION_LANGUAGE_ID}
      height={height}
      autocompleteConfig={autocompleteConfig}
      onMount={editor => {
        modelUri.current = editor.getModel()?.uri.toString();
      }}
      options={{
        readOnly,
        lineNumbers: 'off',
        wordWrap: 'on',
        folding: false,
        glyphMargin: false,
        lineDecorationsWidth: 8,
        renderLineHighlight: 'none',
        wordBasedSuggestions: 'off',
        fixedOverflowWidgets: true,
        scrollbar: { vertical: 'auto', horizontal: 'hidden', alwaysConsumeMouseWheel: false },
      }}
    />
  );
}

function toSuggestion(
  monaco: Monaco,
  completion: ExpressionCompletion,
  position: Position,
  closingBracketFollows: boolean
): languages.CompletionItem {
  const isReference = completion.kind === 'reference';
  const replacesClosingBracket = closingBracketFollows && !isReference;
  return {
    label: completion.label,
    kind: toCompletionItemKind(monaco, completion.kind),
    insertText: completion.insertText,
    range: {
      startLineNumber: position.lineNumber,
      endLineNumber: position.lineNumber,
      startColumn: position.column - completion.replaceLength,
      endColumn: replacesClosingBracket ? position.column + 1 : position.column,
    },
    command: isReference ? { id: 'editor.action.triggerSuggest', title: 'Suggest keys' } : undefined,
  };
}

function toCompletionItemKind(monaco: Monaco, kind: ExpressionCompletion['kind']) {
  if (kind === 'field') {
    return monaco.languages.CompletionItemKind.Field;
  }
  if (kind === 'measure') {
    return monaco.languages.CompletionItemKind.Variable;
  }
  return monaco.languages.CompletionItemKind.Module;
}
