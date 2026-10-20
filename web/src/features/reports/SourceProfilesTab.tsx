import type { ColumnDef } from '@tanstack/react-table';
import { EnabledBadge } from '@/shared/EnabledBadge';
import { VersionContentTab } from '@/shared/VersionContentTab';
import { SourceProfileSheet } from './SourceProfileSheet';
import type { ReportVersion, SourceProfile } from './types';

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

/** The version's source profiles: which source each variable reads when a request runs under the profile. */
export function SourceProfilesTab({ version, isEditable }: { version: ReportVersion; isEditable: boolean }) {
  return (
    <VersionContentTab
      kind="profiles"
      versionId={version.reportVersionId}
      isEditable={isEditable}
      columns={columns}
      noun="profile"
      identify={profile => ({ id: profile.profile.sourceProfileId, key: profile.profile.profileKey })}
      deleteDescription="Its picks are deleted too. The version's default profile is kept."
      renderSheet={(profile, onClose) => (
        <SourceProfileSheet version={version} profile={profile} readOnly={!isEditable} onClose={onClose} />
      )}
    />
  );
}
