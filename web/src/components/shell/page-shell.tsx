import { ChevronRightIcon, type LucideIcon } from 'lucide-react'
import { Fragment, type ReactNode } from 'react'
import { cn } from '@/lib/utils'
import { UnsavedChangesBar, useChangeScope, useNavigationGuard } from './changes'

/** Module list screen: title + description + primary actions, then the page body (search, filters, full-width table). */
export function ListPage({ title, description, actions, children }: { title: string; description?: ReactNode; actions?: ReactNode; children: ReactNode }) {
  return (
    <main className="min-h-0 flex-1 overflow-auto">
      <div className="flex flex-col gap-5 p-4 md:p-6">
        <div className="flex flex-wrap items-end justify-between gap-3">
          <div className="flex flex-col gap-1">
            <h1 className="text-xl font-semibold tracking-tight">{title}</h1>
            {description && <p className="max-w-3xl text-sm text-pretty text-muted-foreground">{description}</p>}
          </div>
          {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
        </div>
        {children}
      </div>
    </main>
  )
}

/** Single-row detail header: `Section › title` breadcrumb · meta on the left, status and actions on the right. */
export function DetailHeader({ section, onBack, title, meta, children }: { section: string; onBack: () => void; title: ReactNode; meta?: ReactNode; children?: ReactNode }) {
  const guard = useNavigationGuard()
  const scope = useChangeScope()
  return (
    <header className="relative flex shrink-0 flex-col gap-2 border-b bg-card px-4 py-2.5 lg:h-14 lg:flex-row lg:items-center lg:gap-4 lg:py-0">
      <div className="flex min-w-0 items-center gap-3">
        <nav aria-label="Breadcrumb" className="min-w-0">
          <ol className="flex min-w-0 items-center gap-1.5 text-sm">
            <li className="shrink-0">
              <button
                type="button"
                onClick={() => guard(onBack)}
                className="rounded-sm text-muted-foreground outline-none transition-colors hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring"
              >
                {section}
              </button>
            </li>
            <li aria-hidden className="shrink-0 text-muted-foreground">
              <ChevronRightIcon className="size-3.5" />
            </li>
            <li aria-current="page" className="flex min-w-0 items-center gap-2">
              {title}
            </li>
          </ol>
        </nav>
        {meta && <div className="flex min-w-0 items-center gap-2">{meta}</div>}
      </div>
      {(children || scope) && (
        <div className="flex min-w-0 flex-wrap items-center gap-2 lg:ml-auto">
          {scope && (
            <>
              <UnsavedChangesBar />
              {children && <span aria-hidden className="hidden h-4 w-px bg-border lg:block" />}
            </>
          )}
          {children}
        </div>
      )}
    </header>
  )
}

export function DetailTitle({ children, mono }: { children: ReactNode; mono?: boolean }) {
  return <h1 className={cn('truncate text-sm font-semibold', mono && 'font-mono')}>{children}</h1>
}

export function DetailKey({ children }: { children: ReactNode }) {
  return <span className="hidden shrink-0 font-mono text-xs text-muted-foreground sm:inline">{children}</span>
}

/** Header + left section nav + scrolling content, shared by every editor. */
export function DetailShell({ header, nav, children, footer }: { header: ReactNode; nav: ReactNode; children: ReactNode; footer?: ReactNode }) {
  return (
    <div className="flex min-h-0 flex-1 flex-col">
      {header}
      <div className="flex min-h-0 flex-1 flex-col md:flex-row">
        {nav}
        <main className="min-h-0 min-w-0 flex-1 overflow-auto">
          <div className="flex flex-col gap-5 p-4 md:p-6">{children}</div>
        </main>
      </div>
      {footer}
    </div>
  )
}

export interface NavItem<T extends string> {
  id: T
  label: string
  icon?: LucideIcon
  badge?: string | number
  tone?: 'good' | 'bad'
  disabled?: boolean
}

export interface NavGroup<T extends string> {
  label?: string
  items: NavItem<T>[]
}

export function SectionNav<T extends string>({ label, groups, active, onSelect }: { label: string; groups: NavGroup<T>[]; active: T; onSelect: (id: T) => void }) {
  return (
    <nav aria-label={label} className="shrink-0 border-b bg-card md:w-56 md:overflow-y-auto md:border-r md:border-b-0">
      <ul className="flex gap-1 overflow-x-auto p-2 md:flex-col md:overflow-visible md:p-3">
        {groups.map((g, gi) => (
          <Fragment key={g.label ?? gi}>
            {gi > 0 && <li className="hidden md:my-2 md:block md:border-t" aria-hidden />}
            {g.label && <li className="hidden px-2.5 pb-1 text-[11px] font-medium tracking-wide text-muted-foreground uppercase md:block">{g.label}</li>}
            {g.items.map((item) => (
              <li key={item.id}>
                <NavButton item={item} active={active === item.id} onClick={() => onSelect(item.id)} />
              </li>
            ))}
          </Fragment>
        ))}
      </ul>
    </nav>
  )
}

function NavButton<T extends string>({ item, active, onClick }: { item: NavItem<T>; active: boolean; onClick: () => void }) {
  const { icon: Icon, label, badge, tone, disabled } = item
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      aria-current={active ? 'page' : undefined}
      className={cn(
        'flex w-full items-center gap-2.5 rounded-md px-2.5 py-1.5 text-sm whitespace-nowrap transition-colors disabled:pointer-events-none disabled:opacity-50',
        active ? 'bg-accent font-medium text-accent-foreground' : 'text-muted-foreground hover:bg-muted hover:text-foreground',
      )}
    >
      {Icon && <Icon aria-hidden className="size-4 shrink-0" />}
      <span className="flex-1 text-left">{label}</span>
      {badge !== undefined && badge !== '' && (
        <span
          className={cn(
            'rounded-sm px-1.5 font-mono text-[11px] tabular-nums',
            tone === 'bad' ? 'bg-destructive/15 text-destructive' : tone === 'good' ? 'bg-primary/15 text-primary' : 'text-muted-foreground',
          )}
        >
          {badge}
        </span>
      )}
    </button>
  )
}

/** Centered message used for loading / not-found states inside a module. */
export function PageMessage({ children, action }: { children: ReactNode; action?: ReactNode }) {
  return (
    <main className="flex flex-1 flex-col items-center justify-center gap-3 p-6 text-sm text-muted-foreground">
      <p>{children}</p>
      {action}
    </main>
  )
}
