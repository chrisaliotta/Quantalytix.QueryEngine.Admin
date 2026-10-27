import { ArrowLeftIcon, PlusIcon } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { DetailShell } from '@/components/shell/page-shell'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { ModelEditScope, useModelDocument, useModelsApi, useRefreshModels } from '@/lib/models/client'
import type { ModelDocument, ModelListItem, Violation } from '@/lib/models/contract'
import { attributionOf, findMetric, metricsOf, type ModelMetric } from '@/lib/models/metrics'
import { cn } from '@/lib/utils'
import { AttributionEditor } from './attribution-editor'
import { FormulaText } from './formula-editor'
import { METHOD_LABEL } from './method-explainer'
import { MeasuresSection } from './measures-section'
import { ModelGeneralSection } from './model-general-section'
import { VariablesSection } from './variables-section'
import { ModelHeader } from './model-header'
import { MODEL_SECTION_LABEL, ModelNav, type ModelSection } from './model-nav'
import { ParametersTab } from './parameters-tab'
import { RefusalAlert } from './refusal-alert'
import { refusalOf } from './use-model-edit'

export function EnabledBadge({ enabled }: { enabled: boolean }) {
  return enabled ? (
    <Badge variant="outline" className="gap-1.5">
      <span aria-hidden className="size-1.5 rounded-full bg-primary" />
      Enabled
    </Badge>
  ) : (
    <Badge variant="secondary">Disabled</Badge>
  )
}

function Th({ children, className }: { children?: ReactNode; className?: string }) {
  return <th className={cn('h-9 px-3 text-left text-xs font-medium whitespace-nowrap text-muted-foreground', className)}>{children}</th>
}
function Td({ children, className, colSpan }: { children?: ReactNode; className?: string; colSpan?: number }) {
  return (
    <td colSpan={colSpan} className={cn('px-3 py-2 align-middle', className)}>
      {children}
    </td>
  )
}

type DetailTab = ModelSection
const TAB_LABEL = MODEL_SECTION_LABEL

const SECTION_HELP: Record<DetailTab, string> = {
  variables: 'Values the model reads from data sources.',
  measures: 'Values the model computes from variables and parameters.',
  parameters: 'Named constants formulas can reference with @key.',
  attribution: 'How a change in each metric splits across its drivers.',
  general: 'Settings that apply to the whole model.',
}

function SectionHeader({ tab, action }: { tab: DetailTab; action?: ReactNode }) {
  return (
    <div className="flex flex-wrap items-end justify-between gap-3">
      <div className="flex flex-col gap-1">
        <h2 className="text-lg font-semibold tracking-tight">{TAB_LABEL[tab]}</h2>
        <p className="text-sm text-muted-foreground">{SECTION_HELP[tab]}</p>
      </div>
      {action}
    </div>
  )
}

interface ModelDetailProps {
  /** The saved model, as the list shows it. */
  model: ModelListItem
  onBack: () => void
  onOpenReport: (key: string) => void
}

/** One model's editor: every edit stages in the model's change set, which preview refuses when it introduces a violation (OI-42). */
export function ModelDetail({ model, onBack, onOpenReport }: ModelDetailProps) {
  return (
    <ModelEditScope modelId={model.id} label={model.displayName}>
      <ModelEditor saved={model} onBack={onBack} onOpenReport={onOpenReport} />
    </ModelEditScope>
  )
}

