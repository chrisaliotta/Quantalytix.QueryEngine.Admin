import { useForm, useWatch } from 'react-hook-form';
import { toast } from 'sonner';
import { Textarea } from '@/components/ui/textarea';
import { problemOnLines, type PastedRows, type UnresolvedLine } from './bulkLists';
import { FormFieldLayout, useFormField, type FormFieldProps } from './form';
import { FormDialog } from './FormDialog';

interface BulkPasteValues {
  text: string;
}

export interface BulkPasteDialogProps<TRow> {
  title: string;
  /** The line format, such as `source_key,member_key`. */
  format: string;
  /** The current list written as lines; the text starts with it, so the replace keeps what the user leaves alone. */
  initialText: string;
  /** Reads the text into rows and the lines that are none. */
  read: (text: string) => PastedRows<TRow>;
  /** Replaces the list with the rows, e.g. a bulk replace mutation's `mutateAsync`. */
  replace: (rows: TRow[]) => Promise<unknown>;
  /** The list's property in the API's validation keys, such as `entries`. */
  listName: string;
  /** What the rows are called, singular then plural, such as `['entry', 'entries']`. */
  noun: readonly [string, string];
  onClose: () => void;
}

/**
 * Dialog that replaces a draft's list from pasted lines. It starts with the current list, reads the lines as they are
 * edited, lists the lines it cannot read, and replaces the list with the rows of the others. A validation problem
 * names the pasted line it concerns.
 */
export function BulkPasteDialog<TRow>({
  title,
  format,
  initialText,
  read,
  replace,
  listName,
  noun,
  onClose,
}: BulkPasteDialogProps<TRow>) {
  const form = useForm<BulkPasteValues>({ defaultValues: { text: initialText } });
  const text = useWatch({ control: form.control, name: 'text' });
  const pasted = read(text);

  async function submit(values: BulkPasteValues) {
    const { rows, lineNumbers } = read(values.text);
    try {
      await replace(rows);
    } catch (error) {
      throw problemOnLines(error, listName, lineNumbers, 'text');
    }
    toast.success(`Saved ${count(rows.length, noun)}.`);
  }

  return (
    <FormDialog
      open
      onOpenChange={open => !open && onClose()}
      title={title}
      description={`One ${format} per line. The lines replace every ${noun[0]} of the draft, so the text starts with the current ${noun[1]}.`}
      form={form}
      onSubmit={submit}
      submitLabel={`Replace with ${count(pasted.rows.length, noun)}`}
    >
      <LinesField name="text" label="Lines" description="Spaces around values are ignored; leading zeros are kept." />
      <UnresolvedLines lines={pasted.unresolved} />
    </FormDialog>
  );
}

/** A monospace textarea of unwrapped lines that scrolls instead of growing past a screenful. */
function LinesField({ name, label, description }: FormFieldProps) {
  const { field, id, error, isDisabled } = useFormField(name);
  return (
    <FormFieldLayout id={id} label={label} description={description} error={error}>
      <Textarea
        {...field}
        id={id}
        wrap="off"
        spellCheck={false}
        className="max-h-80 min-h-40 overflow-auto font-mono text-xs md:text-xs"
        disabled={isDisabled}
        aria-invalid={error !== undefined}
      />
    </FormFieldLayout>
  );
}

/** The pasted lines the replace leaves out, each with its line number and reason. */
function UnresolvedLines({ lines }: { lines: UnresolvedLine[] }) {
  if (lines.length === 0) {
    return null;
  }
  return (
    <div data-slot="unresolved-lines" className="space-y-1">
      <p className="text-sm font-medium text-destructive">{count(lines.length, ['line is', 'lines are'])} left out:</p>
      <ul className="max-h-32 space-y-0.5 overflow-y-auto text-xs">
        {lines.map(line => (
          <li key={line.lineNumber}>
            <span className="text-muted-foreground">Line {line.lineNumber}:</span> <code>{line.text}</code> —{' '}
            {line.reason}
          </li>
        ))}
      </ul>
    </div>
  );
}

/** A count with its noun, such as `1 entry` or `3 entries`. */
function count(total: number, noun: readonly [string, string]) {
  return `${total} ${total === 1 ? noun[0] : noun[1]}`;
}
