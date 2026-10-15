import { Copy, Plus, Trash2 } from 'lucide-react';
import { useState, type ReactNode } from 'react';
import { toast } from 'sonner';
import { Combobox } from '@registry/components/Combobox';
import { TruncatedText } from '@registry/components/TruncatedText';
import { Button } from '@/components/ui/button';
import { ApiErrorAlert } from './ApiErrorAlert';
import { ConfirmDialog, type ConfirmDialogProps } from './ConfirmDialog';
import { StatusBadge } from './StatusBadge';
import type { LookupItem, VersionKind, VersionStatus, VersionStatusResult } from './types';
import { useVersionTransition } from './useVersionTransition';
import { getVersionTransitions, type VersionTransition } from './versionTransitions';

export interface VersionHeaderProps {
  kind: VersionKind;
  /** Versions of the entity, e.g. `useLookup('dataset-versions', datasetId).data`. */
  versions: LookupItem[];
  selectedVersionId: number | null;
  onSelectVersion: (id: number) => void;
  /** Shows "New version". */
  onCreateVersion?: () => void;
  /** Shows "Clone selected". */
  onCloneVersion?: (sourceId: number) => void;
  /** Shows "Delete draft" on draft versions (confirmed first). */
  onDeleteVersion?: (id: number) => void;
  /** Called after a status change succeeded, e.g. to select or refetch the version. */
  onTransitioned?: (result: VersionStatusResult) => void;
  /** Replaces the confirmation text of the transitions to these statuses, e.g. a warning before retiring a release. */
  confirmDescriptions?: Partial<Record<VersionStatus, ReactNode>>;
}

type Confirmation = Omit<ConfirmDialogProps, 'open' | 'onOpenChange'>;

/**
 * Version picker for one versioned entity: status badge, the lifecycle actions valid for the
 * selected version, and create/clone actions. Rejected transitions (409, or 422 with rule
 * violations) are shown inline while the version they were for stays selected.
 */
export function VersionHeader({
  kind,
  versions,
  selectedVersionId,
  onSelectVersion,
  onCreateVersion,
  onCloneVersion,
  onDeleteVersion,
  onTransitioned,
  confirmDescriptions,
}: VersionHeaderProps) {
  const transition = useVersionTransition(kind);
  const [confirmation, setConfirmation] = useState<Confirmation | null>(null);
  const selected = versions.find(version => version.id === selectedVersionId);
  const status = selected?.status as VersionStatus | undefined;
  const transitionError = transition.variables?.id === selectedVersionId ? transition.error : null;

  function requestTransition(version: LookupItem, action: VersionTransition) {
    const run = () =>
      transition.mutate(
        { id: version.id, status: action.to },
        {
          onSuccess: result => {
            toast.success(`${version.key} is now ${result.status}.`);
            onTransitioned?.(result);
          },
        }
      );
    if (action.confirm) {
      const description = confirmDescriptions?.[action.to] ?? action.confirm.description;
      setConfirmation({ ...action.confirm, description, confirmLabel: action.label, onConfirm: run });
      return;
    }
    run();
  }

  function requestDelete(version: LookupItem, deleteVersion: (id: number) => void) {
    setConfirmation({
      title: 'Delete this draft?',
      description: `${version.key} and its changes will be deleted.`,
      confirmLabel: 'Delete draft',
      destructive: true,
      onConfirm: () => deleteVersion(version.id),
    });
  }

  return (
    <div data-slot="version-header" className="space-y-3">
      <div className="flex flex-wrap items-center gap-2">
        <VersionPicker
          versions={versions}
          selectedVersionId={selectedVersionId}
          onSelectVersion={onSelectVersion}
        />
        {selected && status && (
          <>
            <StatusBadge status={status} />
            {getVersionTransitions(kind, status).map(action => (
              <Button
                key={action.to}
                variant="outline"
                size="sm"
                disabled={transition.isPending}
                onClick={() => requestTransition(selected, action)}
              >
                {action.label}
              </Button>
            ))}
            {status === 'draft' && onDeleteVersion && (
              <Button
                variant="outline"
                size="sm"
                className="text-destructive"
                onClick={() => requestDelete(selected, onDeleteVersion)}
              >
                <Trash2 />
                Delete draft
              </Button>
            )}
          </>
        )}
        <div className="ml-auto flex items-center gap-2">
          {selected && onCloneVersion && (
            <Button variant="outline" size="sm" onClick={() => onCloneVersion(selected.id)}>
              <Copy />
              Clone selected
            </Button>
          )}
          {onCreateVersion && (
            <Button size="sm" onClick={onCreateVersion}>
              <Plus />
              New version
            </Button>
          )}
        </div>
      </div>
      <ApiErrorAlert error={transitionError} />
      {confirmation && (
        <ConfirmDialog open onOpenChange={() => setConfirmation(null)} {...confirmation} />
      )}
    </div>
  );
}

function VersionPicker({
  versions,
  selectedVersionId,
  onSelectVersion,
}: Pick<VersionHeaderProps, 'versions' | 'selectedVersionId' | 'onSelectVersion'>) {
  const statuses = new Map(versions.map(version => [String(version.id), version.status as string]));
  return (
    <Combobox
      className="w-72"
      size="sm"
      data={versions.map(version => ({
        value: String(version.id),
        label: `${version.key} — ${version.label}`,
      }))}
      value={selectedVersionId === null ? '' : String(selectedVersionId)}
      onValueChange={value => onSelectVersion(Number(value))}
      placeholder="Select a version"
      searchPlaceholder="Search versions..."
      emptyMessage="No versions yet."
      sort="none"
      itemRenderer={item => (
        <span className="flex w-full items-center justify-between gap-2">
          <span className="min-w-0 flex-1">
            <TruncatedText text={item.label} />
          </span>
          <StatusBadge status={statuses.get(item.value) as string} />
        </span>
      )}
    />
  );
}
