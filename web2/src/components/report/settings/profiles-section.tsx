import { PlusIcon, Trash2Icon } from 'lucide-react'
import { useState } from 'react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Switch } from '@/components/ui/switch'
import { KEY_PATTERN } from '@/lib/api/contract'
import type { ReportProfile, Violation } from '@/lib/reports/contract'
import { useNames, useReportEditor } from '../editor-state'
import { KeyText, Refusal } from '../shared/bits'
import { SimpleSelect } from '../shared/simple-select'

const NONE = '__none__'

/**
 * The profiles: for each variable a source supplies, the source a run under the profile reads it from (P8). The server resolves
 * each pick (OI-05); the default profile cannot be deleted.
 */
export function ProfilesSection() {
  const { reportId, report, diagnostics, run } = useReportEditor()
  const names = useNames()
  const [refusal, setRefusal] = useState<Violation[] | null>(null)
  const [newKey, setNewKey] = useState('')
  const suppliers = (variableKey: string) => report.sources.filter((s) => s.variables.some((v) => v.variableKey === variableKey))
  const variables = [...new Set(report.sources.flatMap((s) => s.variables.map((v) => v.variableKey)))].sort()
  const edit = async (call: Parameters<typeof run>[0]) => setRefusal(await run(call))
  const save = (profile: ReportProfile, originalKey: string) => edit((api) => api.saveProfile(reportId, { ...profile, originalKey }))

  const add = async () => {
    const picks = Object.fromEntries(variables.flatMap((v) => (suppliers(v).length === 1 ? [[v, suppliers(v)[0].key]] : [])))
    await edit((api) => api.saveProfile(reportId, { key: newKey, displayName: newKey, isEnabled: true, picks }))
    setNewKey('')
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="overflow-x-auto rounded-lg border bg-card">
        <table className="w-full text-sm">
          <thead className="border-b text-xs text-muted-foreground">
            <tr className="[&>th]:h-9 [&>th]:px-3 [&>th]:text-left [&>th]:font-medium">
              <th>Profile</th>
              <th>Enabled</th>
              {variables.map((v) => (
                <th key={v}>{names.variable(v)?.displayName ?? v}</th>
              ))}
              <th>
                <span className="sr-only">Actions</span>
              </th>
            </tr>
          </thead>
          <tbody className="divide-y">
            {report.profiles.map((profile) => {
              const isDefault = report.settings.defaultProfileKey === profile.key
              return (
                <tr key={profile.key} className="[&>td]:px-3 [&>td]:py-2">
                  <td>
                    <div className="flex items-center gap-2">
                      <Input
                        aria-label={`Name of ${profile.key}`}
                        className="h-7 w-36"
                        defaultValue={profile.displayName}
                        onBlur={(e) => e.target.value !== profile.displayName && void save({ ...profile, displayName: e.target.value }, profile.key)}
                      />
                      <KeyText>{profile.key}</KeyText>
                      {isDefault && <Badge variant="outline">Default</Badge>}
                    </div>
                  </td>
                  <td>
                    <Switch aria-label={`${profile.key} enabled`} checked={profile.isEnabled} onCheckedChange={(isEnabled) => void save({ ...profile, isEnabled }, profile.key)} />
                  </td>
                  {variables.map((v) => {
                    const resolution = diagnostics?.resolution[profile.key]?.[v]
                    return (
                      <td key={v}>
                        <div className="flex flex-col gap-1">
                          <SimpleSelect
                            aria-label={`${profile.key} source for ${v}`}
                            size="sm"
                            value={profile.picks[v] ?? NONE}
                            onChange={(sourceKey) => {
                              const { [v]: _, ...rest } = profile.picks
                              void save({ ...profile, picks: sourceKey === NONE ? rest : { ...rest, [v]: sourceKey } }, profile.key)
                            }}
                            options={[{ value: NONE, label: 'No source' }, ...suppliers(v).map((s) => ({ value: s.key, label: s.displayName }))]}
                            className="w-40"
                          />
                          {resolution && resolution.status !== 'ok' && resolution.status !== 'na' && (
                            <span className="text-xs text-muted-foreground">
                              {resolution.status === 'gap' ? `Missing on ${resolution.rowGaps.join(', ')}` : resolution.status}
                            </span>
                          )}
                        </div>
                      </td>
                    )
                  })}
                  <td>
                    <Button size="icon-sm" variant="ghost" aria-label={`Delete ${profile.key}`} disabled={isDefault} onClick={() => void edit((api) => api.deleteProfile(reportId, profile.key))}>
                      <Trash2Icon />
                    </Button>
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>
      <Refusal violations={refusal} />
      <form
        className="flex items-center gap-2"
        onSubmit={(e) => {
          e.preventDefault()
          void add()
        }}
      >
        <Input aria-label="New profile key" className="w-48 font-mono" placeholder="new_profile" value={newKey} onChange={(e) => setNewKey(e.target.value.trim())} />
        <Button type="submit" variant="outline" disabled={!KEY_PATTERN.test(newKey)}>
          <PlusIcon /> Add profile
        </Button>
      </form>
    </div>
  )
}
