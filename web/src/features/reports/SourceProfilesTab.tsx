import type { ColumnDef } from '@tanstack/react-table';
import { EnabledBadge } from '@/shared/EnabledBadge';
import { OwnedContentTab } from '@/shared/OwnedContentTab';
import { SourceProfileSheet } from './SourceProfileSheet';
import type { EditorView, SourceProfile } from './types';

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

/** The report's source profiles: which source each variable reads when a request runs under the profile. */
export function SourceProfilesTab({ view }: { view: EditorView }) {
  return (
    <OwnedContentTab
      kind="profiles"
      ownerId={view.report.reportId}
      columns={columns}
      noun="profile"
      identify={profile => ({ id: profile.profile.calculationSourceProfileId, key: profile.profile.profileKey })}
      deleteDescription="Its picks are deleted too. The report's default profile is kept."
      renderSheet={(profile, onClose) => <SourceProfileSheet view={view} profile={profile} onClose={onClose} />}
    />
  );
}
