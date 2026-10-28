import { PlusIcon, Trash2Icon } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Switch } from '@/components/ui/switch'
import type { ReportBorderStyle, ReportFontWeight, ReportStyle, ReportStyleRole, Violation } from '@/lib/reports/contract'
import { useReportEditor } from '../editor-state'
import { KeyText, Refusal } from '../shared/bits'
import { SimpleSelect } from '../shared/simple-select'

const NONE = '__none__'
const ROLES: ReportStyleRole[] = ['header', 'subtotal', 'total', 'detail', 'formula']
const WEIGHTS: ReportFontWeight[] = ['normal', 'medium', 'semibold', 'bold']
const BORDERS: ReportBorderStyle[] = ['none', 'thin', 'medium', 'thick', 'double']

const blank = (key: string): ReportStyle => ({
  key,
  displayName: key,
  defaultRole: null,
  fontFamily: null,
  fontSize: null,
  fontWeight: null,
  isItalic: false,
  textColor: null,
  backgroundColor: null,
  borderTop: null,
  borderBottom: null,
  indent: null,
})

/** The report's styles: how rows that name one, or rows of its default role, show (P11). A style a row shows cannot be deleted. */
export function StylesTab() {
  const { report } = useReportEditor()
  const [adding, setAdding] = useState(false)
  return (
    <div className="flex flex-col gap-4 p-4 md:p-6">
      <div className="flex items-center justify-between gap-2">
        <div>
          <h2 className="text-lg font-semibold">Styles</h2>
          <p className="text-sm text-muted-foreground">A property left empty keeps the client's own styling. Each role has at most one default style.</p>
        </div>
        <Button onClick={() => setAdding(true)} disabled={adding}>
          <PlusIcon /> Add style
        </Button>
      </div>
      {adding && <StyleForm initial={blank(`style_${report.styles.length + 1}`)} isNew onDone={() => setAdding(false)} />}
      {report.styles.map((style) => (
        <StyleForm key={style.key} initial={style} onDone={() => {}} />
      ))}
    </div>
  )
}

function StyleForm({ initial, isNew, onDone }: { initial: ReportStyle; isNew?: boolean; onDone: () => void }) {
  const { reportId, run } = useReportEditor()
  const [style, setStyle] = useState(initial)
  const [refusal, setRefusal] = useState<Violation[] | null>(null)
  const patch = (p: Partial<ReportStyle>) => setStyle((s) => ({ ...s, ...p }))
  const optional = <T extends string>(values: T[]) => [{ value: NONE, label: 'Client default' }, ...values.map((v) => ({ value: v, label: v }))]
  const pick = <T extends string>(v: string) => (v === NONE ? null : (v as T))

  const save = async () => {
    const violations = await run((api) => api.saveStyle(reportId, style))
    setRefusal(violations)
    if (!violations) onDone()
  }

  return (
    <section className="flex flex-col gap-3 rounded-lg border bg-card p-4" aria-label={style.displayName}>
      <div className="flex items-center gap-2">
        {isNew ? (
          <Input aria-label="Style key" className="h-7 w-40 font-mono" value={style.key} onChange={(e) => patch({ key: e.target.value.trim() })} />
        ) : (
          <KeyText>{style.key}</KeyText>
        )}
        {!isNew && (
          <Button
            size="icon-sm"
            variant="ghost"
            className="ml-auto"
            aria-label={`Delete ${style.displayName}`}
            onClick={async () => setRefusal(await run((api) => api.deleteStyle(reportId, style.key)))}
          >
            <Trash2Icon />
          </Button>
        )}
      </div>
      <div className="grid gap-3 sm:grid-cols-3">
        <Field label="Name">
          <Input value={style.displayName} onChange={(e) => patch({ displayName: e.target.value })} />
        </Field>
        <Field label="Default for role">
          <SimpleSelect value={style.defaultRole ?? NONE} onChange={(v) => patch({ defaultRole: pick<ReportStyleRole>(v) })} options={[{ value: NONE, label: 'None' }, ...ROLES.map((r) => ({ value: r, label: r }))]} />
        </Field>
        <Field label="Font weight">
          <SimpleSelect value={style.fontWeight ?? NONE} onChange={(v) => patch({ fontWeight: pick<ReportFontWeight>(v) })} options={optional(WEIGHTS)} />
        </Field>
        <Field label="Font family">
          <Input value={style.fontFamily ?? ''} onChange={(e) => patch({ fontFamily: e.target.value || null })} />
        </Field>
        <Field label="Font size (pt)">
          <Input type="number" value={style.fontSize ?? ''} onChange={(e) => patch({ fontSize: e.target.value === '' ? null : Number(e.target.value) })} />
        </Field>
        <Field label="Indent">
          <Input type="number" min={0} max={10} value={style.indent ?? ''} onChange={(e) => patch({ indent: e.target.value === '' ? null : Number(e.target.value) })} />
        </Field>
        <Field label="Text color">
          <Input value={style.textColor ?? ''} placeholder="#1F2937" onChange={(e) => patch({ textColor: e.target.value || null })} />
        </Field>
        <Field label="Background">
          <Input value={style.backgroundColor ?? ''} placeholder="#F3F4F6" onChange={(e) => patch({ backgroundColor: e.target.value || null })} />
        </Field>
        <Field label="Italic">
          <Switch checked={style.isItalic} onCheckedChange={(isItalic) => patch({ isItalic })} />
        </Field>
        <Field label="Border above">
          <SimpleSelect value={style.borderTop ?? NONE} onChange={(v) => patch({ borderTop: pick<ReportBorderStyle>(v) })} options={optional(BORDERS)} />
        </Field>
        <Field label="Border below">
          <SimpleSelect value={style.borderBottom ?? NONE} onChange={(v) => patch({ borderBottom: pick<ReportBorderStyle>(v) })} options={optional(BORDERS)} />
        </Field>
      </div>
      <Refusal violations={refusal} />
      <div className="flex gap-2">
        <Button size="sm" disabled={JSON.stringify(style) === JSON.stringify(initial)} onClick={() => void save()}>
          {isNew ? 'Add style' : 'Apply'}
        </Button>
        {isNew && (
          <Button size="sm" variant="outline" onClick={onDone}>
            Cancel
          </Button>
        )}
      </div>
    </section>
  )
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="flex flex-col gap-1.5">
      <Label>{label}</Label>
      {children}
    </div>
  )
}
