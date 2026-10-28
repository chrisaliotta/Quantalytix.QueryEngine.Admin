import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import type { Violation } from '@/lib/reports/contract'
import { useReportEditor } from '../editor-state'
import { KeyText, Refusal, SectionLabel } from '../shared/bits'

/**
 * A bridge source's coverage, as the server computes it (OI-05): the items that reach no row, the items that reach more than one,
 * and the items left out on purpose (OI-51), which coverage does not count as gaps.
 */
export function CoverageDialog({ sourceKey, onClose }: { sourceKey: string; onClose: () => void }) {
  const { reportId, report, diagnostics, run } = useReportEditor()
  const source = report.sources.find((s) => s.key === sourceKey)!
  const coverage = diagnostics?.coverage[sourceKey]
  const [refusal, setRefusal] = useState<Violation[] | null>(null)
  const setLeftOut = async (itemKeys: string[]) => setRefusal(await run((api) => api.setLeftOut(reportId, sourceKey, itemKeys)))

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Coverage of {source.displayName}</DialogTitle>
          <DialogDescription>Every item the source reads should reach a row, or be left out on purpose.</DialogDescription>
        </DialogHeader>
        {coverage && (
          <div className="flex max-h-[60vh] flex-col gap-5 overflow-y-auto">
            <section className="flex flex-col gap-2">
              <SectionLabel>Not mapped ({coverage.unmapped.length})</SectionLabel>
              {coverage.unmapped.length === 0 && <p className="text-sm text-muted-foreground">Every item reaches a row.</p>}
              <ul className="flex flex-col gap-1">
                {coverage.unmapped.map((item) => (
                  <li key={item} className="flex items-center justify-between gap-2">
                    <KeyText>{item}</KeyText>
                    <Button size="xs" variant="outline" onClick={() => void setLeftOut([...source.leftOut, item])}>
                      Leave out
                    </Button>
                  </li>
                ))}
              </ul>
            </section>
            <section className="flex flex-col gap-2">
              <SectionLabel>In more than one row ({coverage.multi.length})</SectionLabel>
              <ul className="flex flex-col gap-1">
                {coverage.multi.map((m) => (
                  <li key={m.itemKey} className="flex items-center justify-between gap-2 text-sm">
                    <KeyText>{m.itemKey}</KeyText>
                    <span className="text-muted-foreground">
                      {m.rows.join(', ')} · counted ×{m.net}
                    </span>
                  </li>
                ))}
              </ul>
            </section>
            <section className="flex flex-col gap-2">
              <SectionLabel>Left out ({coverage.leftOut.length})</SectionLabel>
              <ul className="flex flex-col gap-1">
                {coverage.leftOut.map((item) => (
                  <li key={item} className="flex items-center justify-between gap-2">
                    <KeyText>{item}</KeyText>
                    <Button size="xs" variant="ghost" onClick={() => void setLeftOut(source.leftOut.filter((k) => k !== item))}>
                      Include
                    </Button>
                  </li>
                ))}
              </ul>
            </section>
            <Refusal violations={refusal} />
          </div>
        )}
      </DialogContent>
    </Dialog>
  )
}
