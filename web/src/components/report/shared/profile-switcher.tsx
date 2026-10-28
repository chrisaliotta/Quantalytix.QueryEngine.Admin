import { Select, SelectContent, SelectItem, SelectSeparator, SelectTrigger, SelectValue } from '@/components/ui/select'
import { getProfile } from '@/lib/report/profiles'
import { useEditor } from '@/lib/report/store'
import { KeyText } from './bits'

const ALL = '__all__'
const MANAGE = '__manage__'

/** The profile the editor is viewed as, plus whether profile dimming is on. */
export function useActiveProfile() {
  const { report, ui } = useEditor()
  const key = getProfile(report, ui.activeProfile) ? ui.activeProfile : report.settings.defaultProfile
  return { key, profile: getProfile(report, key), dimming: !(ui.advanced && ui.allProfiles) }
}

export function ProfileSwitcher() {
  const { report, ui, setUi } = useEditor()
  const { key, dimming } = useActiveProfile()
  const value = dimming ? key : ALL
  if (report.profiles.length <= 1) return null
  const items = [
    ...report.profiles.map((p) => ({
      value: p.key,
      label: p.key === report.settings.defaultProfile ? `${p.key} (default)` : p.key,
    })),
    { value: ALL, label: 'All profiles' },
    { value: MANAGE, label: 'Manage profiles…' },
  ]

  return (
    <Select
      value={value}
      items={items}
      onValueChange={(v) => {
        if (v === MANAGE) setUi({ view: 'settings', settingsSection: 'profiles', selectedProfile: key })
        else if (v === ALL) setUi({ allProfiles: true })
        else if (v) setUi({ activeProfile: String(v), allProfiles: false })
      }}
    >
      <SelectTrigger size="sm" aria-label="Editing as profile" className="min-w-48 font-mono text-xs">
        <span className="font-sans text-muted-foreground">Editing as</span>
        <SelectValue />
      </SelectTrigger>
      <SelectContent className="min-w-64">
        {report.profiles.map((p) => (
          <SelectItem key={p.key} value={p.key}>
            <span className="flex min-w-0 flex-col">
              <span className="font-mono text-xs">{p.key}</span>
              <span className="text-xs text-muted-foreground">
                {p.name}
                {p.key === report.settings.defaultProfile && ' · default'}
                {!p.enabled && ' · disabled'}
              </span>
            </span>
          </SelectItem>
        ))}
        {ui.advanced && (
          <>
            <SelectSeparator />
            <SelectItem value={ALL}>All profiles (no dimming)</SelectItem>
          </>
        )}
        <SelectSeparator />
        <SelectItem value={MANAGE}>Manage profiles…</SelectItem>
      </SelectContent>
    </Select>
  )
}

export function ProfileHint() {
  const { key, dimming } = useActiveProfile()
  return (
    <p className="text-xs text-muted-foreground text-pretty">
      {dimming ? (
        <>
          Showing what the report reads under <KeyText className="text-foreground">{key}</KeyText>. Mappings for other
          profiles are kept but dimmed.
        </>
      ) : (
        'Showing every mapping across all profiles, without dimming.'
      )}
    </p>
  )
}

/** Inline "read by gl_monthly / not read by gl_monthly" note. */
export function ReadNote({ read, profileKey }: { read: boolean; profileKey: string }) {
  return (
    <span className="text-xs text-muted-foreground">
      {read ? 'Read by ' : 'Not read by '}
      <KeyText className={read ? 'text-foreground' : undefined}>{profileKey}</KeyText>
    </span>
  )
}
