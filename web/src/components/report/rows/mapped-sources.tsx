import { ChevronRightIcon, PlusIcon } from 'lucide-react'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible'
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList, CommandSeparator } from '@/components/ui/command'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { sourcesOnRow } from '@/lib/report/coverage'
import { readSourcesOnRow, sourceReadBy } from '@/lib/report/profiles'
import { useEditor } from '@/lib/report/store'
import type { Source } from '@/lib/report/types'
import { cn } from '@/lib/utils'
import { KeyText, SectionLabel, WARN_CHIP } from '../shared/bits'
import { useActiveProfile } from '../shared/profile-switcher'
import { RowSourceCard } from './source-card'

export function MappedSources({ rowKey }: { rowKey: string }) {
  const { report } = useEditor()
  const { key: profileKey, dimming } = useActiveProfile()
  const { read, unread } = readSourcesOnRow(report, rowKey, profileKey)
  const all = [...read, ...unread]

  return (
    <section aria-label="Mapped sources" className="flex flex-col gap-3">
      {all.length === 0 ? (
        <div className={cn('rounded-lg border border-dashed px-3 py-4 text-pretty', WARN_CHIP)}>
          No sources are currently mapped to this item.
        </div>
      ) : !dimming ? (
        <div className="flex flex-col gap-2">
          {all.map((s) => (
            <RowSourceCard key={s.key} source={s} rowKey={rowKey} />
          ))}
        </div>
      ) : (
        <div className="flex flex-col gap-2">
          {read.length ? (
            read.map((s) => <RowSourceCard key={s.key} source={s} rowKey={rowKey} />)
          ) : (
            <p className={cn('rounded-lg border border-dashed px-3 py-3 text-pretty', WARN_CHIP)}>
              No sources are currently mapped to this item for this profile.
            </p>
          )}
        </div>
      )}
      <div>
        <MapSourcePicker rowKey={rowKey} />
      </div>
    </section>
  )
}

export function UnreadSources({ rowKey }: { rowKey: string }) {
  const { report } = useEditor()
  const { key: profileKey, dimming } = useActiveProfile()
  const { unread } = readSourcesOnRow(report, rowKey, profileKey)
  const [open, setOpen] = useState(false)
  if (!dimming || unread.length === 0) return null

  return (
    <section aria-labelledby="unread-sources" className="flex flex-col gap-3">
      <Collapsible open={open} onOpenChange={setOpen}>
        <CollapsibleTrigger
          render={
            <button
              type="button"
              className="flex items-center gap-1.5 rounded outline-none focus-visible:ring-2 focus-visible:ring-ring/60"
            />
          }
        >
          <ChevronRightIcon
            className={cn('size-3.5 text-muted-foreground transition-transform', open && 'rotate-90')}
            aria-hidden
          />
          <SectionLabel id="unread-sources">Not read by this profile</SectionLabel>
          <span className="rounded bg-muted px-1.5 text-xs tabular-nums text-muted-foreground">{unread.length}</span>
        </CollapsibleTrigger>
        <CollapsibleContent className="flex flex-col gap-2 pt-3">
          <p className="text-xs leading-relaxed text-muted-foreground text-pretty">
            Mapped here, but <KeyText className="text-foreground">{profileKey}</KeyText> doesn&apos;t read them. Variables they
            could supply:
          </p>
          <ul className="flex flex-col divide-y rounded-lg border bg-card">
            {unread.map((s) => (
              <li key={s.key} className="flex flex-col gap-1 px-3 py-2">
                <span className="truncate font-medium">{s.name}</span>
                <span className="flex flex-wrap gap-1">
                  {s.bindings.map((b) => (
                    <KeyText key={b.variable} className="rounded bg-muted px-1.5 py-0.5 text-xs">
                      {b.variable}
                    </KeyText>
                  ))}
                </span>
              </li>
            ))}
          </ul>
        </CollapsibleContent>
      </Collapsible>
    </section>
  )
}

function SourceOption({ source, onSelect }: { source: Source; onSelect: () => void }) {
  return (
    <CommandItem value={`${source.name} ${source.dataset}`} onSelect={onSelect}>
      <span className="flex min-w-0 flex-col">
        <span className="truncate">{source.name}</span>
        <KeyText>{source.dataset}</KeyText>
      </span>
    </CommandItem>
  )
}

function MapSourcePicker({ rowKey }: { rowKey: string }) {
  const { report, openDialog, setUi } = useEditor()
  const { key: profileKey, dimming } = useActiveProfile()
  const [open, setOpen] = useState(false)
  const onRow = new Set(sourcesOnRow(report, rowKey).map((s) => s.key))
  const candidates = report.sources.filter((s) => s.method === 'mapped' && !onRow.has(s.key))
  const isRead = (s: Source) => !dimming || sourceReadBy(report, profileKey, s.key)
  const readCandidates = candidates.filter(isRead)
  const otherCandidates = candidates.filter((s) => !isRead(s))
  const matched = report.sources.filter((s) => s.method === 'matched' && !onRow.has(s.key))
  const pick = (s: Source) => {
    setOpen(false)
    openDialog({ type: 'mapping', sourceKey: s.key, rowKey })
  }

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger render={<Button variant="outline" size="sm" />}>
        <PlusIcon data-icon="inline-start" />
        Map a source…
      </PopoverTrigger>
      <PopoverContent align="start" className="w-80 p-0">
        <Command>
          <CommandInput placeholder="Search sources…" />
          <CommandList>
            <CommandEmpty>Every source is already on this row.</CommandEmpty>
            {readCandidates.length > 0 && (
              <CommandGroup heading={dimming ? `Read by ${profileKey}` : 'Mapped sources'}>
                {readCandidates.map((s) => (
                  <SourceOption key={s.key} source={s} onSelect={() => pick(s)} />
                ))}
              </CommandGroup>
            )}
            {otherCandidates.length > 0 && (
              <>
                <CommandSeparator />
                <CommandGroup heading={`Other sources (not read by ${profileKey})`}>
                  {otherCandidates.map((s) => (
                    <SourceOption key={s.key} source={s} onSelect={() => pick(s)} />
                  ))}
                </CommandGroup>
              </>
            )}
            {matched.length > 0 && (
              <CommandGroup heading="Direct on a field">
                {matched.map((s) => (
                  <CommandItem key={s.key} value={`${s.name} ${s.dataset}`} disabled>
                    <span className="flex min-w-0 flex-col">
                      <span className="truncate">{s.name}</span>
                      <span className="text-xs text-muted-foreground">
                        Reaches rows whose key appears in <KeyText>{s.rowKeyField}</KeyText>
                      </span>
                    </span>
                  </CommandItem>
                ))}
              </CommandGroup>
            )}
            <CommandSeparator />
            <CommandGroup>
              <CommandItem
                value="new source"
                onSelect={() => {
                  setOpen(false)
                  setUi({ tab: 'data', addingSource: true })
                }}
              >
                <PlusIcon />
                New source…
              </CommandItem>
            </CommandGroup>
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  )
}
