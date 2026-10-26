import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { useModels } from '@/lib/model/store'
import { sourcesOnRow } from '@/lib/report/coverage'
import { type RowPanelTab, useEditor } from '@/lib/report/store'
import { allRows, findRow } from '@/lib/report/tree'
import type { RowNode } from '@/lib/report/types'
import { cn } from '@/lib/utils'
import { KIND_LABEL, KeyText, KindGlyph, SectionLabel, StatusDot } from '../shared/bits'
import { EditableLabel } from '../shared/editable-label'
import { MappedSources, UnreadSources } from './mapped-sources'
import { RowParameters, RowStyle } from './row-formatting'
import { RowProperties } from './row-properties'
import { FormulaRowShows, WhatThisRowShows } from './what-this-row-shows'

export function RowPanel() {
  const { report, ui, setUi } = useEditor()
  const { getModel } = useModels()
  const tab = ui.rowPanelTab
  const row = ui.selectedRowKey ? findRow(report.rows, ui.selectedRowKey) : null

  if (!row) {
    return (
      <div className="flex h-full items-center justify-center p-6 text-center text-muted-foreground">
        Select a row to see what data feeds it and what it will show.
      </div>
    )
  }

  const hasDiagnostics = ui.advanced && row.kind === 'line'
  const model = getModel(report.settings.model)
  const modelParameters = model?.language === '1.2' ? model.parameters : []
  const hasParameters = row.kind !== 'heading' && modelParameters.length > 0
  const activeTab =
    tab === 'style'
      ? 'properties'
      : (tab === 'diagnostics' && !hasDiagnostics) || (tab === 'parameters' && !hasParameters)
        ? 'mapping'
        : tab

  return (
    <Tabs value={activeTab} onValueChange={(v) => setUi({ rowPanelTab: v as RowPanelTab })} className="flex h-full flex-col gap-0 overflow-y-auto">
      <div className="sticky top-0 z-10 flex flex-col border-b bg-background/95 backdrop-blur">
        <RowPanelHeader row={row} />
        <TabsList variant="line" className="h-9 px-3">
          <TabsTrigger value="mapping" className="flex-none px-2">
            {row.kind === 'formula' ? 'Formula' : row.kind === 'line' ? 'Mapping' : 'Lines'}
          </TabsTrigger>
          <TabsTrigger value="properties" className="flex-none px-2">
            Properties
          </TabsTrigger>
          {hasParameters && (
            <TabsTrigger value="parameters" className="flex-none px-2">
              Parameters
            </TabsTrigger>
          )}
          {hasDiagnostics && (
            <TabsTrigger value="diagnostics" className="flex-none px-2">
              Diagnostics
            </TabsTrigger>
          )}
        </TabsList>
      </div>
      <TabsContent value="mapping" className="flex flex-col gap-6 p-4 pb-10">
        {row.kind === 'line' && <MappedSources rowKey={row.key} />}
        {row.kind === 'formula' && <FormulaRowShows rowKey={row.key} />}
        {(row.kind === 'subtotal' || row.kind === 'heading') && <GroupRowNote row={row} />}
      </TabsContent>
      <TabsContent value="properties" className="flex flex-col gap-6 p-4 pb-10">
        <RowProperties row={row} />
        <div className="flex flex-col gap-4 border-t pt-6">
          <SectionLabel>Style</SectionLabel>
          <RowStyle row={row} />
        </div>
      </TabsContent>
      {hasParameters && (
        <TabsContent value="parameters" className="flex flex-col gap-6 p-4 pb-10">
          <RowParameters row={row} />
        </TabsContent>
      )}
      {hasDiagnostics && (
        <TabsContent value="diagnostics" className="flex flex-col gap-6 p-4 pb-10">
          <WhatThisRowShows rowKey={row.key} />
          <UnreadSources rowKey={row.key} />
        </TabsContent>
      )}
    </Tabs>
  )
}

function RowPanelHeader({ row }: { row: RowNode }) {
  const { ui } = useEditor()
  return (
    <header className="flex items-center gap-3 px-4 pt-3 pb-1">
      <div className="flex min-w-0 flex-1 flex-col">
        <div className="flex min-w-0 items-center gap-2">
          <EditableRowLabel key={row.key} row={row} />
          <span className="flex shrink-0 items-center gap-1.5 text-xs text-muted-foreground">
            <KindGlyph kind={row.kind} />
            {KIND_LABEL[row.kind]}
          </span>
          {row.hidden && (
            <Badge variant="outline" className="font-normal text-muted-foreground">
              Hidden
            </Badge>
          )}
        </div>
        {ui.advanced && <KeyText className="text-xs">{row.key}</KeyText>}
      </div>
    </header>
  )
}

function EditableRowLabel({ row }: { row: RowNode }) {
  const { mutate } = useEditor()
  return (
    <h2 className="-ml-2 flex min-w-0">
      <EditableLabel
        aria-label="Row label"
        title="Rename row"
        value={row.label}
        showIcon
        onCommit={(next) => {
          if (!next || next === row.label) return
          mutate((d) => {
            const r = findRow(d.rows, row.key)
            if (r) r.label = next
          })
        }}
        className="w-auto max-w-full text-base font-semibold"
      />
    </h2>
  )
}

function GroupRowNote({ row }: { row: RowNode }) {
  const { report, setUi } = useEditor()
  const lines = allRows(row.children).filter((r) => r.kind === 'line')
  const note =
    row.kind === 'subtotal'
      ? 'A subtotal totals the rows under it; map data on the lines below.'
      : 'A heading labels a group and shows no values; map data on the lines below.'

  return (
    <section aria-labelledby="group-lines" className="flex flex-col gap-3">
      <p className="text-pretty text-muted-foreground">{note}</p>
      <SectionLabel id="group-lines">Lines under this row</SectionLabel>
      {lines.length === 0 ? (
        <p className="rounded-lg border border-dashed p-3 text-muted-foreground">No lines under this row yet.</p>
      ) : (
        <ul className="flex flex-col divide-y rounded-lg border bg-card">
          {lines.map((line) => {
            const count = sourcesOnRow(report, line.key).length
            return (
              <li key={line.key}>
                <button
                  type="button"
                  onClick={() => setUi({ selectedRowKey: line.key })}
                  className="flex w-full items-center gap-2 px-3 py-2 text-left outline-none hover:bg-muted/60 focus-visible:bg-muted"
                >
                  <StatusDot status={count ? 'pass' : 'warn'} />
                  <span className="min-w-0 flex-1 truncate">{line.label}</span>
                  <span className={cn('text-xs tabular-nums', count ? 'text-muted-foreground' : 'text-warning-foreground')}>
                    {count ? `${count} ${count === 1 ? 'source' : 'sources'}` : 'No data'}
                  </span>
                </button>
              </li>
            )
          })}
        </ul>
      )}
    </section>
  )
}
