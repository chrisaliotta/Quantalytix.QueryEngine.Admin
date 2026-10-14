import { Badge } from '@/components/ui/badge';

/** Badge for an administrative availability flag: "Enabled", or a muted "Disabled". */
export function EnabledBadge({ enabled }: { enabled: boolean }) {
  if (enabled) {
    return <Badge variant="outline">Enabled</Badge>;
  }
  return (
    <Badge variant="outline" className="text-muted-foreground">
      Disabled
    </Badge>
  );
}
