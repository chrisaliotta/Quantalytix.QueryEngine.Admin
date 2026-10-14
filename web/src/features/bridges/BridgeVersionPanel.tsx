import { Pencil } from 'lucide-react';
import { useState } from 'react';
import { Link } from 'react-router';
import { Button } from '@/components/ui/button';
import { StatusBadge } from '@/shared/StatusBadge';
import { useLookup } from '@/shared/useLookup';
import { useTenantId } from '@/shared/useTenantId';
import { BridgeMappings } from './BridgeMappings';
import { BridgeVersionSheet } from './BridgeVersionSheet';
import type { Bridge, BridgeVersion } from './types';

export interface BridgeVersionPanelProps {
  bridge: Bridge;
  version: BridgeVersion;
  isDraft: boolean;
}

/**
 * The selected bridge version: its endpoint versions and allocation semantics, editable on a draft, above its
 * mappings.
 */
export function BridgeVersionPanel({ bridge, version, isDraft }: BridgeVersionPanelProps) {
  const [editing, setEditing] = useState(false);

  return (
    <div data-slot="bridge-version-panel" className="space-y-4">
      <div className="flex flex-wrap items-start gap-x-8 gap-y-3 rounded-md border p-3 text-sm">
        <Endpoint
          label="Source version"
          dimensionId={bridge.sourceDimensionId}
          versionId={version.sourceDimensionVersionId}
        />
        <Endpoint
          label="Target version"
          dimensionId={bridge.targetDimensionId}
          versionId={version.targetDimensionVersionId}
        />
        <Setting label="Cardinality" value={version.cardinality} />
        <Setting label="Allocation mode" value={version.allocationMode} />
        {isDraft && (
          <Button variant="outline" size="sm" className="ml-auto" onClick={() => setEditing(true)}>
            <Pencil />
            Edit settings
          </Button>
        )}
      </div>
      <BridgeMappings version={version} isDraft={isDraft} />
      {editing && <BridgeVersionSheet bridge={bridge} version={version} onClose={() => setEditing(false)} />}
    </div>
  );
}

interface EndpointProps {
  label: string;
  dimensionId: number;
  /** The pinned version of the dimension. */
  versionId: number;
}

/** An endpoint version: its dimension's key and the version's label, linking to it, with its status. */
function Endpoint({ label, dimensionId, versionId }: EndpointProps) {
  const tenantId = useTenantId();
  const dimensions = useLookup('dimensions');
  const versions = useLookup('dimension-versions', dimensionId);
  const dimensionKey = dimensions.data?.find(dimension => dimension.id === dimensionId)?.key ?? '';
  const version = versions.data?.find(item => item.id === versionId);

  return (
    <div className="space-y-1">
      <div className="text-muted-foreground">{label}</div>
      <div className="flex items-center gap-2">
        <Link
          className="font-medium hover:underline"
          to={`/t/${tenantId}/dimensions/${dimensionId}?version=${versionId}`}
        >
          {dimensionKey} {version?.label}
        </Link>
        {version?.status && <StatusBadge status={version.status} />}
      </div>
    </div>
  );
}

function Setting({ label, value }: { label: string; value: string }) {
  return (
    <div className="space-y-1">
      <div className="text-muted-foreground">{label}</div>
      <div className="font-medium">{value}</div>
    </div>
  );
}
