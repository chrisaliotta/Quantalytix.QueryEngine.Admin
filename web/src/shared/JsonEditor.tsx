import { lazy, Suspense } from 'react';
import type { JsonEditorProps } from '@registry/components/JsonEditor';
import { Loading } from '@registry/components/Loading';

const RegistryJsonEditor = lazy(async () => {
  const { JsonEditor } = await import('@registry/components/JsonEditor');
  return { default: JsonEditor };
});

/**
 * The registry JsonEditor (CodeMirror), loaded on first render so CodeMirror stays out of the
 * main bundle. Use this instead of `@registry/components/JsonEditor`.
 */
export function JsonEditor(props: JsonEditorProps) {
  return (
    <Suspense fallback={<Loading size="sm" className="h-full border" />}>
      <RegistryJsonEditor {...props} />
    </Suspense>
  );
}
