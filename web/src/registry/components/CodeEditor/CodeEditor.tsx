import { useEffect, useRef, useState } from 'react';
import { Editor, type Monaco } from '@monaco-editor/react';
import { Maximize, Play } from 'lucide-react';
import {
  type editor,
  type IDisposable,
  MarkerSeverity,
  type Position,
} from 'monaco-editor';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import { cn } from '@/lib/utils';
import { DEFAULT_EDITOR_OPTIONS, DEFAULT_HEIGHT, DEFAULT_LANGUAGE, MONACO_THEME_NAME } from './constants';
import type { CodeEditorProps, MonacoSurfaceProps } from './types';
import { applyMonacoTheme } from './utils';
import './CodeEditor.css';

function MonacoSurface({
  value,
  language = DEFAULT_LANGUAGE,
  height = DEFAULT_HEIGHT,
  onChange,
  autocompleteConfig,
  options,
  onMount,
  onExecute,
  executeButtonLabel = 'Run',
}: MonacoSurfaceProps) {
  const [errors, setErrors] = useState<editor.IMarker[]>([]);
  const [monacoInstance, setMonacoInstance] = useState<Monaco | null>(null);
  const completionDisposableRef = useRef<IDisposable | null>(null);

  useEffect(() => {
    completionDisposableRef.current?.dispose();
    completionDisposableRef.current = null;

    if (monacoInstance && autocompleteConfig) {
      try {
        completionDisposableRef.current = monacoInstance.languages.registerCompletionItemProvider(
          language,
          {
            triggerCharacters: autocompleteConfig.triggerCharacters,
            provideCompletionItems: (model: editor.ITextModel, position: Position) => {
              return autocompleteConfig.provideCompletionItems(model, position, monacoInstance);
            },
          }
        );
      } catch (error) {
        console.warn('Failed to register Monaco completion provider:', error);
      }
    }

    return () => {
      completionDisposableRef.current?.dispose();
      completionDisposableRef.current = null;
    };
  }, [autocompleteConfig, monacoInstance, language]);

  function handleEditorDidMount(editorInstance: editor.IStandaloneCodeEditor, monaco: Monaco) {
    setMonacoInstance(monaco);
    applyMonacoTheme(monaco);
    onMount?.(editorInstance, monaco);
  }

  function handleEditorChange(nextValue: string | undefined) {
    if (nextValue !== undefined) {
      onChange(nextValue);
    }
  }

  function handleValidate(markers: editor.IMarker[]) {
    setErrors(markers);
  }

  const hasErrors = errors.some(error => error.severity === MarkerSeverity.Error);

  return (
    <div
      data-slot="code-editor-surface"
      className={cn('border', hasErrors && 'border-destructive')}
    >
      <Editor
        theme={MONACO_THEME_NAME}
        height={height}
        value={value}
        defaultLanguage={language}
        onValidate={handleValidate}
        onChange={handleEditorChange}
        onMount={handleEditorDidMount}
        options={{
          ...DEFAULT_EDITOR_OPTIONS,
          quickSuggestions: autocompleteConfig
            ? {
                other: true,
                strings: true,
              }
            : false,
          suggestOnTriggerCharacters: Boolean(autocompleteConfig),
          ...options,
        }}
      />
      {errors.length > 0 && (
        <div data-slot="code-editor-errors" className="p-2 text-sm text-destructive">
          {errors.map((error, idx) => {
            if (error.severity === MarkerSeverity.Error) {
              return <div key={`${error.message}-${idx}`}>- {error.message}</div>;
            }

            return null;
          })}
        </div>
      )}
      {onExecute && (
        <div data-slot="code-editor-execute" className="border-t p-2">
          <Button type="button" variant="outline" size="sm" onClick={() => onExecute(value)}>
            <Play className="mr-1 h-4 w-4" />
            {executeButtonLabel}
          </Button>
        </div>
      )}
    </div>
  );
}

export function CodeEditor({
  value,
  onChange,
  label,
  labelClassName,
  showFullscreenButton = true,
  fullscreenTitle = 'Code Editor',
  onMount,
  ...codeEditorProps
}: CodeEditorProps) {
  const [isFullscreenOpen, setIsFullscreenOpen] = useState(false);

  return (
    <div data-slot="code-editor" className="space-y-2">
      {label && (
        <div data-slot="code-editor-toolbar" className="flex items-center justify-between">
          <Label
            className={cn('text-xs uppercase tracking-wide text-muted-foreground', labelClassName)}
          >
            {label}
          </Label>
          {showFullscreenButton && (
            <Button
              variant="outline"
              size="sm"
              onClick={() => setIsFullscreenOpen(true)}
              className="h-8 px-2"
            >
              <Maximize className="mr-1 h-4 w-4" />
              Fullscreen
            </Button>
          )}
        </div>
      )}

      <MonacoSurface value={value} onChange={onChange} onMount={onMount} {...codeEditorProps} />

      <Dialog open={isFullscreenOpen} onOpenChange={setIsFullscreenOpen}>
        <DialogContent className="h-[800px] max-h-[95vh] !w-[1200px] !max-w-[1200px] overflow-auto">
          <DialogHeader>
            <DialogTitle>{fullscreenTitle}</DialogTitle>
          </DialogHeader>
          <MonacoSurface
            value={value}
            onChange={onChange}
            height="calc(100vh - 200px)"
            onMount={onMount}
            {...codeEditorProps}
          />
        </DialogContent>
      </Dialog>
    </div>
  );
}
