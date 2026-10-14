import { Card, CardContent } from '@/components/ui/card';
import { PageHeader } from './PageHeader';

/** Stand-in page for a route whose feature slice is not built yet. */
export function PlaceholderPage({ title }: { title: string }) {
  return (
    <div data-slot="placeholder-page" className="space-y-6">
      <PageHeader title={title} />
      <Card>
        <CardContent className="text-sm text-muted-foreground">
          This page is not implemented yet.
        </CardContent>
      </Card>
    </div>
  );
}
