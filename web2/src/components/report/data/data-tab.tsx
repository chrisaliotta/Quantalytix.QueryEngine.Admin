import { DatabaseIcon, PlusIcon } from 'lucide-react'
import { useState } from 'react'
import { methodLabel } from '@/lib/report/scenarios'
import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/button'
import { Switch } from '@/components/ui/switch'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { useEditor } from '@/lib/report/store'
import { useCalcModel } from '@/lib/report/use-calc-model'
import { KeyText } from '../shared/bits'
import { BindingFilterDialog, ProfilesDialog } from './data-dialogs'
import { SourceCard } from './source-card'
import { NewSourceForm, SourceProperties } from './source-form'

type LocalDialog = { type: 'filter'; sourceKey: string; bindingId: string } | { type: 'profiles'; variable: string } | null

export function DataTab() {
  const { report, ui, setUi } = useEditor()
  const [matrix, setMatrix] = useState(false)
  const [dialog, setDialog] = useState<LocalDialog>(null)
  const [selectedKey, setSelectedKey] = useState<string | null>(report.sources[0]?.key ?? null)
  const showMatrix = ui.advanced && matrix
  const adding = ui.addingSource

  const selected = report.sources.find((s) => s.key === selectedKey) ?? report.sources[0]
  const startAdding = () => setUi({ addingSource: true })
  const selectSource = (key: string) => {
    setSelectedKey(key)
    setUi({ addingSource: false })
  }

  return (
    <div className="flex w-full flex-col gap-4 p-4 md:p-6">
      <div className="flex flex-wrap items-center gap-3">
        <div className="flex flex-1 flex-col">
          <h2 className="font-semibold">Sources</h2>
          <p className="text-pretty text-muted-foreground">Datasets the report reads, and which variables each one supplies.</p>
        </div>
        {ui.advanced && (
          <label className="flex items-center gap-2 text-muted-foreground">
            <Switch checked={matrix} onCheckedChange={setMatrix} size="sm" />
            Matrix view
          </label>
        )}
      </div>

      {showMatrix ? (
        <MatrixView onOpenProfiles={(variable) => setDialog({ type: 'profiles', variable })} />
      ) : (
        <div className="flex flex-col gap-4 lg:flex-row lg:items-start">
          <nav aria-label="Sources" className="flex shrink-0 flex-col gap-1 lg:w-64">
            <ul className="flex flex-col gap-1">
              {report.sources.map((s) => {
                const active = !adding && s.key === selected?.key
                return (
                  <li key={s.key}>
                    <button
                      type="button"
                      onClick={() => selectSource(s.key)}
                      aria-current={active ? 'true' : undefined}
                      className={cn(
                        'flex w-full items-start gap-2.5 rounded-md px-2.5 py-2 text-left outline-none transition-colors focus-visible:ring-2 focus-visible:ring-ring/60',
                        active ? 'bg-accent text-accent-foreground' : 'text-muted-foreground hover:bg-accent/50 hover:text-foreground',
                      )}
                    >
                      <DatabaseIcon className="mt-0.5 size-4 shrink-0" aria-hidden />
                      <span className="flex min-w-0 flex-col">
                        <span className={cn('truncate font-medium', active && 'text-foreground')}>{s.name}</span>
                        <span className="truncate text-xs text-muted-foreground">
                          {methodLabel(s)} · {s.bindings.length} {s.bindings.length === 1 ? 'variable' : 'variables'}
                        </span>
                      </span>
                    </button>
                  </li>
                )
              })}
              {adding && (
                <li>
                  <span
                    aria-current="true"
                    className="flex w-full items-center gap-2.5 rounded-md bg-accent px-2.5 py-2 font-medium text-foreground"
                  >
                    <PlusIcon className="size-4 shrink-0" aria-hidden />
                    New source
                  </span>
                </li>
              )}
            </ul>
            {!adding && (
              <Button variant="ghost" size="sm" className="justify-start text-muted-foreground" onClick={startAdding}>
                <PlusIcon /> New source
              </Button>
            )}
          </nav>

          <div className="min-w-0 flex-1">
            {adding ? (
              <NewSourceForm onCreated={selectSource} onCancel={() => setUi({ addingSource: false })} />
            ) : selected ? (
              <div className="flex flex-col gap-4">
                <SourceProperties key={`props-${selected.key}`} source={selected} onRenamed={setSelectedKey} />
                <SourceCard
                  key={selected.key}
                  source={selected}
                  onEditFilter={(bindingId) => setDialog({ type: 'filter', sourceKey: selected.key, bindingId })}
                  onOpenProfiles={(variable) => setDialog({ type: 'profiles', variable })}
                />
              </div>
            ) : (
              <div className="flex flex-col items-start gap-3 rounded-lg border border-dashed p-6">
                <p className="text-muted-foreground">This report has no sources yet. Add one to start supplying variables.</p>
                <Button size="sm" onClick={startAdding}>
                  <PlusIcon /> New source
                </Button>
              </div>
            )}
          </div>
        </div>
      )}

      {dialog?.type === 'filter' && <BindingFilterDialog sourceKey={dialog.sourceKey} bindingId={dialog.bindingId} onClose={() => setDialog(null)} />}
      {dialog?.type === 'profiles' && <ProfilesDialog variable={dialog.variable} onClose={() => setDialog(null)} />}
    </div>
  )
}

function MatrixView({ onOpenProfiles }: { onOpenProfiles: (variable: string) => void }) {
  const { report } = useEditor()
  const calc = useCalcModel()
  return (
    <div className="overflow-x-auto rounded-lg border bg-card">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead className="pl-4">Variable</TableHead>
            {report.sources.map((s) => (
              <TableHead key={s.key}>{s.name}</TableHead>
            ))}
          </TableRow>
        </TableHeader>
        <TableBody>
          {calc.variables.map((v) => {
            const count = report.sources.filter((s) => s.bindings.some((b) => b.variable === v.key)).length
            return (
              <TableRow key={v.key}>
                <TableCell className="pl-4">
                  <div className="flex items-center gap-2">
                    <KeyText className="text-foreground">{v.key}</KeyText>
                    {count > 1 && (
                      <Button variant="link" size="xs" className="h-auto p-0" onClick={() => onOpenProfiles(v.key)}>
                        Profiles<span className="sr-only"> for {v.key}</span>
                      </Button>
                    )}
                  </div>
                </TableCell>
                {report.sources.map((s) => {
                  const b = s.bindings.find((x) => x.variable === v.key)
                  return (
                    <TableCell key={s.key} className="font-mono text-xs">
                      {b ? (
                        <span>
                          {b.measure}
                          {b.filters.length > 0 && <span className="text-muted-foreground"> (filtered)</span>}
                        </span>
                      ) : (
                        <span className="text-muted-foreground" aria-label="Not supplied">
                          -
                        </span>
                      )}
                    </TableCell>
                  )
                })}
              </TableRow>
            )
          })}
        </TableBody>
      </Table>
    </div>
  )
}
