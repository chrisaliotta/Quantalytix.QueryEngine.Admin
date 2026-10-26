import { Badge } from '@/components/ui/badge'
import { useModels } from '@/lib/model/store'
import { resolveParameter, resolveStyle, styleCss } from '@/lib/report/rowStyle'
import { useEditor } from '@/lib/report/store'
import { findRow } from '@/lib/report/tree'
import { cn } from '@/lib/utils'
import { KeyText } from '../shared/bits'

type Pair = [string, string]

const STATEMENT: { key: string; label: string; depth: number; formula?: boolean; balance?: Pair; income?: Pair; yield: Pair }[] = [
  { key: 'TOTAL', label: 'Total', depth: 0, balance: ['904,650.24', '920,000.00'], income: ['1,200.50', '1,376.40'], yield: ['1.567%', '1.766%'] },
  { key: 'CASH', label: 'Cash and equivalents', depth: 1, balance: ['214,162.56', '216,000.00'], income: ['—', '—'], yield: ['—', '—'] },
  { key: 'LOANS', label: 'Loans', depth: 1, balance: ['222,162.56', '232,000.00'], income: ['1,200.50', '1,376.40'], yield: ['6.380%', '7.004%'] },
  { key: 'MORTGAGE', label: 'Mortgage loans', depth: 2, balance: ['—', '116,000.00'], income: ['—', '573.50'], yield: ['—', '5.837%'] },
  { key: 'AUTO', label: 'Auto loans', depth: 2, balance: ['—', '69,600.00'], income: ['—', '458.80'], yield: ['—', '7.783%'] },
  { key: 'COMMERCIAL', label: 'Commercial loans', depth: 2, balance: ['—', '46,400.00'], income: ['—', '344.10'], yield: ['—', '8.756%'] },
  { key: 'DEPOSITS', label: 'Deposits', depth: 1, balance: ['230,162.56', '232,000.00'], income: ['—', '—'], yield: ['—', '—'] },
  { key: 'CAPITAL', label: 'Capital', depth: 1, balance: ['238,162.56', '240,000.00'], income: ['—', '—'], yield: ['—', '—'] },
  { key: 'nim', label: 'Net interest margin', depth: 0, formula: true, yield: ['−0.220%', '0.853%'] },
]

/** Values in bps of yield; totals are absolute, steps are deltas. */
const WATERFALL: { label: string; value: number; total?: boolean; display: string }[] = [
  { label: 'February', value: 638.19, total: true, display: '6.3819%' },
  { label: 'Income', value: 42.61, display: '+42.61 bps' },
  { label: 'Volume', value: -0.23, display: '−0.23 bps' },
  { label: 'Days', value: -42.59, display: '−42.59 bps' },
  { label: 'Residual', value: 0, display: '0.00 bps' },
  { label: 'March', value: 637.98, total: true, display: '6.3798%' },
]

const BRANCHES = [
  { branch: 'BR01', actual: '6.294%', change: '−47.71 bps', rate: '−9.87', mix: '+0.04', interaction: '+0.09' },
  { branch: 'BR02', actual: '5.920%', change: '+11.67 bps', rate: '+1.41', mix: '+0.69', interaction: '−0.07' },
  { branch: 'BR03', actual: '6.440%', change: '−72.98 bps', rate: '−21.39', mix: '+0.03', interaction: '−0.14' },
  { branch: 'BR04', actual: '6.517%', change: '−86.95 bps', rate: '−32.98', mix: '+0.22', interaction: '−0.50' },
]

export function PreviewTab() {
  return (
    <div className="mx-auto flex w-full max-w-5xl flex-col gap-8 p-6">
      <div className="flex flex-col gap-1">
        <h2 className="font-semibold">Preview</h2>
        <p className="text-sm leading-relaxed text-muted-foreground text-pretty">
          Sample output so you can see how comparisons, drivers and contribution read. The editor runs nothing; these
          numbers are illustrative.
        </p>
      </div>
      <StatementPreview />
      <div className="grid gap-8 lg:grid-cols-2">
        <WaterfallPreview />
        <BranchContribution />
      </div>
    </div>
  )
}

function PreviewSection({ title, caption, children }: { title: string; caption?: string; children: React.ReactNode }) {
  return (
    <section className="flex flex-col gap-3">
      <div className="flex flex-col gap-0.5">
        <h3 className="text-sm font-semibold">{title}</h3>
        {caption && <p className="text-xs text-muted-foreground">{caption}</p>}
      </div>
      <div className="overflow-x-auto rounded-lg border bg-card">{children}</div>
    </section>
  )
}

function PairCell({ pair }: { pair?: Pair }) {
  if (!pair) return <td className="px-3 py-2" />
  return (
    <td className="px-3 py-2 text-right font-mono text-xs tabular-nums">
      <span>{pair[0]}</span>
      <span className="text-muted-foreground"> / {pair[1]}</span>
    </td>
  )
}

