import { PlusIcon, Trash2Icon } from 'lucide-react'
import { useState, type ReactNode } from 'react'
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
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Sheet, SheetContent, SheetDescription, SheetFooter, SheetHeader, SheetTitle } from '@/components/ui/sheet'
import { Switch } from '@/components/ui/switch'
import { SimpleSelect } from '@/components/report/shared/simple-select'
import { BORDERS, ROLES, WEIGHTS, styleCss, styleUsers } from '@/lib/report/rowStyle'
import { useEditor } from '@/lib/report/store'
import type { BorderStyle, FontWeight, ReportStyle, RowRole } from '@/lib/report/types'

const KEY = /^[A-Za-z][A-Za-z0-9_]*$/
const HEX = /^#[0-9A-Fa-f]{6}$/
const SIZE = /^\d+(\.\d{1,2})?$/
const NONE = '__none'

function Th({ children, className }: { children?: ReactNode; className?: string }) {
  return <th className={`px-3 py-2 text-left text-xs font-medium text-muted-foreground ${className ?? ''}`}>{children}</th>
}

export function StylesTab() {
  const { report, mutate } = useEditor()
  const [sheet, setSheet] = useState<{ open: boolean; style: ReportStyle | null }>({ open: false, style: null })
  const [deleting, setDeleting] = useState<ReportStyle | null>(null)
  const [refusal, setRefusal] = useState<string | null>(null)

  const confirmDelete = () => {
    if (!deleting) return
    const users = styleUsers(report.rows, deleting.key)
    if (users.length) {
      setRefusal(`Style '${deleting.key}' is used by row${users.length > 1 ? 's' : ''} ${users.map((r) => r.key).join(', ')}, so it can't be deleted.`)
    } else {
      mutate((d) => void (d.styles = d.styles.filter((s) => s.key !== deleting.key)))
    }
    setDeleting(null)
  }

  return (
    <div className="mx-auto flex w-full max-w-5xl flex-col gap-4 p-6">
      <div className="flex items-start justify-between gap-4">
        <div className="flex flex-col gap-1">
          <h2 className="text-lg font-semibold">Styles</h2>
          <p className="text-sm text-pretty text-muted-foreground">Named looks for rows. Each role can have one default style; a row can pick its own instead.</p>
        </div>
        <Button size="sm" onClick={() => setSheet({ open: true, style: null })}>
          <PlusIcon /> Add style
        </Button>
      </div>

      {refusal && (
        <div role="alert" className="flex items-start justify-between gap-3 rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm">
          <p>
            <span className="font-medium text-destructive">The server refused this change. </span>
            {refusal}
          </p>
          <Button variant="ghost" size="sm" className="h-auto px-1.5 py-0.5" onClick={() => setRefusal(null)}>
            Dismiss
          </Button>
        </div>
      )}

      <div className="overflow-x-auto rounded-lg border bg-card">
        <table className="w-full text-sm">
          <thead className="border-b">
            <tr>
              <Th>Key</Th>
              <Th>Name</Th>
              <Th>Default for</Th>
              <Th>Used by</Th>
              <Th className="w-10">
                <span className="sr-only">Actions</span>
              </Th>
            </tr>
          </thead>
          <tbody className="divide-y">
            {report.styles.map((s) => {
              const users = styleUsers(report.rows, s.key)
              return (
                <tr key={s.key} className="cursor-pointer hover:bg-muted/40" onClick={() => setSheet({ open: true, style: s })}>
                  <td className="px-3 py-2.5 font-mono text-xs">{s.key}</td>
                  <td className="px-3 py-2.5">
                    <span className="inline-block px-1.5 py-0.5" style={styleCss(s)}>
                      {s.name}
                    </span>
                  </td>
                  <td className="px-3 py-2.5 text-muted-foreground">{s.defaultRole ?? '—'}</td>
                  <td className="px-3 py-2.5 font-mono text-xs text-muted-foreground">{users.length ? users.map((r) => r.key).join(', ') : '—'}</td>
                  <td className="px-3 py-2.5">
                    <Button
                      variant="ghost"
                      size="icon-sm"
                      aria-label={`Delete style ${s.key}`}
                      onClick={(e) => {
                        e.stopPropagation()
                        setDeleting(s)
                      }}
                    >
                      <Trash2Icon />
                    </Button>
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>

      <Sheet open={sheet.open} onOpenChange={(o) => !o && setSheet({ open: false, style: null })}>
        <SheetContent className="w-full gap-0 p-0 data-[side=right]:sm:max-w-2xl">
          {sheet.open && (
            <StyleForm
              key={sheet.style?.key ?? 'new'}
              style={sheet.style}
              styles={report.styles}
              onClose={() => setSheet({ open: false, style: null })}
              onSave={(s) =>
                mutate((d) => {
                  const i = d.styles.findIndex((x) => x.key === s.key)
                  if (i >= 0) d.styles[i] = s
                  else d.styles.push(s)
                })
              }
            />
          )}
        </SheetContent>
      </Sheet>

      <AlertDialog open={!!deleting} onOpenChange={(o) => !o && setDeleting(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete style {deleting?.key}?</AlertDialogTitle>
            <AlertDialogDescription>Rows can&apos;t be using it. Roles that defaulted to it fall back to the viewer&apos;s own look.</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction variant="destructive" onClick={confirmDelete}>
              Delete
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  )
}

function Field({ id, label, error, help, children }: { id: string; label: string; error?: string | null; help?: string; children: ReactNode }) {
  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor={id}>{label}</Label>
      {children}
      {error ? <p className="text-xs text-destructive">{error}</p> : help && <p className="text-xs text-muted-foreground">{help}</p>}
    </div>
  )
}

function ColorField({ id, label, value, onChange, error }: { id: string; label: string; value: string; onChange: (v: string) => void; error?: string | null }) {
  return (
    <Field id={id} label={label} error={error}>
      <div className="flex items-center gap-2">
        <input
          type="color"
          aria-label={`${label} picker`}
          value={HEX.test(value) ? value : '#000000'}
          onChange={(e) => onChange(e.target.value.toUpperCase())}
          className="size-9 shrink-0 cursor-pointer rounded-md border bg-transparent p-1"
        />
        <Input id={id} value={value} onChange={(e) => onChange(e.target.value)} placeholder="#RRGGBB" className="font-mono uppercase" />
        {value && (
          <Button type="button" variant="ghost" size="sm" onClick={() => onChange('')}>
            Clear
          </Button>
        )}
      </div>
    </Field>
  )
}

function Preview({ style }: { style: ReportStyle }) {
  const rows: [string, string, boolean][] = [
    ['Row above', '1,204.50', false],
    ['Styled row', '12,345.67', true],
    ['Row below', '980.25', false],
  ]
  return (
    <div className="overflow-hidden rounded-md border bg-background text-sm" aria-label="Style preview">
      {rows.map(([label, amount, styled]) => (
        <div key={label} className="flex items-center justify-between gap-4 px-3 py-1.5" style={styled ? styleCss(style) : undefined}>
          <span style={styled && style.indent ? { paddingLeft: `${style.indent * 1.25}rem` } : undefined}>{label}</span>
          <span className="font-mono tabular-nums">{amount}</span>
        </div>
      ))}
    </div>
  )
}

function StyleForm({ style, styles, onClose, onSave }: { style: ReportStyle | null; styles: ReportStyle[]; onClose: () => void; onSave: (s: ReportStyle) => void }) {
  const [key, setKey] = useState(style?.key ?? '')
  const [name, setName] = useState(style?.name ?? '')
  const [role, setRole] = useState<string>(style?.defaultRole ?? NONE)
  const [fontFamily, setFontFamily] = useState(style?.fontFamily ?? '')
  const [fontSize, setFontSize] = useState(style?.fontSize != null ? String(style.fontSize) : '')
  const [weight, setWeight] = useState<string>(style?.fontWeight ?? NONE)
  const [italic, setItalic] = useState(style?.italic ?? false)
  const [textColor, setTextColor] = useState(style?.textColor ?? '')
  const [bgColor, setBgColor] = useState(style?.backgroundColor ?? '')
  const [borderTop, setBorderTop] = useState<string>(style?.borderTop ?? NONE)
  const [borderBottom, setBorderBottom] = useState<string>(style?.borderBottom ?? NONE)
  const [indent, setIndent] = useState(style?.indent != null ? String(style.indent) : '')
  const [submitted, setSubmitted] = useState(false)

  const roleOwner = role !== NONE ? styles.find((s) => s.defaultRole === role && s.key !== style?.key) : undefined
  const sizeNum = Number(fontSize)
  const indentNum = Number(indent)

  const errors = {
    key: style ? null : !key ? 'Enter a key.' : key.length > 128 ? 'At most 128 characters.' : !KEY.test(key) ? 'Start with a letter, then letters, digits or _.' : styles.some((s) => s.key === key) ? 'This key is already used.' : null,
    name: !name.trim() ? 'Enter a name.' : null,
    role: roleOwner ? `Style '${roleOwner.key}' is already the default for ${role}.` : null,
    fontFamily: fontFamily.length > 128 ? 'At most 128 characters.' : null,
    fontSize: fontSize && (!SIZE.test(fontSize) || sizeNum < 6 || sizeNum > 72) ? 'Use 6 to 72 points, up to 2 decimals.' : null,
    textColor: textColor && !HEX.test(textColor) ? 'Use #RRGGBB.' : null,
    bgColor: bgColor && !HEX.test(bgColor) ? 'Use #RRGGBB.' : null,
    indent: indent && (!/^\d+$/.test(indent) || indentNum > 10) ? 'Use a whole number from 0 to 10.' : null,
  }
  const invalid = Object.values(errors).some(Boolean)

  const draft: ReportStyle = {
    key: style?.key ?? key,
    name: name.trim() || 'Styled row',
    defaultRole: role === NONE ? null : (role as RowRole),
    fontFamily: fontFamily.trim() || undefined,
    fontSize: fontSize && !errors.fontSize ? sizeNum : undefined,
    fontWeight: weight === NONE ? undefined : (weight as FontWeight),
    italic,
    textColor: textColor && !errors.textColor ? textColor.toUpperCase() : undefined,
    backgroundColor: bgColor && !errors.bgColor ? bgColor.toUpperCase() : undefined,
    borderTop: borderTop === NONE ? undefined : (borderTop as BorderStyle),
    borderBottom: borderBottom === NONE ? undefined : (borderBottom as BorderStyle),
    indent: indent && !errors.indent ? indentNum : undefined,
  }

  const borderOptions = [{ value: NONE, label: 'Viewer default' }, ...BORDERS.map((b) => ({ value: b, label: b }))]

  return (
    <form
      className="flex h-full min-h-0 flex-col"
      onSubmit={(e) => {
        e.preventDefault()
        setSubmitted(true)
        if (invalid) return
        onSave(draft)
        onClose()
      }}
    >
      <SheetHeader className="border-b p-4">
        <SheetTitle>{style ? `Edit style ${style.key}` : 'Add style'}</SheetTitle>
        <SheetDescription>Any field left empty keeps the viewer&apos;s own default.</SheetDescription>
      </SheetHeader>
      <div className="border-b bg-muted/30 p-4">
        <Preview style={draft} />
      </div>
      <div className="grid min-h-0 flex-1 grid-cols-1 content-start gap-5 overflow-y-auto p-4 sm:grid-cols-2">
        <Field id="st-key" label="Key" error={submitted ? errors.key : null} help={style ? "A style's key can't be changed." : undefined}>
          <Input id="st-key" value={key} disabled={!!style} onChange={(e) => setKey(e.target.value)} className="font-mono" placeholder="highlight" autoFocus={!style} />
        </Field>
        <Field id="st-name" label="Name" error={submitted ? errors.name : null}>
          <Input id="st-name" value={name} onChange={(e) => setName(e.target.value)} placeholder="Highlight" />
        </Field>
        <Field id="st-role" label="Default for" error={errors.role}>
          <SimpleSelect id="st-role" value={role} onChange={setRole} options={[{ value: NONE, label: 'No role' }, ...ROLES.map((r) => ({ value: r, label: r }))]} />
        </Field>
        <Field id="st-font" label="Font family" error={errors.fontFamily}>
          <Input id="st-font" value={fontFamily} onChange={(e) => setFontFamily(e.target.value)} placeholder="The client's own" />
        </Field>
        <Field id="st-size" label="Font size (pt)" error={errors.fontSize}>
          <Input id="st-size" inputMode="decimal" value={fontSize} onChange={(e) => setFontSize(e.target.value)} className="font-mono tabular-nums" placeholder="6 – 72" />
        </Field>
        <Field id="st-weight" label="Weight">
          <SimpleSelect id="st-weight" value={weight} onChange={setWeight} options={[{ value: NONE, label: 'Viewer default' }, ...WEIGHTS.map((w) => ({ value: w, label: w }))]} />
        </Field>
        <div className="flex items-center justify-between gap-3 rounded-md border px-3 py-2 sm:col-span-2">
          <Label htmlFor="st-italic">Italic</Label>
          <Switch id="st-italic" checked={italic} onCheckedChange={setItalic} />
        </div>
        <ColorField id="st-text" label="Text color" value={textColor} onChange={setTextColor} error={errors.textColor} />
        <ColorField id="st-bg" label="Background color" value={bgColor} onChange={setBgColor} error={errors.bgColor} />
        <Field id="st-top" label="Rule above">
          <SimpleSelect id="st-top" value={borderTop} onChange={setBorderTop} options={borderOptions} />
        </Field>
        <Field id="st-bottom" label="Rule below">
          <SimpleSelect id="st-bottom" value={borderBottom} onChange={setBorderBottom} options={borderOptions} />
        </Field>
        <Field id="st-indent" label="Indent" error={errors.indent} help="Levels added to the row's depth, 0 to 10.">
          <Input id="st-indent" inputMode="numeric" value={indent} onChange={(e) => setIndent(e.target.value)} className="font-mono tabular-nums" placeholder="0" />
        </Field>
      </div>
      <SheetFooter className="flex-row justify-end border-t p-4">
        <Button type="button" variant="outline" onClick={onClose}>
          Cancel
        </Button>
        <Button type="submit">{style ? 'Save style' : 'Add style'}</Button>
      </SheetFooter>
    </form>
  )
}
