import { Badge } from '@/components/ui/badge';
import { cn } from '@/lib/utils';

const STATUS_CLASSES: Record<string, string> = {
  draft: 'border-transparent bg-muted text-muted-foreground',
  validated: 'border-transparent bg-primary/15 text-primary',
  published: 'border-transparent bg-positive/15 text-positive',
  retired: 'text-muted-foreground line-through',
  active: 'border-transparent bg-positive/15 text-positive',
  historical: 'border-transparent bg-muted text-muted-foreground',
};

/** Colored badge for a version status (draft, validated, published or retired) or an assumption set status (active or historical). */
export function StatusBadge({ status }: { status: string }) {
  return (
    <Badge
      variant="outline"
      data-slot="status-badge"
      data-status={status}
      className={cn('capitalize', STATUS_CLASSES[status])}
    >
      {status}
    </Badge>
  );
}
