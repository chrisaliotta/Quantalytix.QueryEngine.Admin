import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';

/** Measures tab of the dataset-version editor; placeholder until the Bindings/Measures slice replaces it. */
export function MeasuresTab(props: { tenantId: string; datasetVersionId: number; isDraft: boolean }) {
  return (
    <Card data-slot="measures-tab">
      <CardHeader>
        <CardTitle>Measures</CardTitle>
        <CardDescription>Not implemented yet.</CardDescription>
      </CardHeader>
      <CardContent className="text-sm text-muted-foreground">
        Tenant {props.tenantId}, dataset version {props.datasetVersionId}
        {props.isDraft ? ' (draft)' : ' (read-only)'}.
      </CardContent>
    </Card>
  );
}
