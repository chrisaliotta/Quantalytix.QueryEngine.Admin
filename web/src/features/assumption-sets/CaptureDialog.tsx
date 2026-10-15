import { useState } from 'react';
import { toast } from 'sonner';
import { MultiSelect } from '@registry/components/MultiSelect';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { useLookup } from '@/shared/useLookup';
import { formatDateTime } from '@/shared/utcTime';
import { selectedDimensionIds } from './captureSelection';
import type { AssumptionSet } from './types';
import { useCaptureAssumptionPins } from './useAssumptionSets';

export interface CaptureDialogProps {
  assumptionSet: AssumptionSet;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

/**
 * "Capture effective versions": pins, for each selected dimension, its published version effective
 * at the set's effective time, replacing those dimensions' pins. Dimensions without such a version
 * end up unpinned, and a warning names them.
 */
export function CaptureDialog({ assumptionSet, open, onOpenChange }: CaptureDialogProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent data-slot="capture-dialog">
        <DialogHeader>
          <DialogTitle>Capture effective versions</DialogTitle>
          <DialogDescription>
            Pins, for each dimension, the published version effective at {formatDateTime(assumptionSet.effectiveAt)}. Existing
            pins of these dimensions are replaced.
          </DialogDescription>
        </DialogHeader>
        <CaptureForm assumptionSet={assumptionSet} onDone={() => onOpenChange(false)} />
      </DialogContent>
    </Dialog>
  );
}

/** Mounted only while the dialog is open, so every opening starts without a selection. */
function CaptureForm({ assumptionSet, onDone }: { assumptionSet: AssumptionSet; onDone: () => void }) {
  const dimensions = useLookup('dimensions');
  const capture = useCaptureAssumptionPins(assumptionSet.assumptionSetId);
  const [selected, setSelected] = useState<string[]>([]);

  function submit() {
    const requested = selectedDimensionIds(selected, dimensions.data ?? []);
    capture.mutate(requested, {
      onSuccess: pins => {
        const unpinned = requested.filter(id => !pins.some(pin => pin.dimensionId === id));
        toast.success(`Captured ${requested.length - unpinned.length} of ${requested.length} dimensions.`);
        if (unpinned.length > 0) {
          const keys = unpinned.map(id => dimensions.data?.find(item => item.id === id)?.key ?? String(id));
          toast.warning(
            `No published version is effective at ${formatDateTime(assumptionSet.effectiveAt)} for ${keys.join(', ')}, so they are not pinned.`
          );
        }
        onDone();
      },
    });
  }

  return (
    <>
      <MultiSelect
        options={dimensions.data?.map(item => ({ value: String(item.id), label: `${item.key} — ${item.label}` }))}
        value={selected}
        onChange={setSelected}
        isLoading={dimensions.isPending}
        placeholder="Select dimensions"
        selectAll
        sort="none"
      />
      <ApiErrorAlert error={capture.error} />
      <DialogFooter>
        <Button variant="outline" onClick={onDone}>
          Cancel
        </Button>
        <Button disabled={selected.length === 0 || capture.isPending} onClick={submit}>
          Capture
        </Button>
      </DialogFooter>
    </>
  );
}
