import { Combobox, type ComboboxItem } from '@registry/components/Combobox';
import { ClearSelectionButton } from './ClearSelectionButton';
import { lookupItemText } from './lookupItemText';
import type { DimensionPurpose, LookupItem, LookupKind } from './types';
import { useLookup } from './useLookup';

export interface EntitySelectProps {
  lookupKind: LookupKind;
  /** Parent id for parent-scoped kinds; `null` disables the select until the parent is chosen. */
  parentId?: number | null;
  /** Narrows the `dimensions` kind to dimensions of one purpose. */
  purpose?: DimensionPurpose;
  value: number | null;
  onChange: (value: number | null) => void;
  /** Shows a clear button that sets the value to null. */
  allowEmpty?: boolean;
  /** Narrows the options, e.g. `item => item.status === 'published'`. */
  filter?: (item: LookupItem) => boolean;
  disabled?: boolean;
  placeholder?: string;
}

/**
 * Searchable picker over lookup items, labelled by key and label (`lookupItemText`); the value is the item id. A value
 * the options do not list, such as one the filter hides or a reference to another version, shows its raw ID.
 */
export function EntitySelect({
  lookupKind,
  parentId,
  purpose,
  value,
  onChange,
  allowEmpty = false,
  filter,
  disabled = false,
  placeholder = 'Select...',
}: EntitySelectProps) {
  const lookup = useLookup(lookupKind, parentId, purpose);
  const items = lookup.data ?? [];
  const options = (filter ? items.filter(filter) : items).map(item => ({
    value: String(item.id),
    label: lookupItemText(item),
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
          selectedItemRenderer={item => <SelectedItem item={item} value={value} isLoaded={lookup.isSuccess} />}
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

/** The trigger's text: the selected option, or once the options have loaded, the raw ID of a value they do not list. */
function SelectedItem({ item, value, isLoaded }: { item: ComboboxItem | undefined; value: number | null; isLoaded: boolean }) {
  if (item) {
    return item.label;
  }
  if (!isLoaded) {
    return null;
  }
  return <span className="text-muted-foreground">{value} (not in list)</span>;
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
