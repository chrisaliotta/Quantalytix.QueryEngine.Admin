import { lazy, Suspense } from 'react';
import type { CodeEditorProps } from '@registry/components/CodeEditor';
import { DEFAULT_HEIGHT } from '@registry/components/CodeEditor/constants';
import { Loading } from '@registry/components/Loading';

const RegistryCodeEditor = lazy(async () => {
  await import('./monaco');
  const { CodeEditor } = await import('@registry/components/CodeEditor');
  return { default: CodeEditor };
});

/**
 * The registry CodeEditor (Monaco), loaded on first render together with the bundled
 * monaco-editor: Monaco stays out of the main bundle and needs no CDN. Use this instead of
 * `@registry/components/CodeEditor`, and pass `language` (only the base editor worker is bundled).
 */
export function CodeEditor(props: CodeEditorProps) {
  return (
    <Suspense
      fallback={
        <div
          className="flex items-center justify-center border"
          style={{ height: props.height ?? DEFAULT_HEIGHT }}
        >
          <Loading size="sm" />
        </div>
      }
    >
      <RegistryCodeEditor {...props} />
    </Suspense>
  );
}
