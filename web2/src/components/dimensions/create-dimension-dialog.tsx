import { ArrowLeftIcon } from 'lucide-react'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Switch } from '@/components/ui/switch'
import { Segmented } from '@/components/report/shared/segmented'
import { useDimensionList, useDimensionsApi } from '@/lib/dimensions/client'
import { KEY_HELP, KEY_PATTERN, KIND_LABEL, RANGE_TYPE_LABEL, type DimensionKind, type RangeDataType } from '@/lib/dimensions/contract'
import { cn } from '@/lib/utils'
import { useAppNav } from '@/lib/navigation'
import { Field, KIND_ICON, ProblemBanner, useDimCall } from './shared'

const KIND_COPY: Record<DimensionKind, string> = {
  categorical: 'A flat list of named members, grouped by key.',
  hierarchical: 'A tree of members. Queries group at any level and roll up.',
  range: 'Buckets a numeric or date value into ordered bands.',
  time: 'A time axis. Periods come from the data; members only for custom calendars.',
}

const lowerFirst = (s: string) => s.charAt(0).toLowerCase() + s.slice(1)

export function CreateDimensionDialog({ open, onOpenChange }: { open: boolean; onOpenChange: (o: boolean) => void }) {
  const nav = useAppNav()
  const api = useDimensionsApi()
  const { data: userDims } = useDimensionList('user')
  const { data: systemDims } = useDimensionList('system')
  const { run, saving, problem, setProblem, errorsFor } = useDimCall()

  const [step, setStep] = useState<1 | 2>(1)
  const [kind, setKind] = useState<DimensionKind>('categorical')
  const [rangeType, setRangeType] = useState<RangeDataType>('decimal')
  const [key, setKey] = useState('')
  const [name, setName] = useState('')
  const [roleKey, setRoleKey] = useState<string | null>(null)
  const [roleName, setRoleName] = useState<string | null>(null)
  const [enabled, setEnabled] = useState(true)
  const [removable, setRemovable] = useState(true)
  const [touched, setTouched] = useState(false)

  const effectiveRoleKey = roleKey ?? lowerFirst(key.trim())
  const effectiveRoleName = roleName ?? name
  const all = [...(userDims ?? []), ...(systemDims ?? [])]
  const keyErrors = !touched
    ? undefined
    : !key.trim()
      ? ['Key is required.']
      : !KEY_PATTERN.test(key.trim())
        ? ['Key must start with a letter and contain only letters, digits or underscores.']
        : all.some((d) => d.key.toLowerCase() === key.trim().toLowerCase())
          ? [`A dimension with key '${key.trim()}' already exists.`]
          : undefined
  const roleErrors =
    touched && kind !== 'time' && effectiveRoleKey && all.some((d) => d.roles.some((r) => r.key.toLowerCase() === effectiveRoleKey.toLowerCase()))
      ? [`Role key '${effectiveRoleKey}' is already used by another dimension.`]
      : undefined

  function reset() {
    setStep(1)
    setKind('categorical')
    setRangeType('decimal')
    setKey('')
    setName('')
    setRoleKey(null)
    setRoleName(null)
    setEnabled(true)
    setRemovable(true)
    setTouched(false)
    setProblem(null)
  }

  async function create() {
    setTouched(true)
    if (keyErrors || roleErrors || !key.trim() || !KEY_PATTERN.test(key.trim())) return
    const r = await run(
      () =>
        api.createDimension({
          key: key.trim(),
          displayName: name.trim() || key.trim(),
          kind,
          rangeDataType: kind === 'range' ? rangeType : null,
          firstRole: kind === 'time' ? null : { key: effectiveRoleKey, displayName: effectiveRoleName.trim() || effectiveRoleKey },
          isEnabled: enabled,
          isRemovable: removable,
        }),
      `Created ${name.trim() || key.trim()}`,
    )
    if (r.ok) {
      onOpenChange(false)
      reset()
      nav.dimension(r.data.dimensionId, { version: r.data.versionId, tab: 'members' })
    }
  }

  return (
    <Dialog
      open={open}
      onOpenChange={(o) => {
        onOpenChange(o)
        if (!o) reset()
      }}
    >
      <DialogContent className="sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>New dimension</DialogTitle>
          <DialogDescription>{step === 1 ? 'Step 1 of 2 — Choose a kind.' : 'Step 2 of 2 — Details.'}</DialogDescription>
        </DialogHeader>

        {step === 1 ? (
          <div className="flex flex-col gap-4">
            <div role="radiogroup" aria-label="Dimension kind" className="grid gap-3 sm:grid-cols-2">
              {(Object.keys(KIND_LABEL) as DimensionKind[]).map((k) => {
                const Icon = KIND_ICON[k]
                const active = kind === k
                return (
                  <button
                    key={k}
                    type="button"
                    role="radio"
                    aria-checked={active}
                    onClick={() => setKind(k)}
                    className={cn(
                      'flex flex-col gap-2 rounded-lg border p-4 text-left transition-colors',
                      active ? 'border-primary bg-primary/5 ring-1 ring-primary' : 'hover:border-foreground/20 hover:bg-muted/40',
                    )}
                  >
                    <span className="flex items-center gap-2 font-medium">
                      <Icon aria-hidden className={cn('size-4', active ? 'text-primary' : 'text-muted-foreground')} />
                      {KIND_LABEL[k]}
                    </span>
                    <span className="text-sm text-pretty text-muted-foreground">{KIND_COPY[k]}</span>
                    {k === 'range' && active && (
                      <span className="mt-1" onClick={(e) => e.stopPropagation()}>
                        <Segmented<RangeDataType>
                          aria-label="Range data type"
                          value={rangeType}
                          onChange={setRangeType}
                          options={(Object.keys(RANGE_TYPE_LABEL) as RangeDataType[]).map((t) => ({ value: t, label: RANGE_TYPE_LABEL[t] }))}
                        />
                      </span>
                    )}
                  </button>
                )
              })}
            </div>
            <p className="text-xs text-pretty text-muted-foreground">
              Grouping by a raw or calculated field value does not need a dimension. Define a groupable field on the dataset instead —{' '}
              <a href="#datasets-fields" className="underline underline-offset-2 hover:text-foreground">
                Datasets › Fields
              </a>
              .
            </p>
          </div>
        ) : (
          <form
            className="flex flex-col gap-4"
            onSubmit={(e) => {
              e.preventDefault()
              create()
            }}
          >
            <ProblemBanner problem={problem && Object.keys(problem.errors).length ? null : problem} />
            <div className="grid gap-4 sm:grid-cols-2">
              <Field label="Key" htmlFor="dim-key" hint={KEY_HELP} errors={keyErrors ?? errorsFor('key')} required>
                <Input id="dim-key" value={key} onChange={(e) => setKey(e.target.value)} onBlur={() => key && setTouched(true)} className="font-mono" autoFocus placeholder="RiskGrade" />
              </Field>
              <Field label="Display name" htmlFor="dim-name">
                <Input id="dim-name" value={name} onChange={(e) => setName(e.target.value)} placeholder="Risk grade" />
              </Field>
            </div>
            {kind === 'range' && (
              <Field label="Range data type">
                <Segmented<RangeDataType>
                  aria-label="Range data type"
                  value={rangeType}
                  onChange={setRangeType}
                  options={(Object.keys(RANGE_TYPE_LABEL) as RangeDataType[]).map((t) => ({ value: t, label: RANGE_TYPE_LABEL[t] }))}
                />
              </Field>
            )}
            {kind !== 'time' && (
              <fieldset className="flex flex-col gap-3 rounded-lg border p-3">
                <legend className="px-1 text-sm font-medium">First role</legend>
                <p className="text-xs text-muted-foreground">Datasets bind fields to a role. It follows the dimension key and name until you edit it.</p>
                <div className="grid gap-4 sm:grid-cols-2">
                  <Field label="Role key" htmlFor="role-key" errors={roleErrors ?? errorsFor('firstRole.key')}>
                    <Input id="role-key" value={effectiveRoleKey} onChange={(e) => setRoleKey(e.target.value)} className="font-mono" />
                  </Field>
                  <Field label="Role name" htmlFor="role-name">
                    <Input id="role-name" value={effectiveRoleName} onChange={(e) => setRoleName(e.target.value)} />
                  </Field>
                </div>
              </fieldset>
            )}
            <div className="flex flex-wrap gap-6">
              <label className="flex items-center gap-2 text-sm">
                <Switch checked={enabled} onCheckedChange={setEnabled} /> Enabled
              </label>
              <label className="flex items-center gap-2 text-sm">
                <Switch checked={removable} onCheckedChange={setRemovable} /> Removable
              </label>
            </div>
            <p className="rounded-md bg-muted/60 px-3 py-2 text-sm">
              Creates <span className="font-medium">{name.trim() || key.trim() || 'the dimension'}</span> ({KIND_LABEL[kind].toLowerCase()}
              {kind === 'range' ? `, ${RANGE_TYPE_LABEL[rangeType].toLowerCase()}` : ''}). Version 1 is live and editable straight away.
            </p>
            <button type="submit" hidden />
          </form>
        )}

        <DialogFooter className="sm:justify-between">
          {step === 2 ? (
            <Button variant="ghost" onClick={() => setStep(1)}>
              <ArrowLeftIcon /> Back
            </Button>
          ) : (
            <span />
          )}
          <div className="flex gap-2">
            <Button variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            {step === 1 ? (
              <Button onClick={() => setStep(2)}>Continue</Button>
            ) : (
              <Button onClick={create} disabled={saving}>
                {saving ? 'Creating…' : 'Create'}
              </Button>
            )}
          </div>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
