import { Combobox } from '@registry/components/Combobox';
import { ClearSelectionButton } from './ClearSelectionButton';
import type { LookupItem, LookupKind } from './types';
import { useLookup } from './useLookup';

export interface EntitySelectProps {
  lookupKind: LookupKind;
  /** Parent id for parent-scoped kinds; `null` disables the select until the parent is chosen. */
  parentId?: number | null;
  value: number | null;
  onChange: (value: number | null) => void;
  /** Shows a clear button that sets the value to null. */
  allowEmpty?: boolean;
  /** Narrows the options, e.g. `item => item.status === 'published'`. */
  filter?: (item: LookupItem) => boolean;
  disabled?: boolean;
  placeholder?: string;
}

/** Searchable picker over lookup items, labelled `key — label`; the value is the item id. */
export function EntitySelect({
  lookupKind,
  parentId,
  value,
  onChange,
  allowEmpty = false,
  filter,
  disabled = false,
  placeholder = 'Select...',
}: EntitySelectProps) {
  const lookup = useLookup(lookupKind, parentId);
  const items = lookup.data ?? [];
  const options = (filter ? items.filter(filter) : items).map(item => ({
    value: String(item.id),
    label: `${item.key} — ${item.label}`,
  }));
  const isWaitingForParent = parentId === null;

  return (
    <div data-slot="entity-select" className="space-y-1">
      <div className="flex items-center gap-1">
        <Combobox
          className="min-w-0 flex-1"
          data={options}
          value={value === null ? '' : String(value)}
          onValueChange={next => onChange(Number(next))}
          placeholder={resolvePlaceholder(placeholder, isWaitingForParent, lookup.isLoading)}
          emptyMessage="No matching items."
          disabled={disabled || isWaitingForParent || lookup.isLoading}
        />
        {allowEmpty && value !== null && !disabled && (
          <ClearSelectionButton onClick={() => onChange(null)} />
        )}
      </div>
      {lookup.error && <p className="text-sm text-destructive">{lookup.error.message}</p>}
    </div>
  );
}

function resolvePlaceholder(placeholder: string, isWaitingForParent: boolean, isLoading: boolean) {
  if (isWaitingForParent) {
    return 'Choose the parent first';
  }
  if (isLoading) {
    return 'Loading...';
  }
  return placeholder;
}
