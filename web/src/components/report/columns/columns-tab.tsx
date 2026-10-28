import { closestCenter, DndContext, KeyboardSensor, PointerSensor, useSensor, useSensors, type DragEndEvent } from '@dnd-kit/core'
import { arrayMove, SortableContext, sortableKeyboardCoordinates, useSortable, verticalListSortingStrategy } from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import { FunctionSquareIcon, GripVerticalIcon, PencilIcon, PlusIcon, VariableIcon } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { useUndoAction } from '@/components/shell/history'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Switch } from '@/components/ui/switch'
import { useModels } from '@/lib/model/store'
import { useEditor } from '@/lib/report/store'
import { useCalcModel } from '@/lib/report/use-calc-model'
import type { Column } from '@/lib/report/types'
import { cn } from '@/lib/utils'
import { KeyText } from '../shared/bits'
import { ColumnDialog, FORMAT_LABEL } from './column-dialog'

type Editing = { key: string | null } | null

const TH = 'h-9 px-3 text-left align-middle text-xs font-medium whitespace-nowrap text-muted-foreground'
const TD = 'px-3 py-2 align-middle'

export function ColumnsTab() {
  const { report, ui, mutate } = useEditor()
  const undoAction = useUndoAction()
  const [editing, setEditing] = useState<Editing>(null)
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 4 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  )

  function onDragEnd({ active, over }: DragEndEvent) {
    if (!over || active.id === over.id) return
    const previous = report.columns.map((c) => c.key)
    const from = previous.indexOf(String(active.id))
    const to = previous.indexOf(String(over.id))
    mutate((d) => {
      d.columns = arrayMove(d.columns, from, to)
    })
    toast('Columns reordered', { action: undoAction() })
  }

  const visibleCount = report.columns.filter((c) => c.visible).length

  return (
    <div className="flex w-full flex-col gap-4 p-4 md:p-6">
      <div className="flex flex-wrap items-end gap-3">
        <div className="flex flex-1 flex-col gap-0.5">
          <h2 className="font-semibold">Columns</h2>
          <p className="text-pretty text-muted-foreground">
            Each column shows one metric. Drag to reorder. Changes stay in your draft until you save the report.
          </p>
        </div>
        <Button size="sm" onClick={() => setEditing({ key: null })}>
          <PlusIcon /> New column
        </Button>
      </div>

      {report.columns.length === 0 ? (
        <div className="flex flex-col items-center gap-2 rounded-lg border border-dashed p-10 text-center">
          <p className="font-medium">No columns yet</p>
          <p className="text-muted-foreground">Add a column to choose which metrics the report shows.</p>
          <Button size="sm" variant="outline" onClick={() => setEditing({ key: null })}>
            <PlusIcon /> New column
          </Button>
        </div>
      ) : (
        <div className="overflow-x-auto rounded-lg border bg-card">
          <DndContext id="columns-dnd" sensors={sensors} collisionDetection={closestCenter} onDragEnd={onDragEnd}>
            <table className="w-full caption-bottom text-sm">
              <caption className="sr-only">Columns in display order</caption>
              <thead className="border-b bg-muted/30">
                <tr>
                  <th className="w-8" aria-label="Reorder" />
                  <th className={cn(TH, 'w-8 px-1 text-right')}>#</th>
                  <th className={TH}>Label</th>
                  <th className={TH}>Returns</th>
                  <th className={cn(TH, 'hidden lg:table-cell')}>Drivers</th>
                  <th className={cn(TH, 'hidden lg:table-cell')}>Contribution</th>
                  <th className={TH}>Format</th>
                  {ui.advanced && <th className={cn(TH, 'hidden xl:table-cell')}>Key</th>}
                  <th className={cn(TH, 'w-20')}>Visible</th>
                  <th className="w-12" aria-label="Actions" />
                </tr>
              </thead>
              <SortableContext items={report.columns.map((c) => c.key)} strategy={verticalListSortingStrategy}>
                <tbody>
                  {report.columns.map((c, i) => (
                    <ColumnRow key={c.key} column={c} index={i} showKey={ui.advanced} onOpen={() => setEditing({ key: c.key })} />
                  ))}
                </tbody>
              </SortableContext>
            </table>
          </DndContext>
        </div>
      )}

      {report.columns.length > 0 && (
        <p className="text-pretty text-xs text-muted-foreground">
          {visibleCount} of {report.columns.length} columns visible. Drivers and contribution can only be requested when every
          selected column supports them.
        </p>
      )}

      {editing && <ColumnDialog columnKey={editing.key} onClose={() => setEditing(null)} />}
    </div>
  )
}

