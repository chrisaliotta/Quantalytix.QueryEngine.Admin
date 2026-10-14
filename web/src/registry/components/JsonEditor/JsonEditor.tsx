import { useEffect, useMemo, useRef, useState } from 'react';
import { json, jsonParseLinter } from '@codemirror/lang-json';
import { diagnosticCount, linter } from '@codemirror/lint';
import { EditorView } from '@codemirror/view';
import { IconTooltip } from '@registry/components/IconTooltip';
import CodeMirror from '@uiw/react-codemirror';
import { Copy, CopyCheck } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Label } from '@/components/ui/label';
import { cn } from '@/lib/utils';
import { DEFAULT_TOKENS } from './constants';
import type { JsonEditorProps } from './types';
import { buildThemeExtension, formatJson } from './utils';

export function JsonEditor({
  value,
  onChange,
  label = 'JSON Editor',
  labelTooltip,
  labelTooltipClassName,
  editable = true,
  className = '',
  foldGutter = true,
  showToggle = false,
  showJson = true,
  showFormatButton = true,
  showCopyButton = true,
  onToggle,
  onValidationChange,
  tokens = DEFAULT_TOKENS,
  ...rest
}: JsonEditorProps) {
  const [showCode, setShowCode] = useState(showJson);
  const [copied, setCopied] = useState(false);
  const copyResetRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    return () => {
      if (copyResetRef.current) clearTimeout(copyResetRef.current);
    };
  }, []);

  // Recreate listener when `onValidationChange` identity changes. Prefer a stable handler
  // (e.g. `setX` from useState or a Zustand setter); inline lambdas here will recreate extensions.
  const validationExtension = useMemo(() => {
    if (typeof onValidationChange !== 'function') {
      return [];
    }
    let lastLintValid = true;
    return [
      EditorView.updateListener.of(update => {
        const valid = diagnosticCount(update.state) === 0;
        if (valid === lastLintValid) return;
        lastLintValid = valid;
        onValidationChange(valid);
      }),
    ];
  }, [onValidationChange]);

  const themeExtension = buildThemeExtension(tokens);
  const baseExtensions = [
    json(),
    linter(jsonParseLinter() as any),
    ...validationExtension,
    ...(themeExtension ? [themeExtension] : []),
  ];

  const handleFormat = () => {
    const formatted = formatJson(value);
    if (formatted !== value) {
      onChange?.(formatted);
    }
  };

  const handleCopy = async () => {
    let success = false;
    try {
      await navigator.clipboard.writeText(value);
      success = true;
    } catch {
      // Fallback for browsers that don't support clipboard.writeText
      const ta = document.createElement('textarea');
      ta.value = value;
      ta.setAttribute('readonly', '');
      ta.style.position = 'fixed';
      ta.style.left = '-9999px';
      document.body.appendChild(ta);
      ta.select();
      success = document.execCommand('copy');
      document.body.removeChild(ta);
    }
    if (!success) return;
    if (copyResetRef.current) clearTimeout(copyResetRef.current);
    setCopied(true);
    copyResetRef.current = setTimeout(() => {
      setCopied(false);
      copyResetRef.current = null;
    }, 2000);
  };

  return (
    <div data-slot="json-editor" className="flex flex-col gap-2 h-full">
      <div data-slot="json-editor-toolbar" className="flex items-center justify-between">
        <div className="flex items-center w-full justify-between gap-3">
          <div className="flex items-center gap-2 min-h-8">
            {showToggle && (
              <Checkbox
                id="showJsonCheckbox"
                checked={showCode}
                onCheckedChange={checked => {
                  setShowCode(Boolean(checked));
                  onToggle?.();
                }}
              />
            )}
            {label && (
              <div className="flex min-w-0 items-center gap-1">
                <Label
                  htmlFor="showJsonCheckbox"
                  className={cn('text-sm font-medium', showToggle && 'cursor-pointer')}
                >
                  {label}
                </Label>
                {labelTooltip != null && labelTooltip !== '' && (
                  <IconTooltip
                    description={labelTooltip}
                    className="shrink-0"
                    tooltipClassName={labelTooltipClassName}
                  />
                )}
              </div>
            )}
          </div>
          {showFormatButton && showCode && (
            <Button type="button" variant="outline" className="h-8 text-sm" onClick={handleFormat}>
              Format JSON
            </Button>
          )}
        </div>
      </div>

      {(!showToggle || showCode) && (
        <div
          data-slot="json-editor-code-mirror"
          className={cn(
            'relative h-full flex-[1_1_0] overflow-hidden rounded-md border border-input bg-transparent',
            'hover:[&_.cm-content]:!bg-[color-mix(in_oklab,var(--accent)80%,transparent)]',
            'focus-within:border-ring focus-within:ring-[3px] focus-within:ring-ring/50',
            '[&_.cm-editor]:h-full [&_.cm-editor]:bg-transparent',
            '[&_.cm-editor_.cm-scroller]:h-full [&_.cm-editor_.cm-scroller]:overflow-auto',
            '[&_.cm-gutters]:border-border! [&_.cm-gutters]:bg-transparent!',
            '[&_.cm-editor_.cm-scroller_.cm-content]:bg-transparent'
          )}
        >
          {showCopyButton && (
            <Button
              type="button"
              variant="ghost"
              size="icon-sm"
              className={
                'absolute top-1.5 right-1.5 z-10 text-muted-foreground hover:text-foreground'
              }
              onClick={() => void handleCopy()}
              aria-label={copied ? 'Copied' : 'Copy JSON to clipboard'}
              title={copied ? 'Copied' : 'Copy to clipboard'}
            >
              {copied ? (
                <CopyCheck className="size-4" strokeWidth={2.5} />
              ) : (
                <Copy className="size-4 opacity-80" />
              )}
            </Button>
          )}
          <CodeMirror
            {...rest}
            value={value}
            onChange={onChange}
            extensions={baseExtensions}
            editable={editable}
            theme="none"
            basicSetup={{
              lineNumbers: true,
              foldGutter,
              bracketMatching: true,
              closeBrackets: true,
              highlightActiveLine: editable,
            }}
            className={cn('h-full w-full', className)}
          />
        </div>
      )}
    </div>
  );
}
