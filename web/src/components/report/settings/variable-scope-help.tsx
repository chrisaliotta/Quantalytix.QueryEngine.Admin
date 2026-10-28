import { ChevronRightIcon } from 'lucide-react'
import type { ReactNode } from 'react'

type Where = 'above' | 'scoped' | 'under' | 'outside' | 'formula'

const EXAMPLE: { row: string; depth: number; where: Where; without: string; scoped: string }[] = [
  { row: 'Total', depth: 0, where: 'above', without: '1,700', scoped: '800' },
  { row: 'Assets', depth: 1, where: 'above', without: '850', scoped: '800' },
  { row: 'Cash', depth: 2, where: 'outside', without: '50', scoped: '' },
  { row: 'Loans', depth: 2, where: 'scoped', without: '800', scoped: '800' },
  { row: 'Mortgage', depth: 3, where: 'under', without: '500', scoped: '500' },
  { row: 'Auto', depth: 3, where: 'under', without: '200', scoped: '200' },
  { row: 'Commercial', depth: 3, where: 'under', without: '100', scoped: '100' },
  { row: 'Funding', depth: 1, where: 'outside', without: '850', scoped: '' },
  { row: 'Deposits', depth: 2, where: 'outside', without: '700', scoped: '' },
  { row: 'Capital', depth: 2, where: 'outside', without: '150', scoped: '' },
  { row: 'Net interest margin', depth: 0, where: 'formula', without: '1,700', scoped: '800' },
]

const WHERE_LABEL: Record<Where, string> = {
  above: 'Above',
  scoped: 'Scoped row',
  under: 'Under',
  outside: 'Outside',
  formula: 'Formula row',
}

function Example({ title, children, defaultOpen }: { title: string; children: ReactNode; defaultOpen?: boolean }) {
  return (
    <details open={defaultOpen} className="group border-t first:border-t-0">
      <summary className="flex cursor-pointer list-none items-center gap-1.5 px-4 py-2.5 text-sm font-medium hover:bg-muted/40 [&::-webkit-details-marker]:hidden">
        <ChevronRightIcon className="size-4 shrink-0 text-muted-foreground transition-transform group-open:rotate-90" aria-hidden />
        {title}
      </summary>
      <div className="flex flex-col gap-3 px-4 pb-4 text-sm leading-relaxed text-muted-foreground">{children}</div>
    </details>
  )
}

export function VariableScopeHelp() {
  return (
    <aside aria-labelledby="scope-help-title" className="flex flex-col gap-4">
      <section className="rounded-lg border bg-card p-4">
        <h3 id="scope-help-title" className="text-sm font-semibold">
          What a scope does
        </h3>
        <p className="mt-1.5 text-sm leading-relaxed text-pretty text-muted-foreground">
          A scope ties one variable to one row, the <span className="text-foreground">scoped row</span>. It changes no
          data, only which row&apos;s value the variable reads:
        </p>
        <ul className="mt-3 flex flex-col gap-2.5 text-sm leading-relaxed">
          <li>
            <span className="font-medium">On the scoped row and every row under it:</span>{' '}
            <span className="text-muted-foreground">that row&apos;s own value.</span>
          </li>
          <li>
            <span className="font-medium">On rows above it, and on formula rows:</span>{' '}
            <span className="text-muted-foreground">the scoped row&apos;s value, instead of their own larger total.</span>
          </li>
          <li>
            <span className="font-medium">On every other row:</span>{' '}
            <span className="text-muted-foreground">
              no value, so the variable&apos;s missing setting applies (blank or 0).
            </span>
          </li>
          <li>
            <span className="font-medium">Without a scope:</span>{' '}
            <span className="text-muted-foreground">every row reads its own value.</span>
          </li>
        </ul>
        <div className="mt-3 border-t pt-3">
          <h4 className="text-xs font-medium text-muted-foreground">Applies to</h4>
          <dl className="mt-1.5 flex flex-col gap-1.5 text-sm leading-relaxed">
            <div>
              <dt className="inline font-medium">This row and rows under it:</dt>{' '}
              <dd className="inline text-muted-foreground">the rules above, unchanged.</dd>
            </div>
            <div>
              <dt className="inline font-medium">This row only:</dt>{' '}
              <dd className="inline text-muted-foreground">rows under the scoped row read no value as well.</dd>
            </div>
          </dl>
        </div>
        <p className="mt-3 border-t pt-3 text-sm leading-relaxed text-pretty text-muted-foreground">
          A scope applies under every profile. Whichever source a profile picks for the variable, the scope picks the
          row.
        </p>
      </section>

      <section aria-label="Examples" className="overflow-hidden rounded-lg border bg-card">
        <Example title="Average loan balance scoped to Loans" defaultOpen>
          <p className="text-pretty">
            Sample balances: Mortgage 500, Auto 200, Commercial 100, Cash 50, Deposits 700, Capital 150.
          </p>
          <div className="overflow-hidden rounded-md border">
            <table className="w-full text-xs">
              <thead className="border-b bg-muted/40 text-muted-foreground">
                <tr>
                  <th scope="col" className="h-8 px-2.5 text-left font-medium">
                    Row
                  </th>
                  <th scope="col" className="h-8 px-2.5 text-left font-medium">
                    Where
                  </th>
                  <th scope="col" className="h-8 px-2.5 text-right font-medium">
                    No scope
                  </th>
                  <th scope="col" className="h-8 px-2.5 text-right font-medium">
                    Scoped
                  </th>
                </tr>
              </thead>
              <tbody className="text-foreground">
                {EXAMPLE.map((r) => {
                  const inScope = r.where === 'scoped' || r.where === 'under'
                  return (
                    <tr
                      key={r.row}
                      className={`border-b last:border-b-0 ${r.where === 'scoped' ? 'bg-primary/10' : ''} ${r.where === 'formula' ? 'border-t border-t-border' : ''}`}
                    >
                      <th
                        scope="row"
                        className={`h-7 px-2.5 text-left font-normal whitespace-nowrap ${r.where === 'scoped' ? 'font-medium' : ''}`}
                        style={{ paddingLeft: `${0.625 + r.depth * 0.75}rem` }}
                      >
                        {r.where === 'formula' && (
                          <span className="mr-1 font-mono text-[10px] text-muted-foreground" aria-hidden>
                            fx
                          </span>
                        )}
                        {r.row}
                      </th>
                      <td className={`px-2.5 whitespace-nowrap ${inScope ? 'text-primary' : 'text-muted-foreground'}`}>
                        {WHERE_LABEL[r.where]}
                      </td>
                      <td className="px-2.5 text-right tabular-nums text-muted-foreground">{r.without}</td>
                      <td className="px-2.5 text-right font-medium tabular-nums">
                        {r.scoped || (
                          <span className="font-normal text-muted-foreground">
                            <span aria-hidden>{'—'}</span>
                            <span className="sr-only">blank</span>
                          </span>
                        )}
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
          <p className="text-pretty">
            So Net interest margin divides by the loan balance (800), not by every balance (1,700).
          </p>
        </Example>
      </section>
    </aside>
  )
}
