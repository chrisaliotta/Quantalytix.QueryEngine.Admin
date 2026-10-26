import { ArrowRightIcon } from 'lucide-react'
import { useMemo, useState } from 'react'
import { toast } from 'sonner'
import { useUndoAction } from '@/components/shell/history'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { autoPicks, getProfile, pickedSource, profileGaps, variableUsage } from '@/lib/report/profiles'
import { useEditor } from '@/lib/report/store'
import { findRow } from '@/lib/report/tree'
import type { ReportState } from '@/lib/report/types'
import { KeyText } from '../shared/bits'
import { SimpleSelect } from '../shared/simple-select'

type Close = { onClose: () => void }

const toKey = (name: string) =>
  name
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '_')
    .replace(/^_+|_+$/g, '')

export function SwitchPickDialog({ profileKey, variable, sourceKey, onClose }: Close & { profileKey: string; variable: string; sourceKey: string }) {
  const { report, setPick } = useEditor()
  const undoAction = useUndoAction()
  const profile = getProfile(report, profileKey)
  const from = pickedSource(report, profile, variable)
  const to = report.sources.find((s) => s.key === sourceKey)
  const usedBy = variableUsage(report).get(variable) ?? []

  const impact = useMemo(() => {
    const next: ReportState = structuredClone(report)
    const p = next.profiles.find((x) => x.key === profileKey)
    if (p) p.picks[variable] = sourceKey
    const id = (g: { rowKey: string; variable: string }) => `${g.rowKey}:${g.variable}`
    const before = profileGaps(report, profileKey).filter((g) => g.variable === variable)
    const after = profileGaps(next, profileKey).filter((g) => g.variable === variable)
    const beforeIds = new Set(before.map(id))
    const afterIds = new Set(after.map(id))
    const label = (k: string) => findRow(report.rows, k)?.label ?? k
    return {
      fixed: before.filter((g) => !afterIds.has(id(g))).map((g) => label(g.rowKey)),
      broken: after.filter((g) => !beforeIds.has(id(g))).map((g) => label(g.rowKey)),
    }
  }, [report, profileKey, variable, sourceKey])

  if (!profile || !to) return null

  return (
    <AlertDialog open onOpenChange={(open) => !open && onClose()}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>
            Read <span className="font-mono">{variable}</span> from {to.name}?
          </AlertDialogTitle>
          <AlertDialogDescription render={<div />}>
            <div className="flex flex-col gap-3">
              <p className="text-pretty">
                This changes <KeyText className="text-foreground">{profileKey}</KeyText> for the whole report, not just this row
                {usedBy.length ? ` — it feeds ${usedBy.join(', ')}` : ''}.
              </p>
              <div className="flex items-center gap-2 rounded-md border bg-muted/40 px-3 py-2 text-foreground">
                <span className={from ? '' : 'text-muted-foreground'}>{from?.name ?? 'Nothing picked'}</span>
                <ArrowRightIcon className="size-3.5 text-muted-foreground" aria-hidden />
                <span className="font-medium">{to.name}</span>
              </div>
              {impact.fixed.length > 0 && (
                <p className="text-pretty">
                  Fills the gap on <span className="text-foreground">{impact.fixed.join(', ')}</span>.
                </p>
              )}
              {impact.broken.length > 0 && (
                <p className="text-pretty text-warning-foreground">
                  Opens a gap on {impact.broken.join(', ')}, where {to.name} isn&apos;t mapped.
                </p>
              )}
              {!impact.fixed.length && !impact.broken.length && <p>No row gains or loses a gap.</p>}
            </div>
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>Cancel</AlertDialogCancel>
          <AlertDialogAction
            onClick={() => {
              setPick(profileKey, variable, sourceKey)
              toast(`${profileKey} now reads ${variable} from ${to.name}`, { action: undoAction() })
              onClose()
            }}
          >
            Switch source
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}

const AUTO = '__auto'

export function NewProfileDialog({ onClose }: Close) {
  const { report, mutate, setUi } = useEditor()
  const [name, setName] = useState('')
  const [base, setBase] = useState<string>(report.settings.defaultProfile)
  const key = toKey(name)
  const taken = report.profiles.some((p) => p.key === key)
  const invalid = !key || taken

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <form
          className="flex flex-col gap-4"
          onSubmit={(e) => {
            e.preventDefault()
            if (invalid) return
            const picks = base === AUTO ? autoPicks(report) : { ...(getProfile(report, base)?.picks ?? {}) }
            mutate((d) => {
              d.profiles.push({ key, name: name.trim(), enabled: true, picks })
            })
            setUi({ activeProfile: key, allProfiles: false, view: 'settings', settingsSection: 'profiles', selectedProfile: key })
            toast(`Profile ${key} created`)
            onClose()
          }}
        >
          <DialogHeader>
            <DialogTitle>New profile</DialogTitle>
            <DialogDescription>A profile picks one source for each variable that more than one source supplies.</DialogDescription>
          </DialogHeader>
          <div className="flex flex-col gap-2">
            <Label htmlFor="profile-name">Name</Label>
            <Input id="profile-name" autoFocus value={name} onChange={(e) => setName(e.target.value)} placeholder="GL weekly" />
            <p className="text-xs text-muted-foreground">
              {taken ? (
                <span className="text-destructive">A profile with key {key} already exists.</span>
              ) : (
                <>
                  Key <KeyText>{key || '—'}</KeyText>
                </>
              )}
            </p>
          </div>
          <div className="flex flex-col gap-2">
            <Label>Start from</Label>
            <SimpleSelect
              aria-label="Start from"
              value={base}
              onChange={setBase}
              options={[
                ...report.profiles.map((p) => ({ value: p.key, label: `Copy of ${p.name}` })),
                { value: AUTO, label: 'Only obvious picks' },
              ]}
            />
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" disabled={invalid}>
              Create profile
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

export function RenameProfileDialog({ profileKey, onClose }: Close & { profileKey: string }) {
  const { report, mutate } = useEditor()
  const profile = getProfile(report, profileKey)
  const [name, setName] = useState(profile?.name ?? '')
  if (!profile) return null
  const invalid = !name.trim()

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <form
          className="flex flex-col gap-4"
          onSubmit={(e) => {
            e.preventDefault()
            if (invalid) return
            mutate((d) => {
              const p = d.profiles.find((x) => x.key === profileKey)
              if (p) p.name = name.trim()
            })
            onClose()
          }}
        >
          <DialogHeader>
            <DialogTitle>Rename profile</DialogTitle>
            <DialogDescription>
              The key <KeyText>{profileKey}</KeyText> stays the same, so anything that runs this report by key keeps working.
            </DialogDescription>
          </DialogHeader>
          <div className="flex flex-col gap-2">
            <Label htmlFor="profile-rename">Name</Label>
            <Input id="profile-rename" autoFocus value={name} onChange={(e) => setName(e.target.value)} />
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" disabled={invalid}>
              Save
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

export function DefaultProfileDialog({ profileKey, onClose }: Close & { profileKey: string }) {
  const { report, mutate } = useEditor()
  const undoAction = useUndoAction()
  const current = report.settings.defaultProfile
  return (
    <AlertDialog open onOpenChange={(open) => !open && onClose()}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>
            Make <span className="font-mono">{profileKey}</span> the default?
          </AlertDialogTitle>
          <AlertDialogDescription>
            The report runs as the default when nobody picks a profile. It currently runs as {current}.
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>Cancel</AlertDialogCancel>
          <AlertDialogAction
            onClick={() => {
              mutate((d) => {
                d.settings.defaultProfile = profileKey
                const p = d.profiles.find((x) => x.key === profileKey)
                if (p) p.enabled = true
              })
              toast(`${profileKey} is now the default`, { action: undoAction() })
              onClose()
            }}
          >
            Make default
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}

export function DeleteProfileDialog({ profileKey, onClose }: Close & { profileKey: string }) {
  const { report, ui, setReport, setUi } = useEditor()
  const undoAction = useUndoAction()
  const profile = getProfile(report, profileKey)
  if (!profile) return null
  const isDefault = report.settings.defaultProfile === profileKey
  const picks = Object.keys(profile.picks).length

  return (
    <AlertDialog open onOpenChange={(open) => !open && onClose()}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>
            {isDefault ? "Can't delete the default profile" : `Delete ${profile.name}?`}
          </AlertDialogTitle>
          <AlertDialogDescription>
            {isDefault
              ? 'Make another profile the default first, then delete this one.'
              : `Its ${picks} ${picks === 1 ? 'pick is' : 'picks are'} removed. Sources and mappings are not touched.`}
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>{isDefault ? 'Close' : 'Cancel'}</AlertDialogCancel>
          {!isDefault && (
            <AlertDialogAction
              variant="destructive"
              onClick={() => {
                const fallback = report.settings.defaultProfile
                setReport({ ...report, profiles: report.profiles.filter((p) => p.key !== profileKey) })
                setUi({
                  activeProfile: ui.activeProfile === profileKey ? fallback : ui.activeProfile,
                  selectedProfile: ui.selectedProfile === profileKey ? fallback : ui.selectedProfile,
                  compareProfile: ui.compareProfile === profileKey ? null : ui.compareProfile,
                  readinessProfile: ui.readinessProfile === profileKey ? null : ui.readinessProfile,
                })
                toast(`Profile ${profileKey} deleted`, { action: undoAction() })
                onClose()
              }}
            >
              Delete profile
            </AlertDialogAction>
          )}
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}
