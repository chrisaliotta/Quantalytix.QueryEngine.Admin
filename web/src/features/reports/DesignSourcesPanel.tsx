import { ExternalLink, Eye, Pencil, Plus, Trash2 } from 'lucide-react';
import { useState, type ReactNode } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { ConfirmDialog } from '@/shared/ConfirmDialog';
import { useLookup } from '@/shared/useLookup';
import { useDeleteDesignSource } from './designApi';
import { DesignSourceSheet } from './DesignSourceSheet';
import { SourceProfilesTab } from './SourceProfilesTab';
import type { DesignSource, ReportDesign, ReportVersion } from './types';

export interface DesignSourcesPanelProps {
  version: ReportVersion;
  design: ReportDesign;
  isEditable: boolean;
  /** Opens the advanced Sources tab, which edits the sources the design shows read-only. */
  onShowSources: () => void;
}

/** What the source sheet was opened for: a source, or a new one. */
interface SourceSheetRequest {
  source: DesignSource | null;
}

/**
 * The design's sources: each reads one dataset and maps the members of the dimension the dataset binds to the report's rows.
 * A draft or the published version adds, edits and deletes them in a sheet. A source whose datasets differ, and the sources
 * that classify directly or through a mapping set, show read-only and link to the Sources tab. When a variable has several
 * sources, the profiles that pick between them show below.
 */
export function DesignSourcesPanel({ version, design, isEditable, onShowSources }: DesignSourcesPanelProps) {
  const remove = useDeleteDesignSource(version.reportVersionId);
  const datasets = useLookup('datasets');
  const roles = useLookup('dimension-roles');
  const dimensions = useLookup('dimensions');
  const mappingSets = useLookup('mapping-sets');
  const [sheet, setSheet] = useState<SourceSheetRequest | null>(null);
  const [deleting, setDeleting] = useState<DesignSource | null>(null);
  const datasetKeys = new Map(datasets.data?.map(item => [item.id, item.key]));
  const roleKeys = new Map(roles.data?.map(item => [item.id, item.key]));
  const dimensionKeys = new Map(dimensions.data?.map(item => [item.id, item.key]));
  const mappingSetKeys = new Map(mappingSets.data?.map(item => [item.id, item.key]));
  const variableKeys = [...design.sources.flatMap(source => source.variables), ...design.otherSources].map(source => source.variableKey);
  const sharedVariables = [...new Set(variableKeys.filter((key, index) => variableKeys.indexOf(key) !== index))];

  function itemCount(source: DesignSource) {
    return design.rows.reduce((total, row) => total + row.mappings.filter(mapping => mapping.bridgeId === source.bridgeId).length, 0);
  }

  return (
    <div data-slot="design-sources-panel" className="space-y-3">
      {isEditable && (
        <Button size="sm" onClick={() => setSheet({ source: null })}>
          <Plus />
          New source
        </Button>
      )}
      <ApiErrorAlert error={remove.error} />
      {design.sources.length === 0 && design.otherSources.length === 0 && (
        <p className="py-6 text-center text-sm text-muted-foreground">
          No sources yet. A source reads one dataset and maps the members of a dimension it binds to the report's rows.
        </p>
      )}
      {design.sources.map(source => (
        <SourceCard
          key={source.bridgeId}
          title={source.label}
          subtitle={source.key}
          badges={<Badge variant="secondary">{itemCount(source)} items mapped</Badge>}
          actions={
            source.isUniform ? (
              <>
                <Button variant="ghost" size="icon-xs" aria-label={`Open ${source.key}`} onClick={() => setSheet({ source })}>
                  {isEditable ? <Pencil /> : <Eye />}
                </Button>
                {isEditable && (
                  <Button variant="ghost" size="icon-xs" aria-label={`Delete ${source.key}`} onClick={() => setDeleting(source)}>
                    <Trash2 />
                  </Button>
                )}
              </>
            ) : (
              <SourcesTabLink onShowSources={onShowSources} />
            )
          }
        >
          <div>
            Maps members of <span className="font-medium">{dimensionKeys.get(source.sourceDimensionId)}</span>
            {source.isUniform &&
              ` through role ${roleKeys.get(source.roleId!)} of dataset ${datasetKeys.get(source.datasetId!)}, by ${source.timeFieldKey}`}
          </div>
          {!source.isUniform && (
            <div>Its variables read different datasets, so the Sources tab edits them; its row mappings stay editable here.</div>
          )}
          <div className="flex flex-wrap gap-1">
            {source.variables.map(variable => (
              <Badge key={variable.variableSourceId} variant="outline">
                {variable.variableKey} ← {variable.measureKey}
              </Badge>
            ))}
          </div>
        </SourceCard>
      ))}
      {design.otherSources.map(source => (
        <SourceCard
          key={source.variableSourceId}
          title={source.sourceKey}
          subtitle={`${source.classificationKind} classification`}
          actions={<SourcesTabLink onShowSources={onShowSources} />}
        >
          <div>
            Reads {source.variableKey} from {source.measureKey} of dataset {datasetKeys.get(source.datasetId)}, by field{' '}
            {source.classificationFieldKey}
            {source.mappingSetId != null && ` through mapping set ${mappingSetKeys.get(source.mappingSetId) ?? source.mappingSetId}`}; the
            Sources tab edits it.
          </div>
        </SourceCard>
      ))}
      {sharedVariables.length > 0 && (
        <section data-slot="design-source-profiles" className="space-y-2 pt-2">
          <h3 className="text-sm font-medium">Profiles</h3>
          <p className="text-sm text-muted-foreground">
            {sharedVariables.join(', ')} {sharedVariables.length === 1 ? 'has' : 'have'} several sources; each profile picks the
            one a request reads, and the version's default profile is read when a request names none.
          </p>
          <SourceProfilesTab version={version} isEditable={isEditable} />
        </section>
      )}
      {sheet && <DesignSourceSheet version={version} source={sheet.source} readOnly={!isEditable} onClose={() => setSheet(null)} />}
      <ConfirmDialog
        open={deleting !== null}
        onOpenChange={open => !open && setDeleting(null)}
        title={`Delete source ${deleting?.key ?? ''}?`}
        description="Its variable sources and the profile picks that name them are deleted, and its items no longer map to rows."
        confirmLabel="Delete source"
        destructive
        onConfirm={() => deleting && remove.mutate(deleting.bridgeId)}
      />
    </div>
  );
}

interface SourceCardProps {
  title: string;
  subtitle: string;
  badges?: ReactNode;
  actions: ReactNode;
  children: ReactNode;
}

/** One source: its title and key, badges and actions, above what it reads. */
function SourceCard({ title, subtitle, badges, actions, children }: SourceCardProps) {
  return (
    <div data-slot="design-source-card" className="space-y-2 rounded-md border p-3 text-sm">
      <div className="flex items-center gap-2">
        <span className="font-medium">{title}</span>
        <span className="font-mono text-xs text-muted-foreground">{subtitle}</span>
        {badges}
        <div className="ml-auto flex items-center">{actions}</div>
      </div>
      <div className="space-y-2 text-muted-foreground">{children}</div>
    </div>
  );
}

/** Opens the advanced Sources tab, which edits what the design shows read-only. */
function SourcesTabLink({ onShowSources }: { onShowSources: () => void }) {
  return (
    <Button variant="ghost" size="sm" onClick={onShowSources}>
      <ExternalLink />
      Sources tab
    </Button>
  );
}
