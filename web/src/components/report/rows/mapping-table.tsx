import { ChevronRightIcon, FolderIcon, XIcon } from 'lucide-react'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import {
  isBrokenOutOnRow,
  isExcludedOnRow,
  leafAccounts,
  mappingCovers,
  mappingsOnRow,
  memberIndex,
  rowLeafFactors,
} from '@/lib/report/coverage'
import { useEditor } from '@/lib/report/store'
import type { DimMember, Mapping, Source } from '@/lib/report/types'
import { cn } from '@/lib/utils'
import { KeyText } from '../shared/bits'

const ROW = 'border-b last:border-b-0 hover:bg-muted/40'
const ACCOUNT_CELL = 'w-full max-w-0 py-1 pr-3'
const FACTOR_CELL = 'py-1 pr-1 text-right align-middle'
const COUNT_CELL = 'py-1 pr-3 text-right align-middle'
const ACTION_CELL = 'w-px py-1 pr-1 text-right align-middle whitespace-nowrap'

export function MappingTable({ source, rowKey, advanced }: { source: Source; rowKey: string; advanced: boolean }) {
  const { report } = useEditor()
  const [expanded, setExpanded] = useState<Set<string>>(() => new Set())
  const index = memberIndex(report.accounts)
  const onRow = mappingsOnRow(source, rowKey)
  const positive = onRow.filter((m) => m.factor > 0)
  const negative = onRow.filter((m) => m.factor <= 0)
  const coveredByFolder = (m: Mapping) =>
    positive.some((p) => p.memberKey !== m.memberKey && index.get(p.memberKey)?.isFolder && mappingCovers(p, m.memberKey, index))
  const standaloneExclusions = advanced ? negative : negative.filter((m) => !coveredByFolder(m))
  const brokenOutOf = (folderMapping: Mapping) =>
    positive.filter(
      (m) =>
        !index.get(m.memberKey)?.isFolder &&
        mappingCovers(folderMapping, m.memberKey, index) &&
        isBrokenOutOnRow(source, rowKey, m.memberKey),
    )
  const nestedIds = new Set(positive.filter((m) => index.get(m.memberKey)?.isFolder).flatMap((f) => brokenOutOf(f).map((m) => m.id)))

  if (!onRow.length) return <p className="text-muted-foreground">No accounts mapped yet.</p>

  const toggle = (id: string) =>
    setExpanded((prev) => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })

  return (
    <div className="overflow-hidden rounded-md border">
      <table className="w-full border-collapse">
        <caption className="sr-only">Accounts mapped to this row from {source.name}</caption>
        <thead className="bg-muted/50">
          <tr className="border-b text-xs text-muted-foreground">
            <th scope="col" className="py-1.5 pr-3 pl-2 text-left font-medium">
              Account
            </th>
            <th scope="col" className="w-20 py-1.5 pr-3 text-right font-medium">
              Items
            </th>
            <th scope="col" className="w-24 py-1.5 pr-2 text-right font-medium">
              Factor
            </th>
            <th scope="col" className="w-px">
              <span className="sr-only">Actions</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {positive.filter((m) => !nestedIds.has(m.id)).map((m) => {
            const info = index.get(m.memberKey)
            return info?.isFolder ? (
              <FolderRows
                key={m.id}
                source={source}
                rowKey={rowKey}
                mapping={m}
                folder={info.member}
                brokenOut={brokenOutOf(m)}
                labelOf={(key) => index.get(key)?.member.label ?? key}
                open={expanded.has(m.id)}
                onToggle={() => toggle(m.id)}
              />
            ) : (
              <AccountRow key={m.id} source={source} mapping={m} label={info?.member.label ?? m.memberKey} />
            )
          })}
          {standaloneExclusions.map((m) => (
            <AccountRow key={m.id} source={source} mapping={m} label={index.get(m.memberKey)?.member.label ?? m.memberKey} />
          ))}
        </tbody>
      </table>
    </div>
  )
}

function Factor({ value }: { value: number }) {
  return <span className="pr-1 font-mono text-xs tabular-nums">{value}</span>
}

function AccountName({ code, label, excluded, className }: { code: string; label: string; excluded?: boolean; className?: string }) {
  return (
    <div className={cn('flex min-w-0 items-center gap-2', className)}>
      <KeyText className={cn('shrink-0 text-foreground', excluded && 'text-muted-foreground line-through')}>{code}</KeyText>
      <span className={cn('truncate', excluded && 'text-muted-foreground line-through')}>{label}</span>
    </div>
  )
}

