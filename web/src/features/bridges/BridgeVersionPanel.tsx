import { Pencil } from 'lucide-react';
import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { BridgeMappings } from './BridgeMappings';
import { BridgeVersionSheet } from './BridgeVersionSheet';
import type { Bridge, BridgeVersion } from './types';

export interface BridgeVersionPanelProps {
  bridge: Bridge;
  version: BridgeVersion;
  isEditable: boolean;
}

/**
 * The selected bridge version: its allocation semantics, editable on a draft or the published version, above its
 * mappings.
 */
export function BridgeVersionPanel({ bridge, version, isEditable }: BridgeVersionPanelProps) {
  const [editing, setEditing] = useState(false);

  return (
    <div data-slot="bridge-version-panel" className="space-y-4">
      <div className="flex flex-wrap items-start gap-x-8 gap-y-3 rounded-md border p-3 text-sm">
        <Setting label="Cardinality" value={version.cardinality} />
        <Setting label="Allocation mode" value={version.allocationMode} />
        {isEditable && (
          <Button variant="outline" size="sm" className="ml-auto" onClick={() => setEditing(true)}>
            <Pencil />
            Edit settings
          </Button>
        )}
      </div>
      <BridgeMappings bridge={bridge} version={version} isEditable={isEditable} />
      {editing && <BridgeVersionSheet version={version} onClose={() => setEditing(false)} />}
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
