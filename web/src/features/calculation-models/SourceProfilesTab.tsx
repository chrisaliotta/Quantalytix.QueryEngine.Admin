import type { ColumnDef } from '@tanstack/react-table';
import { EnabledBadge } from '@/shared/EnabledBadge';
import { ModelContentTab } from './ModelContentTab';
import { SourceProfileSheet } from './SourceProfileSheet';
import type { SourceProfile } from './types';

const columns: ColumnDef<SourceProfile>[] = [
  { id: 'profileKey', header: 'Key', accessorFn: profile => profile.profile.profileKey },
  { id: 'displayName', header: 'Name', accessorFn: profile => profile.profile.displayName },
  { id: 'picks', header: 'Picks', accessorFn: profile => profile.picks.length },
  {
    id: 'isEnabled',
    header: 'Enabled',
    accessorFn: profile => profile.profile.isEnabled,
    cell: ({ row }) => <EnabledBadge enabled={row.original.profile.isEnabled} />,
  },
];

/** The version's source profiles: which source each variable reads when a report runs under the profile. */
export function SourceProfilesTab({ versionId, isDraft }: { versionId: number; isDraft: boolean }) {
  return (
    <ModelContentTab
      kind="profiles"
      versionId={versionId}
      isDraft={isDraft}
      columns={columns}
      noun="profile"
      identify={profile => ({ id: profile.profile.sourceProfileId, key: profile.profile.profileKey })}
      deleteDescription="Its picks are deleted too. A profile that a report version runs under by default is kept."
      renderSheet={(profile, onClose) => (
        <SourceProfileSheet versionId={versionId} profile={profile} readOnly={!isDraft} onClose={onClose} />
      )}
    />
  );
}
