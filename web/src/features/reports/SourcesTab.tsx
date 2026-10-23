import { ExternalLink, Pencil, Plus, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { Link } from 'react-router';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { ConfirmDialog } from '@/shared/ConfirmDialog';
import { useTenantId } from '@/shared/useTenantId';
import { useDeleteReportSource } from './editorApi';
import { SourceSheet } from './SourceSheet';
import type { EditorSource, EditorView } from './types';

export interface SourcesTabProps {
  view: EditorView;
  /** The source to highlight, such as the one a report bridge's page links to. */
  highlightedSourceId: number | null;
}

/** What the source sheet was opened for: a source, or a new one. */
interface SourceSheetRequest {
  source: EditorSource | null;
}

/**
 * The report's sources: each reads one dataset, on a time field, and reaches the report's rows by its own bridge from a role the
 * dataset binds, or by a field of row keys. Each card shows what the source reads, the variables it supplies and, for a bridge
 * source, how many items no row takes yet.
 */
export function SourcesTab({ view, highlightedSourceId }: SourcesTabProps) {
  const tenantId = useTenantId();
  const remove = useDeleteReportSource(view.report.reportId);
  const [sheet, setSheet] = useState<SourceSheetRequest | null>(null);
  const [deleting, setDeleting] = useState<EditorSource | null>(null);

  return (
    <div data-slot="sources-tab" className="space-y-3">
      <Button size="sm" onClick={() => setSheet({ source: null })}>
        <Plus />
        New source
      </Button>
      <ApiErrorAlert error={remove.error} />
      {view.sources.length === 0 && (
        <p className="py-6 text-center text-sm text-muted-foreground">
          No sources yet. A source reads one dataset; its rows reach the report's rows through a bridge of its own or by a field of
          row keys.
        </p>
      )}
      {view.sources.map(editorSource => {
        const { source } = editorSource;
        const isBridge = source.mappingMethod === 'bridge';
        return (
          <section
            key={source.reportSourceId}
            data-slot="source-card"
            className={cn('space-y-2 rounded-md border p-3 text-sm', source.reportSourceId === highlightedSourceId && 'border-primary')}
          >
            <div className="flex items-center gap-2">
              <span className="font-medium">{source.displayName}</span>
              <span className="font-mono text-xs text-muted-foreground">{source.sourceKey}</span>
              <Badge variant="outline">{isBridge ? 'Bridge' : 'Direct'}</Badge>
              {isBridge && (
                <Badge variant={editorSource.unmappedCount === 0 ? 'secondary' : 'destructive'}>{editorSource.unmappedCount} unmapped</Badge>
              )}
              <div className="ml-auto flex items-center">
                <Button variant="ghost" size="icon-xs" aria-label={`Edit ${source.sourceKey}`} onClick={() => setSheet({ source: editorSource })}>
                  <Pencil />
                </Button>
                <Button variant="ghost" size="icon-xs" aria-label={`Delete ${source.sourceKey}`} onClick={() => setDeleting(editorSource)}>
                  <Trash2 />
                </Button>
              </div>
            </div>
            <div className="space-y-2 text-muted-foreground">
              <div>
                Reads <span className="font-medium text-foreground">{editorSource.datasetKey}</span> by {source.timeFieldKey};{' '}
                {isBridge ? (
                  <>
                    its rows classify by role <span className="font-medium text-foreground">{editorSource.dimensionRoleKey}</span>, whose members
                    the rows pick as items through{' '}
                    <Link className="inline-flex items-center gap-1 hover:underline" to={`/t/${tenantId}/bridges/${source.bridgeId}`}>
                      its bridge
                      <ExternalLink className="size-3" />
                    </Link>
                    .
                  </>
                ) : (
                  <>
                    each of its rows goes to the report row its <code className="text-foreground">{source.fieldKey}</code> names.
                  </>
                )}
              </div>
              <div className="flex flex-wrap gap-1">
                {editorSource.variables.length === 0 && <span>Supplies no variables yet; the Variables tab binds them.</span>}
                {editorSource.variables.map(variable => (
                  <Badge key={variable.variableKey} variant="outline">
                    {variable.variableKey} ← {variable.measureKey}
                    {variable.sourceFilterJson && ' (filtered)'}
                    {variable.missingBehavior && ` (missing as ${variable.missingBehavior})`}
                  </Badge>
                ))}
              </div>
            </div>
          </section>
        );
      })}
      {sheet && <SourceSheet view={view} source={sheet.source} onClose={() => setSheet(null)} />}
      <ConfirmDialog
        open={deleting !== null}
        onOpenChange={open => !open && setDeleting(null)}
        title={`Delete source ${deleting?.source.sourceKey ?? ''}?`}
        description="Its variables and the profile picks that name it are deleted, and a bridge source's bridge goes with it."
        confirmLabel="Delete source"
        destructive
        onConfirm={() => deleting && remove.mutate(deleting.source.reportSourceId)}
      />
    </div>
  );
}
