import { Combobox } from '@registry/components/Combobox';
import { IconTooltip } from '@registry/components/IconTooltip';
import { ClearSelectionButton } from './ClearSelectionButton';
import { useVocabulary } from './useVocabularies';

export interface VocabularySelectProps {
  /** Vocabulary name, e.g. `'measure_kind'`. */
  vocabulary: string;
  value: string | null;
  onChange: (value: string | null) => void;
  /** Shows a clear button that sets the value to null. */
  allowEmpty?: boolean;
  /** Values of the vocabulary the picker does not offer, such as `all_mapped` for a variable target. */
  exclude?: string[];
  disabled?: boolean;
  placeholder?: string;
}

/** Searchable single-value picker over a vocabulary; each option shows its description as a tooltip. */
export function VocabularySelect({
  vocabulary,
  value,
  onChange,
  allowEmpty = false,
  exclude = [],
  disabled = false,
  placeholder = 'Select a value',
}: VocabularySelectProps) {
  const { data, isPending } = useVocabulary(vocabulary);
  const descriptions = new Map(data?.values.map(option => [option.value, option.description]));
  const items =
    data?.values.filter(option => !exclude.includes(option.value)).map(option => ({ value: option.value, label: option.value })) ?? [];

  return (
    <div data-slot="vocabulary-select" className="flex items-center gap-1">
      <Combobox
        className="min-w-0 flex-1"
        data={items}
        value={value ?? ''}
        onValueChange={onChange}
        placeholder={isPending ? 'Loading...' : placeholder}
        emptyMessage="No matching values."
        disabled={disabled || isPending}
        sort="none"
        itemRenderer={item => (
          <VocabularyOption value={item.value} description={descriptions.get(item.value)} />
        )}
      />
      {allowEmpty && value !== null && !disabled && (
        <ClearSelectionButton onClick={() => onChange(null)} />
      )}
    </div>
  );
}

/** A vocabulary value with its description in an info tooltip. */
export function VocabularyOption({ value, description }: { value: string; description?: string }) {
  return (
    <span data-slot="vocabulary-option" className="flex w-full items-center justify-between gap-2">
      <span className="truncate">{value}</span>
      <IconTooltip description={description} />
    </span>
  );
}
