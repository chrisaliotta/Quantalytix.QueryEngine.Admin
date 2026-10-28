import { MoreHorizontalIcon, PlusIcon } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
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
import { ListPage } from '@/components/shell/page-shell'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { useModels } from '@/lib/model/store'
import type { CalcModel } from '@/lib/model/types'
import { EnabledBadge, ModelDetail } from './model-detail'
import { METHOD_LABEL } from './method-explainer'
import { ModelSheet } from './metric-sheets'

interface ModelsScreenProps {
  modelKey: string | null
  onSelect: (key: string | null) => void
  onOpenReport: (key: string) => void
}

export function ModelsScreen({ modelKey, onSelect, onOpenReport }: ModelsScreenProps) {
  const { models, usedBy, addModel } = useModels()
  const [creating, setCreating] = useState(false)
  const [deleting, setDeleting] = useState<CalcModel | null>(null)
  const selected = modelKey ? models.find((m) => m.key === modelKey) : undefined

  if (selected) return <ModelDetail model={selected} onBack={() => onSelect(null)} onOpenReport={onOpenReport} />

  return (
    <ListPage
      title="Calculation models"
      description="The rules for each report type: variables, calculated measures and attributions."
      actions={
        <Button size="sm" onClick={() => setCreating(true)}>
          <PlusIcon /> New model
        </Button>
      }
    >
        <div className="overflow-x-auto rounded-lg border bg-card">
          <table className="w-full text-sm">
            <thead className="border-b text-xs text-muted-foreground">
              <tr className="[&>th]:h-9 [&>th]:px-3 [&>th]:text-left [&>th]:font-medium [&>th]:whitespace-nowrap">
                <th>Report type</th>
                <th>Currency</th>
                <th>Formula language</th>
                <th>Attribution method</th>
                <th className="text-right!">Variables</th>
                <th className="text-right!">Calculated measures</th>
                <th className="text-right!">Used by</th>
                <th>Status</th>
                <th>
                  <span className="sr-only">Actions</span>
                </th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {models.map((m) => {
                const reports = usedBy(m.key).length
                return (
                  <tr
                    key={m.key}
                    onClick={() => onSelect(m.key)}
                    className="cursor-pointer hover:bg-muted/40 [&>td]:px-3 [&>td]:py-2.5"
                  >
                    <td>
                      <button
                        type="button"
                        onClick={(e) => {
                          e.stopPropagation()
                          onSelect(m.key)
                        }}
                        className="font-medium hover:underline"
                      >
                        {m.name}
                      </button>
                    </td>
                    <td>{m.currency}</td>
                    <td className="font-mono text-xs">{m.language}</td>
                    <td>{METHOD_LABEL[m.method]}</td>
                    <td className="text-right tabular-nums">{m.variables.length}</td>
                    <td className="text-right tabular-nums">{m.measures.length}</td>
                    <td className="text-right tabular-nums">
                      {reports} {reports === 1 ? 'report' : 'reports'}
                    </td>
                    <td>
                      <EnabledBadge enabled={m.enabled} />
                    </td>
                    <td className="w-10" onClick={(e) => e.stopPropagation()}>
                      <DropdownMenu>
                        <DropdownMenuTrigger render={<Button variant="ghost" size="icon-sm" aria-label={`${m.name} actions`} />}>
                          <MoreHorizontalIcon />
                        </DropdownMenuTrigger>
                        <DropdownMenuContent align="end" className="w-44">
                          <DropdownMenuItem onClick={() => onSelect(m.key)}>Open</DropdownMenuItem>
                          <DropdownMenuSeparator />
                          <DropdownMenuItem variant="destructive" onClick={() => setDeleting(m)}>
                            Delete…
                          </DropdownMenuItem>
                        </DropdownMenuContent>
                      </DropdownMenu>
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      <ModelSheet
        open={creating}
        model={null}
        takenKeys={models.map((m) => m.key)}
        onClose={() => setCreating(false)}
        onSave={(s) => {
          addModel({ ...s, enabled: false, variables: [], measures: [], parameters: [] })
          setCreating(false)
          onSelect(s.key)
        }}
      />
      {deleting && <DeleteModelConfirm model={deleting} onClose={() => setDeleting(null)} />}
    </ListPage>
  )
}

function DeleteModelConfirm({ model, onClose }: { model: CalcModel; onClose: () => void }) {
  const { removeModel, usedBy } = useModels()
  const reports = usedBy(model.key)
  const inUse = reports.length > 0
  return (
    <AlertDialog open onOpenChange={(o) => !o && onClose()}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{inUse ? `${model.name} is in use` : `Delete ${model.name}?`}</AlertDialogTitle>
          <AlertDialogDescription>
            {inUse
              ? `${reports.map((r) => r.name).join(', ')} ${reports.length === 1 ? 'reads' : 'read'} this model. Delete those reports or move them to another report type first.`
              : 'This removes the model with its variables, calculated measures and parameters. This cannot be undone.'}
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>{inUse ? 'Close' : 'Cancel'}</AlertDialogCancel>
          {!inUse && (
            <AlertDialogAction
              variant="destructive"
              onClick={() => {
                removeModel(model.key)
                toast(`${model.name} deleted`)
                onClose()
              }}
            >
              Delete model
            </AlertDialogAction>
          )}
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}
