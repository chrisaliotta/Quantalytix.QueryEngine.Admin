import { ExternalLink } from 'lucide-react';
import { Link } from 'react-router';
import { Loading } from '@registry/components/Loading';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { StatusBadge } from '@/shared/StatusBadge';
import type { LookupItem } from '@/shared/types';
import { useLookup } from '@/shared/useLookup';
import { useTenantId } from '@/shared/useTenantId';

export interface MappingVersionsTabProps {
  dimensionId: number;
  versionId: number;
  /** The version's key, such as `v2`, for the text. */
  versionKey: string;
}

/**
 * Read-only: the mapping versions pinned to this dimension version, per mapping set of the dimension, each linking to
 * its editor in Mappings. Cloning a version clones these as drafts.
 */
export function MappingVersionsTab({ dimensionId, versionId, versionKey }: MappingVersionsTabProps) {
  const sets = useLookup('mapping-sets', dimensionId);

  if (sets.isPending) {
    return <Loading size="sm" />;
  }
  if (sets.error) {
    return <ApiErrorAlert error={sets.error} />;
  }
  if (sets.data.length === 0) {
    return <p className="text-sm text-muted-foreground">No mapping set maps source codes onto this dimension.</p>;
  }
  return (
    <div data-slot="mapping-versions-tab" className="space-y-3">
      <p className="text-sm text-muted-foreground">
        Mapping versions pinned to {versionKey}, per mapping set of the dimension. They are edited in Mappings.
      </p>
      {sets.data.map(set => (
        <MappingSetVersions key={set.id} set={set} versionId={versionId} versionKey={versionKey} />
      ))}
    </div>
  );
}

function MappingSetVersions({ set, versionId, versionKey }: { set: LookupItem; versionId: number; versionKey: string }) {
  const versions = useLookup('mapping-versions', set.id);
  const pinned = (versions.data ?? []).filter(version => version.parentId === versionId);

  return (
    <div data-slot="mapping-set-versions" className="space-y-2 rounded-md border p-3">
      <div className="flex flex-wrap items-baseline gap-2">
        <span className="font-medium">{set.label}</span>
        <span className="font-mono text-xs text-muted-foreground">{set.key}</span>
        {set.classifier && <Badge variant="outline">{set.classifier}</Badge>}
      </div>
      <PinnedVersions set={set} pinned={pinned} isLoading={versions.isPending} versionKey={versionKey} />
      <ApiErrorAlert error={versions.error} />
    </div>
  );
}

function PinnedVersions(props: { set: LookupItem; pinned: LookupItem[]; isLoading: boolean; versionKey: string }) {
  const tenantId = useTenantId();
  if (props.isLoading) {
    return <Loading size="sm" />;
  }
  if (props.pinned.length === 0) {
    return <p className="text-sm text-muted-foreground">No version of this set pins {props.versionKey}.</p>;
  }
  return (
    <ul className="space-y-1">
      {props.pinned.map(version => (
        <li key={version.id} className="flex items-center gap-2 text-sm">
          <span className="font-mono">{version.key}</span>
          {version.status && <StatusBadge status={version.status} />}
          <Button asChild variant="link" size="sm">
            <Link to={`/t/${tenantId}/mappings/${props.set.id}?version=${version.id}`}>
              Open in Mappings
              <ExternalLink />
            </Link>
          </Button>
        </li>
      ))}
    </ul>
  );
}
