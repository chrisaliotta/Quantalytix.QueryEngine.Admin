import type { ReactNode } from 'react'
import { Label } from '@/components/ui/label'
import { Segmented } from '@/components/report/shared/segmented'
import { SimpleSelect } from '@/components/report/shared/simple-select'
import type { AttributionMethod, CalcModelPatch, ModelDocument, ModelFormulaLanguage, Violation } from '@/lib/models/contract'
import { MethodExplainer } from './method-explainer'
import { useModelEdit } from './use-model-edit'

interface ModelGeneralSectionProps {
  model: ModelDocument
  onRefused: (violations: Violation[] | null) => void
  onOpenReport: (key: string) => void
}

function SettingRow({ id, label, help, extra, children }: { id: string; label: string; help: string; extra?: ReactNode; children: ReactNode }) {
  return (
    <div className="flex flex-col gap-3 px-4 py-4 md:flex-row md:items-start md:justify-between md:gap-8">
      <div className="flex max-w-md flex-col gap-1">
        <div className="flex items-center gap-1.5">
          <Label htmlFor={id}>{label}</Label>
          {extra}
        </div>
        <p className="text-sm leading-relaxed text-pretty text-muted-foreground">{help}</p>
      </div>
      <div className="w-full shrink-0 md:w-64">{children}</div>
    </div>
  )
}

export function ModelGeneralSection({ model, onRefused, onOpenReport }: ModelGeneralSectionProps) {
  const run = useModelEdit()
  const update = async (patch: CalcModelPatch) => onRefused(await run((api) => api.updateModel(model.id, patch)))
  return (
    <div className="flex flex-col gap-6">
      <section aria-labelledby="model-settings-heading" className="flex flex-col gap-3">
        <h3 id="model-settings-heading" className="text-sm font-medium">
          Model settings
        </h3>
        <div className="divide-y rounded-lg border bg-card">
          <SettingRow id="gen-currency" label="Currency" help="The currency every amount in this model is stated in.">
            <SimpleSelect
              id="gen-currency"
              value={model.currencyCode}
              onChange={(currencyCode) => void update({ currencyCode })}
              options={['USD', 'EUR', 'GBP', 'CAD'].map((c) => ({ value: c, label: c }))}
            />
          </SettingRow>
          <SettingRow id="gen-calendar" label="Calendar" help="How periods are bucketed for prior-period and year-to-date reads; every model follows the Gregorian calendar.">
            <p id="gen-calendar" className="text-sm capitalize">
              {model.calendarKey}
            </p>
          </SettingRow>
          <SettingRow
            id="gen-language"
            label="Formula language version"
            help="1.1 adds POWER. 1.2 adds declared parameters (@key), text comparisons and YEARFRAC(convention). Moving down is refused while a formula uses a newer feature."
          >
            <div id="gen-language">
              <Segmented<ModelFormulaLanguage>
                aria-label="Formula language version"
                value={model.formulaLanguage}
                onChange={(formulaLanguage) => void update({ formulaLanguage })}
                options={[
                  { value: '1.0', label: '1.0' },
                  { value: '1.1', label: '1.1' },
                  { value: '1.2', label: '1.2' },
                ]}
              />
            </div>
          </SettingRow>
          <SettingRow
            id="gen-method"
            label="Default attribution method"
            help="Shapley averages over every switching order; Sequential follows the drivers' listed order."
            extra={<MethodExplainer />}
          >
            <div id="gen-method">
              <Segmented<AttributionMethod>
                aria-label="Default attribution method"
                value={model.attributionMethod}
                onChange={(attributionMethod) => void update({ attributionMethod })}
                options={[
                  { value: 'shapley', label: 'Shapley' },
                  { value: 'sequential', label: 'Sequential' },
                ]}
              />
            </div>
          </SettingRow>
        </div>
        <p className="text-xs text-muted-foreground">
          Saved changes apply to every enabled report over this model. A change that would break a formula, an attribution or a report is refused.
        </p>
      </section>

      <section aria-labelledby="model-usedby-heading" className="flex flex-col gap-3">
        <h3 id="model-usedby-heading" className="text-sm font-medium">
          Used by
        </h3>
        <div className="rounded-lg border bg-card">
          {model.usedBy.length === 0 ? (
            <p className="px-4 py-4 text-sm text-muted-foreground">No reports read this model yet.</p>
          ) : (
            <ul className="divide-y">
              {model.usedBy.map((r) => (
                <li key={r.reportId}>
                  <button
                    type="button"
                    onClick={() => onOpenReport(r.reportKey)}
                    className="flex w-full items-center justify-between gap-4 px-4 py-2.5 text-left text-sm hover:bg-muted/40"
                  >
                    <span className="text-primary">{r.displayName}</span>
                    <span className="font-mono text-xs text-muted-foreground">{r.reportKey}</span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      </section>
    </div>
  )
}
