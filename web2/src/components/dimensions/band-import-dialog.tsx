import { useMemo, useState } from 'react'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Textarea } from '@/components/ui/textarea'
import { Segmented } from '@/components/report/shared/segmented'
import { useDimensionsApi } from '@/lib/dimensions/client'
import type { ImportMode, Member, RangeBand, RangeDataType } from '@/lib/dimensions/contract'
import { analyzeBands, bandPreview, parseBandImport } from '@/lib/dimensions/bands'
import { cn } from '@/lib/utils'
import { Mono, Td, Th, useDimCall } from './shared'

const PLACEHOLDER: Record<RangeDataType, string> = {
  decimal: 'member key,name,lower,upper,brackets\nBAL_LOW,Under 10k,,10000,[)\nBAL_MID,10k – 50k,10000,50000,[)\nBAL_HIGH,50k and over,50000,,[)',
  date: 'member key,name,lower,upper,brackets\nFY24,FY 2024,2024-01-01,2025-01-01,[)\nFY25,FY 2025,2025-01-01,2026-01-01,[)',
}

/** Paste bands as CSV/TSV. Rows reuse a member by key or create it when the key is new. */
export function BandImportDialog({
  open,
  onOpenChange,
  versionId,
  type,
  members,
  hasBands,
  onImport,
}: {
  open: boolean
  onOpenChange: (o: boolean) => void
  versionId: string
  type: RangeDataType
  members: Member[]
  hasBands: boolean
  onImport: (bands: RangeBand[], mode: ImportMode) => void
}) {
  const api = useDimensionsApi()
  const { run, saving, setProblem } = useDimCall()
  const [text, setText] = useState('')
  const [mode, setMode] = useState<ImportMode>('merge')
  const rows = useMemo(() => parseBandImport(text, type), [text, type])
  const byKey = useMemo(() => new Map(members.map((m) => [m.key, m])), [members])
  const bad = rows.some((r) => r.error)
  const asBands: RangeBand[] = rows.map((r, i) => ({ ordinal: i + 1, memberId: r.memberKey, lower: r.lower, upper: r.upper, lowerInclusive: r.lowerInclusive, upperInclusive: r.upperInclusive }))
  const issues = bad ? [] : analyzeBands(asBands, type)
  const toCreate = rows.filter((r, i) => !byKey.has(r.memberKey) && rows.findIndex((x) => x.memberKey === r.memberKey) === i)

  const close = (o: boolean) => {
    onOpenChange(o)
    if (!o) {
      setText('')
      setProblem(null)
    }
  }

  async function importBands() {
    const ids = new Map([...byKey].map(([k, m]) => [k, m.id]))
    if (toCreate.length) {
      const r = await run(() => api.importMembers(versionId, toCreate.map((x) => ({ key: x.memberKey, displayName: x.memberName, parentKey: null }))))
      if (!r.ok) return
      r.data.added.forEach((m) => ids.set(m.key, m.id))
    }
    onImport(asBands.map((b) => ({ ...b, memberId: ids.get(b.memberId) ?? '' })), hasBands ? mode : 'replace')
    close(false)
  }

  return (
    <Dialog open={open} onOpenChange={close}>
      <DialogContent className="sm:max-w-3xl">
        <DialogHeader>
          <DialogTitle>Import bands</DialogTitle>
          <DialogDescription>
            Paste CSV or TSV with columns: member key, name, lower, upper, brackets. Leave a bound empty for unbounded. Brackets default to <Mono>[)</Mono>; also <Mono>(]</Mono> <Mono>[]</Mono> <Mono>()</Mono>. New member keys are created. A header row is skipped.
          </DialogDescription>
        </DialogHeader>
        <Textarea
          rows={6}
          className="font-mono text-xs"
          value={text}
          onChange={(e) => {
            setText(e.target.value)
            setProblem(null)
          }}
          placeholder={PLACEHOLDER[type]}
          aria-label="Bands to import"
          spellCheck={false}
        />
        {rows.length > 0 && (
          <div className="max-h-64 overflow-auto rounded-md border">
            <table className="w-full text-sm">
              <thead className="sticky top-0 border-b bg-muted">
                <tr>
                  <Th>#</Th>
                  <Th>Member</Th>
                  <Th>Range</Th>
                  <Th>Check</Th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {rows.map((r, i) => {
                  const existing = byKey.get(r.memberKey)
                  const rowIssues = issues.filter((iss) => iss.ordinals.includes(i + 1) && iss.kind !== 'gap')
                  return (
                    <tr key={i} className={cn((r.error || rowIssues.length > 0) && 'bg-destructive/10')}>
                      <Td className="font-mono text-xs text-muted-foreground tabular-nums">{i + 1}</Td>
                      <Td>
                        <span className="flex items-center gap-2">
                          {existing?.displayName ?? r.memberName} <Mono>{r.memberKey || '—'}</Mono>
                        </span>
                      </Td>
                      <Td>
                        <code className="font-mono text-xs">{r.error ? '—' : bandPreview(asBands[i], type)}</code>
                      </Td>
                      <Td className={cn('text-xs', r.error || rowIssues.length ? 'text-destructive' : 'text-muted-foreground')}>
                        {r.error ?? (rowIssues.length ? rowIssues.map((x) => x.message).join(' ') : existing ? 'Existing member' : 'New member')}
                      </Td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        )}
        {issues.some((i) => i.kind === 'gap') && <p className="text-xs text-warning-foreground">Some values fall between bands. You can still import and close the gaps afterwards.</p>}
        <DialogFooter className="items-center sm:justify-between">
          {hasBands ? (
            <div className="flex flex-col gap-1">
              <Segmented<ImportMode>
                aria-label="Existing bands"
                value={mode}
                onChange={setMode}
                options={[
                  { value: 'merge', label: 'Merge' },
                  { value: 'replace', label: 'Replace all' },
                ]}
              />
              <p className="text-xs text-muted-foreground">
                {mode === 'merge' ? 'Rows update the band of the same member; new members are added. Bands are re-sorted by lower bound.' : 'Existing bands are removed and replaced by these rows.'}
              </p>
            </div>
          ) : (
            <span />
          )}
          <div className="flex gap-2">
            <Button variant="outline" onClick={() => close(false)}>
              Cancel
            </Button>
            <Button disabled={!rows.length || bad || saving} onClick={importBands}>
              {saving ? 'Importing…' : `Import ${rows.length || ''} band${rows.length === 1 ? '' : 's'}${toCreate.length ? ` · ${toCreate.length} new member${toCreate.length === 1 ? '' : 's'}` : ''}`}
            </Button>
          </div>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
