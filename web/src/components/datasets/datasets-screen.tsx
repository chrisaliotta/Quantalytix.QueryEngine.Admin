import { DatabaseIcon, PlusIcon, SearchIcon } from 'lucide-react'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { SimpleSelect } from '@/components/report/shared/simple-select'
import { ListPage } from '@/components/shell/page-shell'
import { useDatasetList, useDatasetsApi, useSourceObjects, useVocabularies } from '@/lib/datasets/client'
import type { DatasetCreateInput, DatasetSourceObjectKind, DatasetSourceObjectStats } from '@/lib/datasets/contract'
import { DatasetDetail } from './dataset-detail'
import { FormRow, ProblemBanner, StatusBadge, Td, Th, VocabSelect, useApiCall } from './shared'

export function DatasetsScreen({ datasetId, onSelect }: { datasetId: string | null; onSelect: (id: string | null) => void }) {
  if (datasetId) return <DatasetDetail key={datasetId} datasetId={datasetId} onBack={() => onSelect(null)} />
  return <DatasetList onOpen={onSelect} />
}

function DatasetList({ onOpen }: { onOpen: (id: string) => void }) {
  const { data, isLoading } = useDatasetList()
  const [query, setQuery] = useState('')
  const [creating, setCreating] = useState<DatasetCreateInput['mode'] | null>(null)
  const q = query.trim().toLowerCase()
  const rows = (data ?? []).filter((d) => !q || d.displayName.toLowerCase().includes(q) || d.key.toLowerCase().includes(q))

  return (
    <ListPage
      title="Datasets"
      description="Describe a tenant table or view so the engine can read it: fields, grains, dimension bindings and measures, versioned and published through releases."
      actions={
        <>
          <Button variant="outline" onClick={() => setCreating('existing')}>
            <DatabaseIcon /> Add existing
          </Button>
          <Button onClick={() => setCreating('new')}>
            <PlusIcon /> New dataset
          </Button>
        </>
      }
    >
        <div className="relative w-full max-w-sm">
          <SearchIcon aria-hidden className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input value={query} onChange={(e) => setQuery(e.target.value)} placeholder="Search name or key" aria-label="Search datasets" className="pl-8" />
        </div>

        <div className="overflow-x-auto rounded-lg border bg-card">
          <table className="w-full text-sm">
            <thead className="border-b bg-muted/40">
              <tr>
                <Th>Dataset</Th>
                <Th>Source</Th>
                <Th className="text-right">Records</Th>
                <Th className="text-right">Size on disk</Th>
                <Th>Latest version</Th>
                <Th>Engine reads</Th>
                <Th className="text-right">Versions</Th>
                <Th>Status</Th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {isLoading && (
                <tr>
                  <Td colSpan={8} className="py-8 text-center text-muted-foreground">
                    Loading datasets…
                  </Td>
                </tr>
              )}
              {rows.map((d) => {
                const latest = d.versions[0]
                const active = d.versions.find((v) => v.id === d.activeVersionId)
                return (
                  <tr key={d.id} className="hover:bg-muted/40">
                    <Td>
                      <button type="button" onClick={() => onOpen(d.id)} className="flex flex-col text-left">
                        <span className="font-medium hover:underline">{d.displayName}</span>
                        <span className="font-mono text-xs text-muted-foreground">{d.key}</span>
                      </button>
                    </Td>
                    <Td className="font-mono text-xs">{latest?.sourceObject ?? '—'}</Td>
                    <StorageCells storage={d.storage} />
                    <Td>{latest ? <StatusBadge status={latest.status} label={`v${latest.versionNumber} · ${latest.status}`} /> : '—'}</Td>
                    <Td>
                      {active ? (
                        <span className="font-mono text-xs">
                          v{active.versionNumber} <span className="text-muted-foreground">in {active.releases.find((r) => r.isActive)?.releaseKey}</span>
                        </span>
                      ) : (
                        <span className="text-xs text-muted-foreground">Not in the active release</span>
                      )}
                    </Td>
                    <Td className="text-right tabular-nums">{d.versions.length}</Td>
                    <Td>
                      <span className="inline-flex items-center gap-1.5 text-xs">
                        <span aria-hidden className={d.isEnabled ? 'size-2 rounded-full bg-primary' : 'size-2 rounded-full bg-muted-foreground/40'} />
                        {d.isEnabled ? 'Enabled' : 'Disabled'}
                      </span>
                    </Td>
                  </tr>
                )
              })}
              {!isLoading && !rows.length && (
                <tr>
                  <Td colSpan={8} className="py-8 text-center text-muted-foreground">
                    {q ? 'No datasets match.' : 'No datasets yet.'}
                  </Td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      {creating && <CreateDatasetDialog mode={creating} onClose={() => setCreating(null)} onCreated={onOpen} />}
    </ListPage>
  )
}

const compactCount = new Intl.NumberFormat('en-US', { notation: 'compact', maximumFractionDigits: 1 })
const exactCount = new Intl.NumberFormat('en-US')
const BYTE_UNITS = ['B', 'KB', 'MB', 'GB', 'TB', 'PB']

function formatBytes(bytes: number) {
  const exp = bytes > 0 ? Math.min(Math.floor(Math.log(bytes) / Math.log(1024)), BYTE_UNITS.length - 1) : 0
  const value = bytes / 1024 ** exp
  return `${value.toFixed(exp === 0 || value >= 100 ? 0 : 1)} ${BYTE_UNITS[exp]}`
}

function StorageCells({ storage }: { storage: DatasetSourceObjectStats | null }) {
  if (!storage)
    return (
      <>
        <Td className="text-right text-xs text-muted-foreground">Not measured</Td>
        <Td className="text-right text-xs text-muted-foreground">—</Td>
      </>
    )
  const measured = `Measured ${new Date(storage.measuredAt).toLocaleString()}`
  return (
    <>
      <Td className="text-right font-mono text-xs tabular-nums">
        <span title={`${exactCount.format(storage.rowCount)} records · ${measured}`}>{compactCount.format(storage.rowCount)}</span>
      </Td>
      <Td className="text-right font-mono text-xs tabular-nums">
        {storage.sizeBytes === 0 ? (
          <span title={`Reads through to its base tables; nothing stored · ${measured}`} className="font-sans text-muted-foreground">
            Not stored
          </span>
        ) : (
          <span title={`${exactCount.format(storage.sizeBytes)} bytes · ${measured}`}>{formatBytes(storage.sizeBytes)}</span>
        )}
      </Td>
    </>
  )
}

const CREATE_COPY = {
  existing: {
    title: 'Add existing dataset',
    description: 'Links a table or view that already exists. Draft version 1 maps every readable column to a field.',
    action: 'Add dataset',
    pending: 'Adding…',
  },
  new: {
    title: 'New dataset',
    description: 'Creates a managed table for the dataset. Add fields afterwards; each one can create its column on the table.',
    action: 'Create dataset',
    pending: 'Creating…',
  },
} as const

/** Asks for the dataset key up front, because keys are fixed once created (OI-36). */
function CreateDatasetDialog({ mode, onClose, onCreated }: { mode: DatasetCreateInput['mode']; onClose: () => void; onCreated: (id: string) => void }) {
  const api = useDatasetsApi()
  const { data: vocab } = useVocabularies()
  const { data: sources } = useSourceObjects()
  const { run, saving, problem, errorsFor } = useApiCall()
  const [key, setKey] = useState('')
  const [name, setName] = useState('')
  const [kind, setKind] = useState<DatasetSourceObjectKind>('table')
  const [object, setObject] = useState('')
  const [tableName, setTableName] = useState('')
  const copy = CREATE_COPY[mode]

  async function create() {
    const identity = { key: key.trim(), displayName: name.trim() }
    const input: DatasetCreateInput =
      mode === 'existing'
        ? { mode, ...identity, sourceObjectKind: kind, sourceObject: object }
        : { mode, ...identity, tableName: tableName.trim(), columns: [] }
    const d = await run(() => api.createDataset(input))
    if (d) {
      onClose()
      onCreated(d.id)
    }
  }

  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{copy.title}</DialogTitle>
          <DialogDescription>{copy.description}</DialogDescription>
        </DialogHeader>

        <div className="flex flex-col gap-4">
          {problem && !Object.keys(problem.errors).length && <ProblemBanner problem={problem} />}
          <div className="grid gap-4 sm:grid-cols-2">
            <FormRow label="Display name" htmlFor="nd-name" errors={errorsFor('displayName')}>
              <Input id="nd-name" value={name} onChange={(e) => setName(e.target.value)} placeholder="Daily deposits" aria-invalid={!!errorsFor('displayName')} autoFocus />
            </FormRow>
            <FormRow label="Dataset key" htmlFor="nd-key" errors={errorsFor('key')} hint="Query requests name the dataset by this key. Fixed after creation.">
              <Input id="nd-key" value={key} onChange={(e) => setKey(e.target.value)} placeholder="deposits.daily" className="font-mono" aria-invalid={!!errorsFor('key')} />
            </FormRow>
            {mode === 'existing' ? (
              <>
                <FormRow label="Source kind" htmlFor="nd-kind" errors={errorsFor('sourceObjectKind')}>
                  <VocabSelect
                    id="nd-kind"
                    terms={vocab?.sourceObjectKind}
                    value={kind}
                    onChange={(v) => {
                      setKind(v ?? 'table')
                      setObject('')
                    }}
                  />
                </FormRow>
                <FormRow label="Source object" htmlFor="nd-object" errors={errorsFor('sourceObject')}>
                  <SimpleSelect
                    id="nd-object"
                    value={object}
                    onChange={setObject}
                    placeholder="Choose…"
                    options={(sources ?? []).filter((s) => s.kind === kind).map((s) => ({ value: s.name, label: s.name, hint: s.isManaged ? 'managed' : undefined }))}
                    className="font-mono"
                  />
                </FormRow>
              </>
            ) : (
              <FormRow label="Table name" htmlFor="nd-table" errors={errorsFor('tableName')} hint="The managed table the API creates in the tenant database.">
                <Input id="nd-table" value={tableName} onChange={(e) => setTableName(e.target.value)} placeholder="daily_deposits" className="font-mono" aria-invalid={!!errorsFor('tableName')} />
              </FormRow>
            )}
          </div>
        </div>

        <DialogFooter>
          <Button variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button onClick={create} disabled={saving}>
            {saving ? copy.pending : copy.action}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
