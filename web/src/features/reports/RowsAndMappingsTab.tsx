import { useState } from 'react';
import { RowSources } from './RowSources';
import { RowTree } from './RowTree';
import type { EditorView } from './types';
import { UnmappedItems } from './UnmappedItems';

/**
 * The report's rows on the left, and on the right what each source gives the selected row, above the items of each bridge
 * source that no row takes yet.
 */
export function RowsAndMappingsTab({ view }: { view: EditorView }) {
  const [selectedKey, setSelectedKey] = useState<string | null>(null);
  const selected = view.rows.find(row => row.key === selectedKey) ?? null;

  return (
    <div data-slot="rows-and-mappings-tab" className="grid items-start gap-4 lg:grid-cols-2">
      <RowTree view={view} selectedKey={selectedKey} onSelect={setSelectedKey} />
      <div className="space-y-6">
        <RowSources view={view} row={selected} />
        <UnmappedItems view={view} row={selected} />
      </div>
    </div>
  );
}
