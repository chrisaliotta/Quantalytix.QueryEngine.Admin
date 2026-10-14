import { X } from 'lucide-react';
import { Button } from '@/components/ui/button';

/** Icon button that clears an optional select. */
export function ClearSelectionButton({ onClick }: { onClick: () => void }) {
  return (
    <Button
      type="button"
      variant="ghost"
      size="icon-sm"
      aria-label="Clear selection"
      onClick={onClick}
    >
      <X />
    </Button>
  );
}
