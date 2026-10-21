import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { Loading } from '@registry/components/Loading';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { KeyField } from '@/shared/form';
import { FormDialog } from '@/shared/FormDialog';
import { usePublishedVersionItems } from '@/shared/useLatestVersionItems';
import { useDesignCoverage, useReplaceRowMappings } from './designApi';
import type { DesignSource, ReportDesign } from './types';

export interface DesignCoveragePanelProps {
  versionId: number;
  design: ReportDesign;
  isEditable: boolean;
  /** The row selected in the tree, which assigning offers first when it is a data row. */
  selectedKey: string | null;
}

/** An unmapped item being assigned: its source and key. */
interface Assignment {
  bridgeId: number;
  itemKey: string;
}

/**
 * The items of each source that map to no row, which the report leaves out: the members of the source's bound dimension,
 * from its published version, that take source values but no row takes. A draft or the published version assigns each to a
 * data row.
 */
export function DesignCoveragePanel({ versionId, design, isEditable, selectedKey }: DesignCoveragePanelProps) {
  const [assigning, setAssigning] = useState<Assignment | null>(null);

  if (design.sources.length === 0) {
    return <p className="py-6 text-center text-sm text-muted-foreground">No sources yet, so no items to cover.</p>;
  }
  return (
    <div data-slot="design-coverage-panel" className="space-y-3">
      {design.sources.map(source => (
        <CoverageSection
          key={source.bridgeId}
          versionId={versionId}
          source={source}
          isEditable={isEditable}
          onAssign={itemKey => setAssigning({ bridgeId: source.bridgeId, itemKey })}
        />
      ))}
      {assigning && (
        <AssignItemDialog
          versionId={versionId}
          design={design}
          assignment={assigning}
          selectedKey={selectedKey}
          onClose={() => setAssigning(null)}
        />
      )}
    </div>
  );
}

interface CoverageSectionProps {
  versionId: number;
  source: DesignSource;
  isEditable: boolean;
  onAssign: (itemKey: string) => void;
}

/** One source's unmapped items, with their labels from the bound dimension's published version. */
function CoverageSection({ versionId, source, isEditable, onAssign }: CoverageSectionProps) {
  const unmapped = useDesignCoverage(versionId, source.bridgeId);
  const members = usePublishedVersionItems('dimension-versions', source.sourceDimensionId, 'members');
  const labels = new Map(members?.map(member => [member.key, member.label]));

  return (
    <section data-slot="design-coverage-source" className="space-y-2 rounded-md border p-3 text-sm">
      <div className="flex items-center gap-2">
        <span className="font-medium">{source.label}</span>
        {unmapped.data && (
          <Badge variant={unmapped.data.length === 0 ? 'secondary' : 'destructive'}>{unmapped.data.length} unmapped</Badge>
        )}
      </div>
      <ApiErrorAlert error={unmapped.error} />
      {unmapped.isPending && <Loading size="sm" />}
      {unmapped.data?.length === 0 && <p className="text-muted-foreground">Every item maps to a row.</p>}
      <ul className="divide-y">
        {unmapped.data?.map(itemKey => (
          <li key={itemKey} className="flex items-center gap-2 py-1">
            <span className="font-mono text-xs">{itemKey}</span>
            <span className="truncate text-muted-foreground">{labels.get(itemKey)}</span>
            {isEditable && (
              <Button variant="outline" size="xs" className="ml-auto" onClick={() => onAssign(itemKey)}>
                Assign to row
              </Button>
            )}
          </li>
        ))}
      </ul>
    </section>
  );
}

interface AssignItemDialogProps {
  versionId: number;
  design: ReportDesign;
  assignment: Assignment;
  selectedKey: string | null;
  onClose: () => void;
}

/** Adds one unmapped item to a data row's items, with factor 1. */
function AssignItemDialog({ versionId, design, assignment, selectedKey, onClose }: AssignItemDialogProps) {
  const replace = useReplaceRowMappings(versionId);
  const dataRows = design.rows.filter(row => row.kind === 'data');
  const form = useForm<{ rowKey: string | null }>({
    defaultValues: { rowKey: dataRows.some(row => row.key === selectedKey) ? selectedKey : null },
  });

  function submit({ rowKey }: { rowKey: string | null }) {
    const row = dataRows.find(candidate => candidate.key === rowKey)!;
    return replace.mutateAsync({ rowKey: row.key, mappings: [...row.mappings, { ...assignment, factor: 1 }] });
  }

  return (
    <FormDialog
      open
      onOpenChange={open => !open && onClose()}
      title={`Assign ${assignment.itemKey} to a row`}
      description="The row adds up the item's values from now on."
      form={form}
      onSubmit={submit}
      submitLabel="Assign"
    >
      <KeyField
        name="rowKey"
        label="Row"
        items={dataRows.map((row, index) => ({ id: index, key: row.key, label: row.label }))}
        description="A data row of the design."
      />
    </FormDialog>
  );
}