function useCapabilities(metric: string) {
  const { report } = useEditor()
  const { getModel } = useModels()
  const model = getModel(report.settings.model)
  const entry = model?.variables.find((v) => v.key === metric) ?? model?.measures.find((m) => m.key === metric)
  const drivers = entry?.attribution?.drivers ?? []
  const weightKey = entry?.attribution?.weight
  const weight = weightKey ? (model?.variables.find((v) => v.key === weightKey)?.name ?? weightKey) : undefined
  return { drivers, weight }
}

function ColumnRow({ column, index, showKey, onOpen }: { column: Column; index: number; showKey: boolean; onOpen: () => void }) {
  const { report, mutate } = useEditor()
  const calc = useCalcModel()
  const { attributes, listeners, setNodeRef, setActivatorNodeRef, transform, transition, isDragging } = useSortable({ id: column.key })
  const { drivers, weight } = useCapabilities(column.metric)
  const isCalculated = column.kind === 'calculated'
  const measure = isCalculated ? calc.measures.find((m) => m.key === column.metric) : undefined
  const variable = !isCalculated ? calc.variables.find((v) => v.key === column.metric) : undefined
  const metricName = measure?.name ?? variable?.name

  function setVisible(visible: boolean) {
    mutate((d) => {
      const target = d.columns.find((c) => c.key === column.key)
      if (target) target.visible = visible
    })
  }

  return (
    <tr
      ref={setNodeRef}
      style={{ transform: CSS.Translate.toString(transform), transition }}
      className={cn(
        'group border-b bg-card transition-colors last:border-b-0 hover:bg-muted/40',
        isDragging && 'relative z-10 shadow-md',
      )}
    >
      <td className="w-8 p-0 align-middle">
        <button
          ref={setActivatorNodeRef}
          type="button"
          className="flex h-11 w-8 cursor-grab items-center justify-center text-muted-foreground hover:text-foreground focus-visible:outline-2 focus-visible:outline-ring active:cursor-grabbing"
          aria-label={`Reorder ${column.label}`}
          {...attributes}
          {...listeners}
        >
          <GripVerticalIcon className="size-4" />
        </button>
      </td>
      <td className="w-8 px-1 text-right align-middle text-xs text-muted-foreground tabular-nums">{index + 1}</td>
      <td className={TD}>
        <button
          type="button"
          onClick={onOpen}
          className={cn(
            'rounded-sm text-left font-medium outline-none hover:underline focus-visible:ring-2 focus-visible:ring-ring',
            !column.visible && 'text-muted-foreground',
          )}
        >
          {column.label}
        </button>
      </td>
      <td className={TD}>
        <span className="flex min-w-0 items-center gap-2" title={measure?.formula}>
          <Badge variant="secondary" className="shrink-0 gap-1 font-mono font-normal">
            {isCalculated ? <FunctionSquareIcon aria-hidden /> : <VariableIcon aria-hidden />}
            {column.metric}
            <span className="sr-only">{isCalculated ? ' (calculated measure)' : ' (variable)'}</span>
          </Badge>
          {metricName && metricName !== column.label && <span className="truncate text-muted-foreground">{metricName}</span>}
        </span>
      </td>
      <td className={cn(TD, 'hidden lg:table-cell')}>
        {drivers.length ? (
          <span className="flex flex-wrap gap-1">
            {drivers.map((d) => (
              <Badge key={d.key} variant="outline" className="font-mono font-normal">
                {d.key}
              </Badge>
            ))}
          </span>
        ) : (
          <span className="text-muted-foreground">None</span>
        )}
      </td>
      <td className={cn(TD, 'hidden lg:table-cell')}>
        {weight ? <span>Weighted by {weight}</span> : <span className="text-muted-foreground">None</span>}
      </td>
      <td className={cn(TD, 'whitespace-nowrap text-muted-foreground')}>{FORMAT_LABEL[column.format]}</td>
      {showKey && (
        <td className={cn(TD, 'hidden xl:table-cell')}>
          <KeyText>{column.key}</KeyText>
        </td>
      )}
      <td className={TD}>
        <Switch size="sm" checked={column.visible} onCheckedChange={setVisible} aria-label={`Show ${column.label}`} />
      </td>
      <td className="w-12 pr-2 text-right align-middle">
        <Button variant="ghost" size="icon-sm" onClick={onOpen} aria-label={`Edit ${column.label}`}>
          <PencilIcon />
        </Button>
      </td>
    </tr>
  )
}
