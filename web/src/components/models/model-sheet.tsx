import { useState, type ReactNode } from 'react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Sheet, SheetContent, SheetDescription, SheetFooter, SheetHeader, SheetTitle } from '@/components/ui/sheet'
import { Segmented } from '@/components/report/shared/segmented'
import { SimpleSelect } from '@/components/report/shared/simple-select'
import { DOTTED_KEY_PATTERN } from '@/lib/api/contract'
import type { ApiResult, AttributionMethod, CalcModel, CalcModelCreateInput, ModelFormulaLanguage, Problem } from '@/lib/models/contract'

function Field({ id, label, help, error, children }: { id: string; label: string; help?: string; error?: string | null; children: ReactNode }) {
  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor={id}>{label}</Label>
      {children}
      {error ? <p className="text-xs text-destructive">{error}</p> : help && <p className="text-xs text-pretty text-muted-foreground">{help}</p>}
    </div>
  )
}

const blank = (): CalcModelCreateInput => ({ key: '', displayName: '', currencyCode: 'USD', formulaLanguage: '1.0', attributionMethod: 'shapley' })

/** The server's answer for one field: its input errors, or for `key` a taken key's conflict. */
function fieldError(problem: Problem | null, field: keyof CalcModelCreateInput) {
  if (!problem) return null
  if (problem.errors[field]) return problem.errors[field].join(' ')
  return field === 'key' && problem.status === 409 ? (problem.detail ?? problem.title) : null
}

/** Creates a disabled, empty model (a command, not a staged edit). */
export function ModelSheet({ open, onClose, onCreate }: { open: boolean; onClose: () => void; onCreate: (input: CalcModelCreateInput) => Promise<ApiResult<CalcModel>> }) {
  const [input, setInput] = useState(blank)
  const [problem, setProblem] = useState<Problem | null>(null)
  const [submitted, setSubmitted] = useState(false)
  const keyError = !input.key.trim() ? 'Enter a key.' : DOTTED_KEY_PATTERN.test(input.key) ? null : 'Use letters, digits, dots and underscores; start with a letter.'
  const nameError = input.displayName.trim() ? null : 'Enter a name.'

  const close = () => {
    setInput(blank())
    setProblem(null)
    setSubmitted(false)
    onClose()
  }
  const submit = async () => {
    setSubmitted(true)
    if (keyError || nameError) return
    const result = await onCreate({ ...input, displayName: input.displayName.trim() })
    if (result.ok) close()
    else setProblem(result.problem)
  }

  return (
    <Sheet open={open} onOpenChange={(o) => !o && close()}>
      <SheetContent className="w-full gap-0 p-0 data-[side=right]:sm:max-w-md">
        <form
          className="flex h-full min-h-0 flex-col"
          onSubmit={(e) => {
            e.preventDefault()
            void submit()
          }}
        >
          <SheetHeader className="border-b p-4">
            <SheetTitle>New model</SheetTitle>
            <SheetDescription>A calculation model holds the variables and calculated measures reports read. It starts disabled and empty.</SheetDescription>
          </SheetHeader>
          <div className="flex min-h-0 flex-1 flex-col gap-5 overflow-y-auto p-4">
            <Field id="md-key" label="Key" help="Fixed once created." error={(submitted ? keyError : null) ?? fieldError(problem, 'key')}>
              <Input id="md-key" value={input.key} onChange={(e) => setInput({ ...input, key: e.target.value.toLowerCase() })} className="font-mono" placeholder="bank.margin_model" autoFocus />
            </Field>
            <Field id="md-name" label="Name" error={(submitted ? nameError : null) ?? fieldError(problem, 'displayName')}>
              <Input id="md-name" value={input.displayName} onChange={(e) => setInput({ ...input, displayName: e.target.value })} />
            </Field>
            <Field id="md-currency" label="Currency" error={fieldError(problem, 'currencyCode')}>
              <SimpleSelect
                id="md-currency"
                value={input.currencyCode}
                onChange={(currencyCode) => setInput({ ...input, currencyCode })}
                options={['USD', 'EUR', 'GBP', 'CAD'].map((c) => ({ value: c, label: c }))}
              />
            </Field>
            <Field id="md-language" label="Formula language" help="1.1 adds POWER. 1.2 adds declared parameters (@key), text comparisons and YEARFRAC(convention).">
              <Segmented<ModelFormulaLanguage>
                aria-label="Formula language"
                value={input.formulaLanguage}
                onChange={(formulaLanguage) => setInput({ ...input, formulaLanguage })}
                options={[
                  { value: '1.0', label: '1.0' },
                  { value: '1.1', label: '1.1' },
                  { value: '1.2', label: '1.2' },
                ]}
              />
            </Field>
            <Field id="md-method" label="Attribution method" help="Shapley averages over every switching order; Sequential follows the drivers' listed order.">
              <Segmented<AttributionMethod>
                aria-label="Attribution method"
                value={input.attributionMethod}
                onChange={(attributionMethod) => setInput({ ...input, attributionMethod })}
                options={[
                  { value: 'shapley', label: 'Shapley' },
                  { value: 'sequential', label: 'Sequential' },
                ]}
              />
            </Field>
          </div>
          <SheetFooter className="flex-row justify-end border-t p-4">
            <Button type="button" variant="outline" onClick={close}>
              Cancel
            </Button>
            <Button type="submit">Create model</Button>
          </SheetFooter>
        </form>
      </SheetContent>
    </Sheet>
  )
}
