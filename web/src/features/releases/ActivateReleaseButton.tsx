import { Power } from 'lucide-react';
import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/shared/ConfirmDialog';

export interface ActivateReleaseButtonProps {
  releaseKey: string;
  isPending: boolean;
  /** Runs once the user confirms. */
  onActivate: () => void;
}

/** "Activate" for a published release, confirmed first, because queries switch to the release's dataset versions. */
export function ActivateReleaseButton({ releaseKey, isPending, onActivate }: ActivateReleaseButtonProps) {
  const [isConfirming, setIsConfirming] = useState(false);
  return (
    <>
      <Button disabled={isPending} onClick={() => setIsConfirming(true)}>
        <Power />
        Activate
      </Button>
      <ConfirmDialog
        open={isConfirming}
        onOpenChange={setIsConfirming}
        title={`Activate ${releaseKey}?`}
        description="Queries switch to this release's dataset versions, and the semantic revision advances so the query engine reloads its metadata."
        confirmLabel="Activate"
        onConfirm={onActivate}
      />
    </>
  );
}
