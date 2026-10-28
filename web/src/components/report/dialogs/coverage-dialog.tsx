import { toast } from 'sonner'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { ScrollArea } from '@/components/ui/scroll-area'
import { lineKeys, rowLabel, sourceCoverage } from '@/lib/report/coverage'
import { useEditor } from '@/lib/report/store'
import { findRow } from '@/lib/report/tree'
import { cn } from '@/lib/utils'
import { KeyText, SectionLabel, WARN_CHIP, factorLabel } from '../shared/bits'
import { SearchCombobox } from '../shared/search-combobox'

export function CoverageDialog({ sourceKey, onClose }: { sourceKey: string; onClose: () => void }) {
  const { report, ui, mapping, mutate } = useEditor()
  const source = report.sources.find((s) => s.key === sourceKey)
  if (!source) return null

  const coverage = sourceCoverage(source, report)
  const selected = ui.selectedRowKey ? findRow(report.rows, ui.selectedRowKey) : null
  const selectedIsLine = selected?.kind === 'line'
  const lineOptions = lineKeys(report).map((k) => ({ value: k, label: rowLabel(report, k), hint: k }))

  const mapTo = (memberKey: string, rowKey: string) => {
    mapping.add(source.key, rowKey, memberKey)
    toast(`Mapped ${memberKey} to ${rowKey}`)
  }

  const setLeftOut = (memberKey: string, leftOut: boolean) =>
    mutate((d) => {
      const s = d.sources.find((x) => x.key === sourceKey)
      if (!s) return
      s.leftOut = leftOut ? [...s.leftOut, memberKey] : s.leftOut.filter((k) => k !== memberKey)
    })

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="flex max-h-[min(40rem,calc(100dvh-2rem))] flex-col gap-0 p-0 sm:max-w-2xl">
        <DialogHeader className="gap-1 border-b px-5 pt-4 pb-3">
          <DialogTitle className="text-base">Account coverage - {source.name}</DialogTitle>
          <DialogDescription>Every account should land on a row exactly once. Accounts left out on purpose are not counted as gaps.</DialogDescription>
        </DialogHeader>

        <ScrollArea className="min-h-0 flex-1">
          <div className="flex flex-col gap-5 px-5 py-4">
            <section aria-labelledby="cov-unmapped" className="flex flex-col gap-2">
              <div className="flex items-center gap-2">
                <SectionLabel id="cov-unmapped">Not mapped to any row</SectionLabel>
                <Badge variant="outline" className={cn('tabular-nums', coverage.unmapped.length > 0 && WARN_CHIP)}>
                  {coverage.unmapped.length}
                </Badge>
              </div>
              {coverage.unmapped.length === 0 ? (
                <p className="text-muted-foreground">Every account lands on a row.</p>
              ) : (
                <ul className="flex flex-col divide-y rounded-lg border">
                  {coverage.unmapped.map((a) => (
                    <li key={a.key} className="flex flex-wrap items-center gap-2 px-3 py-2">
                      <KeyText className="text-foreground">{a.key}</KeyText>
                      <span className="min-w-0 flex-1 truncate">{a.label}</span>
                      {selectedIsLine && selected && (
                        <Button size="xs" variant="secondary" onClick={() => mapTo(a.key, selected.key)}>
                          Map to <span className="font-mono">{selected.key}</span>
                        </Button>
                      )}
                      <SearchCombobox
                        value=""
                        onChange={(rowKey) => mapTo(a.key, rowKey)}
                        options={lineOptions}
                        placeholder="Map to…"
                        searchPlaceholder="Find a line"
                        className="h-7 w-32"
                      />
                      <Button size="xs" variant="ghost" onClick={() => setLeftOut(a.key, true)}>
                        Leave out
                      </Button>
                    </li>
                  ))}
                </ul>
              )}
            </section>

            <section aria-labelledby="cov-multi" className="flex flex-col gap-2">
              <div className="flex items-center gap-2">
                <SectionLabel id="cov-multi">Counted more than once</SectionLabel>
                <Badge variant="outline" className={cn('tabular-nums', coverage.multi.length > 0 && WARN_CHIP)}>
                  {coverage.multi.length}
                </Badge>
              </div>
              {coverage.multi.length === 0 ? (
                <p className="text-muted-foreground">No account adds up to anything other than x1.</p>
              ) : (
                <ul className="flex flex-col divide-y rounded-lg border">
                  {coverage.multi.map((e) => (
                    <li key={e.key} className="flex flex-col gap-1.5 px-3 py-2">
                      <div className="flex items-center gap-2">
                        <KeyText className="text-foreground">{e.key}</KeyText>
                        <span className="min-w-0 flex-1 truncate">{e.label}</span>
                        <Badge variant="outline" className={WARN_CHIP}>
                          net {factorLabel(e.net)}, {e.note}
                        </Badge>
                      </div>
                      <ul className="flex flex-wrap gap-1.5" aria-label={`Mappings of ${e.key}`}>
                        {e.parts.map((p, i) => (
                          <li key={`${p.rowKey}-${i}`} className="rounded border bg-muted/50 px-1.5 py-0.5 text-xs">
                            <span className="font-mono">{p.rowKey}</span> <span className="text-muted-foreground">{p.factor < 0 ? 'excluded (x-1)' : factorLabel(p.factor)}</span>
                          </li>
                        ))}
                      </ul>
                    </li>
                  ))}
                </ul>
              )}
            </section>

            {coverage.leftOut.length > 0 && (
              <section aria-labelledby="cov-left" className="flex flex-col gap-2">
                <SectionLabel id="cov-left">Left out on purpose</SectionLabel>
                <ul className="flex flex-wrap gap-1.5">
                  {coverage.leftOut.map((a) => (
                    <li key={a.key} className="flex items-center gap-1 rounded-md border py-0.5 pr-0.5 pl-2 text-muted-foreground">
                      <KeyText>{a.key}</KeyText>
                      <span>{a.label}</span>
                      <Button size="xs" variant="ghost" onClick={() => setLeftOut(a.key, false)}>
                        Bring back<span className="sr-only"> {a.label}</span>
                      </Button>
                    </li>
                  ))}
                </ul>
              </section>
            )}
          </div>
        </ScrollArea>

        <DialogFooter className="mx-0 mb-0">
          <Button variant="outline" onClick={onClose}>
            Done
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
