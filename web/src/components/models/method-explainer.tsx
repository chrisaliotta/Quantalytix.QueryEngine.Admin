import { InfoIcon } from 'lucide-react'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import type { AttributionMethod } from '@/lib/model/types'
import { cn } from '@/lib/utils'

export const METHOD_LABEL: Record<AttributionMethod, string> = { shapley: 'Shapley', sequential: 'Sequential' }

/** Interest income = balance × rate; balance 10 → 12, rate 0.40 → 0.45. */
export const EXAMPLE = {
  start: 4,
  end: 5.4,
  split: {
    shapley: [
      { key: 'volume', label: 'Volume', value: 0.85 },
      { key: 'rate', label: 'Rate', value: 0.55 },
    ],
    sequential: [
      { key: 'volume', label: 'Volume', value: 0.8 },
      { key: 'rate', label: 'Rate', value: 0.6 },
    ],
  },
} as const

export function MethodExplainer() {
  return (
    <Popover>
      <PopoverTrigger
        aria-label="How the attribution methods differ"
        className="inline-flex size-6 items-center justify-center rounded-md text-muted-foreground outline-none hover:bg-muted hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring"
      >
        <InfoIcon className="size-4" />
      </PopoverTrigger>
      <PopoverContent className="w-96 text-sm">
        <dl className="flex flex-col gap-3">
          <div>
            <dt className="font-medium">Shapley</dt>
            <dd className="text-pretty text-muted-foreground">
              Gives each driver its effect averaged over every order of switching the drivers, so the result doesn&apos;t depend on order.
            </dd>
          </div>
          <div>
            <dt className="font-medium">Sequential</dt>
            <dd className="text-pretty text-muted-foreground">Switches drivers one at a time, in their listed order.</dd>
          </div>
        </dl>
        <div className="mt-3 flex flex-col gap-2 border-t pt-3">
          <p className="text-pretty text-muted-foreground">
            Interest income as balance × rate: balance 10 → 12, rate 0.40 → 0.45, so the change is{' '}
            <span className="font-mono text-foreground">1.40</span>.
          </p>
          <table className="w-full text-left text-xs">
            <thead className="text-muted-foreground">
              <tr>
                <th className="py-1 font-medium">Method</th>
                <th className="py-1 text-right font-medium">Volume</th>
                <th className="py-1 text-right font-medium">Rate</th>
              </tr>
            </thead>
            <tbody className="font-mono">
              <tr>
                <td className="py-1 font-sans">Shapley</td>
                <td className="py-1 text-right">0.85</td>
                <td className="py-1 text-right">0.55</td>
              </tr>
              <tr>
                <td className="py-1 font-sans">Sequential, volume first</td>
                <td className="py-1 text-right">0.80</td>
                <td className="py-1 text-right">0.60</td>
              </tr>
            </tbody>
          </table>
        </div>
      </PopoverContent>
    </Popover>
  )
}

interface Bar {
  key: string
  label: string
  value: number
}

/** A small horizontal waterfall: start, one bar per driver, end. */
export function MiniWaterfall({ start, end, bars, className }: { start: number; end: number; bars: readonly Bar[]; className?: string }) {
  const max = Math.max(start, end, ...bars.map((_, i) => start + bars.slice(0, i + 1).reduce((s, b) => s + b.value, 0)))
  const pct = (v: number) => `${(v / max) * 100}%`
  let running = start
  const rows = [
    { key: 'start', label: 'Before', from: 0, to: start, kind: 'total' as const, value: start },
    ...bars.map((b) => {
      const from = running
      running += b.value
      return { key: b.key, label: b.label, from: Math.min(from, running), to: Math.max(from, running), kind: b.value >= 0 ? ('up' as const) : ('down' as const), value: b.value }
    }),
    { key: 'end', label: 'After', from: 0, to: end, kind: 'total' as const, value: end },
  ]
  return (
    <ol className={cn('flex flex-col gap-1.5', className)}>
      {rows.map((r) => (
        <li key={r.key} className="grid grid-cols-[5.5rem_1fr_3.5rem] items-center gap-2 text-xs">
          <span className="truncate text-muted-foreground">{r.label}</span>
          <span className="relative h-4 rounded-sm bg-muted/60">
            <span
              className={cn(
                'absolute inset-y-0 rounded-sm',
                r.kind === 'total' && 'bg-muted-foreground/50',
                r.kind === 'up' && 'bg-primary',
                r.kind === 'down' && 'bg-destructive',
              )}
              style={{ left: pct(r.from), width: pct(r.to - r.from) }}
            />
          </span>
          <span className="text-right font-mono tabular-nums">
            {r.kind === 'total' ? r.value.toFixed(2) : `${r.value >= 0 ? '+' : '−'}${Math.abs(r.value).toFixed(2)}`}
          </span>
        </li>
      ))}
    </ol>
  )
}