function AccountRow({
  source,
  mapping: m,
  label,
  brokenOutFrom,
}: {
  source: Source
  mapping: Mapping
  label: string
  brokenOutFrom?: string
}) {
  const { mapping } = useEditor()
  const excluded = m.factor <= 0
  return (
    <tr className={ROW}>
      <td className={cn(ACCOUNT_CELL, brokenOutFrom ? 'pl-4' : 'pl-8')}>
        {brokenOutFrom ? (
          <div className="ml-2.5 flex h-7 items-center gap-2 border-l border-primary pl-5">
            <AccountName code={m.memberKey} label={label} className="flex-1" />
            <span className="shrink-0 text-xs text-primary">Broken out</span>
          </div>
        ) : (
          <AccountName code={m.memberKey} label={label} excluded={excluded} />
        )}
      </td>
      <td className={COUNT_CELL} />
      <td className={FACTOR_CELL}>
        {excluded ? (
          <span className="pr-1 text-xs text-muted-foreground">Excluded</span>
        ) : (
          <Factor value={m.factor} />
        )}
      </td>
      <td className={ACTION_CELL}>
        <Button
          variant="ghost"
          size="icon-xs"
          aria-label={
            brokenOutFrom
              ? `Return ${m.memberKey} to ${brokenOutFrom}`
              : excluded
                ? `Remove exclusion of ${m.memberKey}`
                : `Remove ${m.memberKey} from this row`
          }
          title={brokenOutFrom ? `Return to ${brokenOutFrom}` : undefined}
          onClick={() => {
            mapping.remove(source.key, m.id)
            if (brokenOutFrom) mapping.include(source.key, m.rowKey, m.memberKey)
          }}
        >
          <XIcon />
        </Button>
      </td>
    </tr>
  )
}

function FolderRows({
  source,
  rowKey,
  mapping: m,
  folder,
  brokenOut,
  labelOf,
  open,
  onToggle,
}: {
  source: Source
  rowKey: string
  mapping: Mapping
  folder: DimMember
  brokenOut: Mapping[]
  labelOf: (key: string) => string
  open: boolean
  onToggle: () => void
}) {
  const { report, mapping } = useEditor()
  const members = leafAccounts(folder.children ?? [])
  const factors = rowLeafFactors(source, rowKey, report.accounts)
  const brokenOutKeys = new Set(brokenOut.map((b) => b.memberKey))
  const isExcluded = (leafKey: string) => isExcludedOnRow(source, rowKey, leafKey) || !factors.get(leafKey)
  const included = members.filter((leaf) => !isExcluded(leaf.key)).length
  const childrenId = `folder-${m.id}`

  return (
    <>
      <tr className={ROW}>
        <td className={cn(ACCOUNT_CELL, 'pl-1')}>
          <button
            type="button"
            onClick={onToggle}
            aria-expanded={open}
            aria-controls={childrenId}
            className="flex w-full min-w-0 items-center gap-1.5 rounded px-1 py-0.5 text-left outline-none hover:bg-muted focus-visible:ring-2 focus-visible:ring-ring/60"
          >
            <ChevronRightIcon className={cn('size-4 shrink-0 text-muted-foreground transition-transform', open && 'rotate-90')} aria-hidden />
            <FolderIcon className="size-4 shrink-0 text-muted-foreground" aria-hidden />
            <AccountName code={m.memberKey} label={folder.label} className="flex-1" />
          </button>
        </td>
        <td className={cn(COUNT_CELL, 'text-xs text-muted-foreground tabular-nums')}>
          {included !== members.length ? `${included} of ${members.length}` : members.length}
          <span className="sr-only"> accounts included</span>
        </td>
        <td className={FACTOR_CELL}>
          <Factor value={m.factor} />
        </td>
        <td className={ACTION_CELL}>
          <Button variant="ghost" size="icon-xs" aria-label={`Remove ${m.memberKey} folder from this row`} onClick={() => mapping.remove(source.key, m.id)}>
            <XIcon />
          </Button>
        </td>
      </tr>
      {brokenOut.map((b) => (
        <AccountRow key={b.id} source={source} mapping={b} label={labelOf(b.memberKey)} brokenOutFrom={m.memberKey} />
      ))}
      {open &&
        members.map((leaf, i) => {
          const isBrokenOut = brokenOutKeys.has(leaf.key)
          const excluded = !isBrokenOut && isExcluded(leaf.key)
          return (
            <tr key={leaf.key} id={i === 0 ? childrenId : undefined} className={cn(ROW, 'group')}>
              <td className={cn(ACCOUNT_CELL, 'pl-4')}>
                <div className="ml-2.5 flex h-7 items-center border-l pl-5">
                  <AccountName
                    code={leaf.key}
                    label={leaf.label}
                    excluded={excluded || isBrokenOut}
                    className={cn('flex-1', !excluded && !isBrokenOut && 'text-muted-foreground')}
                  />
                </div>
              </td>
              <td className={COUNT_CELL} />
              <td className={cn(FACTOR_CELL, 'pr-3 text-xs text-muted-foreground')}>
                {isBrokenOut ? 'Broken out' : excluded ? 'Excluded' : null}
              </td>
              <td className={ACTION_CELL}>
                {!isBrokenOut && (
                  <div className="flex justify-end gap-0.5 opacity-0 group-hover:opacity-100 focus-within:opacity-100">
                    <Button
                      variant="ghost"
                      size="xs"
                      onClick={() => (excluded ? mapping.include(source.key, rowKey, leaf.key) : mapping.exclude(source.key, rowKey, leaf.key))}
                    >
                      {excluded ? 'Include' : 'Exclude'}
                      <span className="sr-only"> {leaf.label}</span>
                    </Button>
                    <Button variant="ghost" size="xs" onClick={() => mapping.breakOut(source.key, rowKey, leaf.key)}>
                      Break out
                      <span className="sr-only"> {leaf.label} with its own factor</span>
                    </Button>
                  </div>
                )}
              </td>
            </tr>
          )
        })}
    </>
  )
}
