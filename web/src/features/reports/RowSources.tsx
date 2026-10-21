import { ChevronRight, Folder, ListChecks, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { count } from '@/shared/text';
import { useDimensionVersionMembers } from './api';
import { useReplaceSourceItems } from './editorApi';
import { FactorControl } from './FactorControl';
import { ItemPickerSheet } from './ItemPickerSheet';
import { branchItems, sourceSections, type SourceSection } from './itemMappings';
import type { DimensionMember, EditorRow, EditorView, SourceItemInput } from './types';

export interface RowSourcesProps {
  view: EditorView;
  /** The row selected in the tree; null before one is. */
  row: EditorRow | null;
}

/**
 * What each source gives the selected data row, one section per source: a bridge source's rules, each an item or a folder of items
 * with a factor, which are changed, removed or added here, and a direct source's field, whose rows with the row's key are the row's.
 */
export function RowSources({ view, row }: RowSourcesProps) {
  if (row === null) {
    return <p className="py-6 text-center text-sm text-muted-foreground">Select a row to see what each source gives it.</p>;
  }
  if (row.kind !== 'data') {
    return (
      <p className="py-6 text-center text-sm text-muted-foreground">
        {row.kind === 'group' ? 'A group totals the rows under it' : 'Sections and formula rows show no source values'}; only data
        rows take source rows.
      </p>
    );
  }
  if (view.sources.length === 0) {
    return (
      <p className="py-6 text-center text-sm text-muted-foreground">
        No sources yet. Add one in the Sources tab; its rows reach this row by bridge or by field.
      </p>
    );
  }
  return (
    <div data-slot="row-sources" className="space-y-3">
      {sourceSections(view, row).map(section => (
        <SourceSectionCard key={section.source.source.reportSourceId} view={view} row={row} section={section} />
      ))}
    </div>
  );
}

interface SourceSectionCardProps {
  view: EditorView;
  row: EditorRow;
  section: SourceSection;
}

/** One source's section of a data row. */
function SourceSectionCard({ view, row, section }: SourceSectionCardProps) {
  const { source } = section;
  return (
    <section data-slot="row-source-section" className="space-y-2 rounded-md border p-3 text-sm">
      <div className="flex items-center gap-2">
        <span className="font-medium">{source.source.displayName}</span>
        <span className="font-mono text-xs text-muted-foreground">{source.datasetKey}</span>
        <Badge variant="outline">{section.method === 'bridge' ? `Bridge from ${source.roleKey}` : 'Direct'}</Badge>
      </div>
      {section.method === 'bridge' ? (
        <BridgeItems view={view} row={row} section={section} />
      ) : (
        <p className="text-muted-foreground">
          Rows whose <code className="text-foreground">{section.fieldKey}</code> is <code className="text-foreground">{section.rowKey}</code>.
        </p>
      )}
    </section>
  );
}

interface BridgeItemsProps {
  view: EditorView;
  row: EditorRow;
  section: Extract<SourceSection, { method: 'bridge' }>;
}

/**
 * A bridge source's rules on the row, with their labels and factors. A folder rule lists the items it takes now, each of which can
 * be taken back out of the row with a rule at the folder's factor negated; each rule's factor changes, each rule is removed, and
 * rules are added in the picker.
 */
function BridgeItems({ view, row, section }: BridgeItemsProps) {
  const replace = useReplaceSourceItems(view.report.reportId);
  const members = useDimensionVersionMembers(section.source.itemDimensionVersionId ?? null);
  const [picking, setPicking] = useState(false);
  const labels = new Map(members.data?.map(member => [member.memberKey, member.displayName]));
  const sourceId = section.source.source.reportSourceId;
  const takenAlone = new Set(section.items.filter(item => item.selection === 'member').map(item => item.itemKey));

  function save(items: SourceItemInput[]) {
    replace.mutate({ sourceId, rowKey: row.key, items });
  }

  return (
    <div className="space-y-2">
      {section.items.length === 0 && <p className="text-muted-foreground">No rules yet; the source adds nothing to the row.</p>}
      <ul className="divide-y">
        {section.items.map(item => (
          <li key={`${item.selection}:${item.itemKey}`} className="py-1">
            <div className="flex items-center gap-2">
              <FactorControl
                factor={item.factor}
                ruleLabel={item.itemKey}
                disabled={replace.isPending}
                onChange={factor => save(section.items.map(other => (other === item ? { ...other, factor } : other)))}
              />
              {item.selection === 'descendants' && <Folder className="size-4 shrink-0 text-muted-foreground" />}
              <span className="font-mono text-xs">{item.itemKey}</span>
              <span className="truncate text-muted-foreground">
                {item.selection === 'descendants' ? `All of ${labels.get(item.itemKey) ?? ''}` : labels.get(item.itemKey)}
              </span>
              <Button
                variant="ghost"
                size="icon-xs"
                className="ml-auto"
                aria-label={`Remove ${item.itemKey}`}
                disabled={replace.isPending}
                onClick={() => save(section.items.filter(other => other !== item))}
              >
                <Trash2 />
              </Button>
            </div>
            {item.selection === 'descendants' && members.data && (
              <FolderItems
                members={members.data}
                folder={item}
                takenAlone={takenAlone}
                disabled={replace.isPending}
                onExclude={itemKey => save([...section.items, { itemKey, selection: 'member', factor: -item.factor }])}
              />
            )}
          </li>
        ))}
      </ul>
      <Button variant="outline" size="sm" onClick={() => setPicking(true)}>
        <ListChecks />
        Add items
      </Button>
      <ApiErrorAlert error={replace.error ?? members.error} />
      {picking && <ItemPickerSheet view={view} source={section.source} row={row} onClose={() => setPicking(false)} />}
    </div>
  );
}

interface FolderItemsProps {
  members: DimensionMember[];
  folder: SourceItemInput;
  /** The items the row already has a rule of their own for, which it can't take back out again. */
  takenAlone: ReadonlySet<string>;
  disabled: boolean;
  onExclude: (itemKey: string) => void;
}

/** The items a folder rule takes now, collapsed at first; each item can be taken back out of the row. */
function FolderItems({ members, folder, takenAlone, disabled, onExclude }: FolderItemsProps) {
  const [open, setOpen] = useState(false);
  const items = branchItems(members, folder.itemKey);
  const labels = new Map(members.map(member => [member.memberKey, member.displayName]));
  return (
    <div className="pl-16">
      <button type="button" className="flex items-center gap-1 text-xs text-muted-foreground" onClick={() => setOpen(!open)}>
        <ChevronRight className={cn('size-3 transition-transform', open && 'rotate-90')} />
        {count(items.length, ['item', 'items'])} now, and any added under {folder.itemKey} later
      </button>
      {open && (
        <ul className="mt-1 space-y-0.5">
          {items.map(itemKey => (
            <li key={itemKey} className="flex items-center gap-2 text-xs">
              <span className="font-mono">{itemKey}</span>
              <span className="truncate text-muted-foreground">{labels.get(itemKey)}</span>
              {takenAlone.has(itemKey) ? (
                <Badge variant="outline" className="ml-auto">
                  rule of its own
                </Badge>
              ) : (
                <Button variant="ghost" size="xs" className="ml-auto" disabled={disabled} onClick={() => onExclude(itemKey)}>
                  Exclude
                </Button>
              )}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
