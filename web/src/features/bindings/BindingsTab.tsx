import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';

/** Bindings tab of the dataset-version editor; placeholder until the Bindings/Measures slice replaces it. */
export function BindingsTab(props: { tenantId: string; datasetVersionId: number; isDraft: boolean }) {
  return (
    <Card data-slot="bindings-tab">
      <CardHeader>
        <CardTitle>Bindings</CardTitle>
        <CardDescription>Not implemented yet.</CardDescription>
      </CardHeader>
      <CardContent className="text-sm text-muted-foreground">
        Tenant {props.tenantId}, dataset version {props.datasetVersionId}
        {props.isDraft ? ' (draft)' : ' (read-only)'}.
      </CardContent>
    </Card>
  );
}