function StatementPreview() {
  const { report } = useEditor()
  const { getModel } = useModels()
  const model = getModel(report.settings.model)
  const params = model?.language === '1.2' ? model.parameters : []

  const lineLook = (key: string) => {
    const row = findRow(report.rows, key)
    if (!row) return { css: {}, badges: [] as string[] }
    const css = styleCss(resolveStyle(report.styles, report.rows, row).style)
    const badges = params
      .map((p) => ({ p, v: resolveParameter(report.rows, key, p, false).value }))
      .filter(({ p, v }) => v != null && v !== (p.type === 'number' ? p.defaultNumber : p.defaultText))
      .map(({ p, v }) => `@${p.key} ${v}`)
    return { css, badges }
  }

  return (
    <PreviewSection title="March 2024, Actual vs Budget" caption="Budget is FY2024 budget, version 2. Each cell reads actual / budget.">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b text-left text-xs text-muted-foreground">
            <th scope="col" className="px-3 py-2 font-medium">Line</th>
            <th scope="col" className="px-3 py-2 text-right font-medium">Avg balance</th>
            <th scope="col" className="px-3 py-2 text-right font-medium">Interest income</th>
            <th scope="col" className="px-3 py-2 text-right font-medium">Yield</th>
          </tr>
        </thead>
        <tbody>
          {STATEMENT.map((r) => {
            const { css, badges } = lineLook(r.key)
            return (
              <tr key={r.key} className="border-b last:border-b-0" style={css}>
                <th scope="row" className="px-3 py-2 text-left font-[inherit]">
                  <span className="flex items-center gap-2" style={{ paddingLeft: `${r.depth * 1}rem` }}>
                    {r.label}
                    <KeyText className="text-xs font-normal text-muted-foreground">{r.key}</KeyText>
                    {badges.map((b) => (
                      <Badge key={b} variant="outline" className="font-mono text-[11px] font-normal text-muted-foreground">
                        {b}
                      </Badge>
                    ))}
                  </span>
                </th>
                <PairCell pair={r.balance} />
                <PairCell pair={r.income} />
                <PairCell pair={r.yield} />
              </tr>
            )
          })}
        </tbody>
      </table>
    </PreviewSection>
  )
}

function WaterfallPreview() {
  const min = 630
  const max = 685
  const pct = (v: number) => ((v - min) / (max - min)) * 100
  let running = 0

  const bars = WATERFALL.map((s) => {
    if (s.total) {
      running = s.value
      return { ...s, from: min, to: s.value }
    }
    const from = running
    running += s.value
    return { ...s, from: Math.min(from, running), to: Math.max(from, running) }
  })

  return (
    <PreviewSection title="LOANS yield, March vs February" caption="vs PM. The change is −0.21 bps. The axis starts at 6.30%.">
      <ul className="flex flex-col gap-2 p-4" aria-label="Yield waterfall">
        {bars.map((b) => (
          <li key={b.label} className="grid grid-cols-[5rem_1fr_6rem] items-center gap-3 text-sm">
            <span className={cn(b.total && 'font-medium')}>{b.label}</span>
            <span className="relative h-4 rounded-sm bg-muted" aria-hidden="true">
              <span
                className={cn(
                  'absolute inset-y-0 rounded-sm',
                  b.total ? 'bg-muted-foreground/50' : b.value >= 0 ? 'bg-primary' : 'bg-destructive',
                )}
                style={{ left: `${pct(b.from)}%`, width: `max(2px, ${pct(b.to) - pct(b.from)}%)` }}
              />
            </span>
            <span className="text-right font-mono text-xs tabular-nums">{b.display}</span>
          </li>
        ))}
      </ul>
    </PreviewSection>
  )
}

function BranchContribution() {
  return (
    <PreviewSection
      title="Branch contribution: LOANS yield vs budget"
      caption="The line's change is −62.47 bps; weighted by average balance."
    >
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b text-xs text-muted-foreground">
            <th scope="col" className="px-3 py-2 text-left font-medium">Branch</th>
            <th scope="col" className="px-3 py-2 text-right font-medium">Actual</th>
            <th scope="col" className="px-3 py-2 text-right font-medium">Change</th>
            <th scope="col" className="px-3 py-2 text-right font-medium">Rate</th>
            <th scope="col" className="px-3 py-2 text-right font-medium">Mix</th>
            <th scope="col" className="px-3 py-2 text-right font-medium">Interaction</th>
          </tr>
        </thead>
        <tbody className="font-mono text-xs tabular-nums">
          {BRANCHES.map((b) => (
            <tr key={b.branch} className="border-b">
              <th scope="row" className="px-3 py-2 text-left font-medium">{b.branch}</th>
              <td className="px-3 py-2 text-right">{b.actual}</td>
              <td className="px-3 py-2 text-right">{b.change}</td>
              <td className="px-3 py-2 text-right">{b.rate}</td>
              <td className="px-3 py-2 text-right">{b.mix}</td>
              <td className="px-3 py-2 text-right">{b.interaction}</td>
            </tr>
          ))}
        </tbody>
      </table>
      <p className="px-3 py-2 text-xs text-muted-foreground">
        {"Rate, mix and interaction are in bps. The line's residual is 0.00 bps: the parts sum to −62.47 bps."}
      </p>
    </PreviewSection>
  )
}
