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
import { useModelDeleteCheck, useModelList, useModelsApi, useRefreshModels } from '@/lib/models/client'
import type { ModelListItem } from '@/lib/models/contract'
import { EnabledBadge, ModelDetail } from './model-detail'
import { METHOD_LABEL } from './method-explainer'
import { ModelSheet } from './model-sheet'

interface ModelsScreenProps {
  modelKey: string | null
  onSelect: (key: string | null) => void
  onOpenReport: (key: string) => void
}

/** The model manager over the `ModelsApi` port; the route names a model by its fixed key. */
export function ModelsScreen({ modelKey, onSelect, onOpenReport }: ModelsScreenProps) {
  const api = useModelsApi()
  const refresh = useRefreshModels()
  const { data: models } = useModelList()
  const [creating, setCreating] = useState(false)
  const [deleting, setDeleting] = useState<ModelListItem | null>(null)
  const selected = modelKey ? models?.find((m) => m.key === modelKey) : undefined

  if (selected) return <ModelDetail model={selected} onBack={() => onSelect(null)} onOpenReport={onOpenReport} />
  if (modelKey && !models) return null

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
            {(models ?? []).map((m) => (
              <tr key={m.id} onClick={() => onSelect(m.key)} className="cursor-pointer hover:bg-muted/40 [&>td]:px-3 [&>td]:py-2.5">
                <td>
                  <button
                    type="button"
                    onClick={(e) => {
                      e.stopPropagation()
                      onSelect(m.key)
                    }}
                    className="font-medium hover:underline"
                  >
                    {m.displayName}
                  </button>
                </td>
                <td>{m.currencyCode}</td>
                <td className="font-mono text-xs">{m.formulaLanguage}</td>
                <td>{METHOD_LABEL[m.attributionMethod]}</td>
                <td className="text-right tabular-nums">{m.variableCount}</td>
                <td className="text-right tabular-nums">{m.measureCount}</td>
                <td className="text-right tabular-nums">
                  {m.usedBy.length} {m.usedBy.length === 1 ? 'report' : 'reports'}
                </td>
                <td>
                  <EnabledBadge enabled={m.isEnabled} />
                </td>
                <td className="w-10" onClick={(e) => e.stopPropagation()}>
                  <DropdownMenu>
                    <DropdownMenuTrigger render={<Button variant="ghost" size="icon-sm" aria-label={`${m.displayName} actions`} />}>
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
            ))}
          </tbody>
        </table>
      </div>
      <ModelSheet
        open={creating}
        onClose={() => setCreating(false)}
        onCreate={async (input) => {
          const result = await api.createModel(input)
          if (result.ok) {
            await refresh()
            onSelect(result.data.key)
          }
          return result
        }}
      />
      {deleting && <DeleteModelConfirm model={deleting} onClose={() => setDeleting(null)} />}
    </ListPage>
  )
}

function DeleteModelConfirm({ model, onClose }: { model: ModelListItem; onClose: () => void }) {
  const api = useModelsApi()
  const refresh = useRefreshModels()
  const { data: check } = useModelDeleteCheck(model.id)
  if (!check) return null
  return (
    <AlertDialog open onOpenChange={(o) => !o && onClose()}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{check.allowed ? `Delete ${model.displayName}?` : `${model.displayName} is in use`}</AlertDialogTitle>
          <AlertDialogDescription>
            {check.allowed
              ? 'This removes the model with its variables, calculated measures and parameters. This cannot be undone.'
              : `${check.message} Delete those reports or move them to another report type first.`}
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>{check.allowed ? 'Cancel' : 'Close'}</AlertDialogCancel>
          {check.allowed && (
            <AlertDialogAction
              variant="destructive"
              onClick={async () => {
                const result = await api.deleteModel(model.id)
                if (result.ok) {
                  toast(`${model.displayName} deleted`)
                  await refresh()
                } else toast.error(result.problem.title, { description: result.problem.detail })
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