function ModelEditor({ saved, onBack, onOpenReport }: { saved: ModelListItem; onBack: () => void; onOpenReport: (key: string) => void }) {
  const { data: model } = useModelDocument(saved.id)
  const api = useModelsApi()
  const refresh = useRefreshModels()
  const [refusal, setRefusal] = useState<Violation[] | null>(null)
  const [tab, setTabState] = useState<DetailTab>('variables')
  const [attrKey, setAttrKeyState] = useState<string | null>(null)
  const [focusKey, setFocusKey] = useState<string | null>(null)
  const [attrFilter, setAttrFilter] = useState<'all' | ModelMetric['type']>('all')
  if (!model) return null

  const setTab = (next: DetailTab) => {
    setTabState(next)
    setAttrKeyState(null)
    setFocusKey(null)
  }
  const setAttrKey = (key: string | null) => {
    setAttrKeyState(key)
    if (key) {
      setTabState('attribution')
      setRefusal(null)
    }
  }

  const handleDelete = async () => {
    const result = await api.deleteModel(model.id)
    if (!result.ok) {
      setRefusal(refusalOf(result.problem))
      return
    }
    await refresh()
    onBack()
  }

  const attrMetric = attrKey ? (findMetric(model, attrKey) ?? null) : null
  const allMetrics = metricsOf(model)
  const attributionRows = allMetrics.filter((m) => attrFilter === 'all' || m.type === attrFilter)
  const attrCounts = {
    all: allMetrics.length,
    variable: model.variables.length,
    measure: model.measures.length,
  }

  return (
    <DetailShell
      header={<ModelHeader model={model} savedEnabled={saved.isEnabled} onBack={onBack} onDelete={handleDelete} onRefused={setRefusal} />}
      nav={<ModelNav model={model} active={tab} onSelect={setTab} />}
    >
      <div key={attrMetric ? `attr-${attrMetric.key}` : tab} className="contents">
        {refusal && <RefusalAlert violations={refusal} onDismiss={() => setRefusal(null)} />}

        {attrMetric && (
          <>
            <Button variant="ghost" size="sm" className="-ml-2 w-fit text-muted-foreground" onClick={() => setAttrKey(null)}>
              <ArrowLeftIcon /> All attributions
            </Button>
            <AttributionEditor model={model} metric={attrMetric} onClose={() => setAttrKey(null)} onRefused={setRefusal} />
          </>
        )}

        {tab === 'parameters' && <ParametersTab model={model} focusKey={focusKey} onFocus={setFocusKey} />}

        {tab === 'general' && (
          <>
            <SectionHeader tab="general" />
            <ModelGeneralSection model={model} onRefused={setRefusal} onOpenReport={onOpenReport} />
          </>
        )}

        {tab === 'variables' && !attrMetric && <VariablesSection model={model} focusKey={focusKey} onFocus={setFocusKey} onAttribution={setAttrKey} />}

        {tab === 'measures' && !attrMetric && <MeasuresSection model={model} focusKey={focusKey} onFocus={setFocusKey} onAttribution={setAttrKey} />}

        {tab === 'attribution' && !attrMetric && (
          <>
            <SectionHeader tab="attribution" />
            <div role="radiogroup" aria-label="Filter by category" className="flex w-fit items-center gap-1 rounded-lg border bg-card p-1 text-sm">
              {(
                [
                  ['all', 'All'],
                  ['variable', 'Variables'],
                  ['measure', 'Calculated measures'],
                ] as const
              ).map(([value, label]) => (
                <button
                  key={value}
                  type="button"
                  role="radio"
                  aria-checked={attrFilter === value}
                  onClick={() => setAttrFilter(value)}
                  className={cn(
                    'rounded-md px-2.5 py-1 text-muted-foreground transition-colors hover:text-foreground',
                    attrFilter === value && 'bg-muted font-medium text-foreground',
                  )}
                >
                  {label} <span className="text-xs text-muted-foreground">{attrCounts[value]}</span>
                </button>
              ))}
            </div>
            <div className="overflow-x-auto rounded-lg border bg-card">
              <table className="w-full text-sm">
                <thead className="border-b">
                  <tr>
                    <Th>Metric</Th>
                    <Th>Category</Th>
                    <Th>Expression</Th>
                    <Th>Drivers</Th>
                    <Th>Weight variable</Th>
                    <Th>Method</Th>
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {attributionRows.map((m) => (
                    <AttributionRow key={m.key} model={model} metric={m} onOpen={() => setAttrKey(m.key)} />
                  ))}
                  {attributionRows.length === 0 && (
                    <tr>
                      <Td colSpan={6} className="py-6 text-center text-muted-foreground">
                        Nothing in this category yet.
                      </Td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
            <p className="text-xs text-muted-foreground">Variables without an attribution have no formula to split until you add one.</p>
          </>
        )}
      </div>
    </DetailShell>
  )
}

function AttributionRow({ model, metric, onOpen }: { model: ModelDocument; metric: ModelMetric; onOpen: () => void }) {
  const isVariable = metric.type === 'variable'
  const own = attributionOf(model, metric.key)
  const category = (
    <span className="rounded-sm border px-1.5 py-0.5 text-xs whitespace-nowrap text-muted-foreground">{isVariable ? 'Variable' : 'Calculated measure'}</span>
  )
  const nameCell = (
    <Td>
      <button type="button" onClick={onOpen} className="flex flex-col text-left hover:underline">
        <span>{metric.displayName}</span>
        <span className="font-mono text-xs text-muted-foreground">{metric.key}</span>
      </button>
    </Td>
  )

  if (isVariable && !own) {
    return (
      <tr className="hover:bg-muted/40">
        {nameCell}
        <Td>{category}</Td>
        <Td colSpan={3}>
          <span className="flex items-center gap-3 text-muted-foreground">
            Not attributed
            <Button variant="outline" size="xs" onClick={onOpen}>
              <PlusIcon /> Add attribution
            </Button>
          </span>
        </Td>
        <Td className="text-muted-foreground">—</Td>
      </tr>
    )
  }

  const drivers = model.effectiveDrivers[metric.key] ?? []
  const weight = own?.weightVariableKey ? findMetric(model, own.weightVariableKey) : undefined
  return (
    <tr className="hover:bg-muted/40">
      {nameCell}
      <Td>{category}</Td>
      <Td className="max-w-80">
        {own?.expression ? <FormulaText formula={own.expression} className="line-clamp-2" /> : <span className="text-muted-foreground">Own formula</span>}
      </Td>
      <Td>
        <ol className="flex flex-wrap items-center gap-1">
          {drivers.map((d, i) => (
            <li key={d.key} className="flex items-center gap-1">
              {i > 0 && <span aria-hidden className="text-muted-foreground">›</span>}
              <span className="rounded-sm border bg-muted px-1.5 py-0.5 font-mono text-xs">{d.key}</span>
            </li>
          ))}
        </ol>
      </Td>
      <Td>{weight ? weight.displayName : <span className="text-muted-foreground">—</span>}</Td>
      <Td className="text-muted-foreground">{METHOD_LABEL[model.attributionMethod]} (model)</Td>
    </tr>
  )
}
