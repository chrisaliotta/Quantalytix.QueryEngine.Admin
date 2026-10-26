import { DatabaseIcon, FileSpreadsheetIcon, MoonIcon, NetworkIcon, RocketIcon, SigmaIcon, SlidersHorizontalIcon, SunIcon, type LucideIcon } from 'lucide-react'
import { useTheme } from 'next-themes'
import { matchPath, useLocation } from 'react-router'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { paths, useAppNav } from '@/lib/navigation'
import { cn } from '@/lib/utils'

const ITEMS: { path: string; label: string; short: string; icon: LucideIcon }[] = [
  { path: paths.reports, label: 'Reports', short: 'Reports', icon: FileSpreadsheetIcon },
  { path: paths.models, label: 'Calculation models', short: 'Models', icon: SigmaIcon },
  { path: paths.datasets, label: 'Datasets', short: 'Datasets', icon: DatabaseIcon },
  { path: paths.dimensions, label: 'Dimensions', short: 'Dims', icon: NetworkIcon },
  { path: paths.releases, label: 'Releases', short: 'Releases', icon: RocketIcon },
  { path: paths.sets, label: 'Assumption sets', short: 'Sets', icon: SlidersHorizontalIcon },
]

export function AppSidebar() {
  const { pathname } = useLocation()
  const nav = useAppNav()

  return (
    <nav
      aria-label="Workspace"
      className="flex shrink-0 items-center gap-1 border-b bg-card px-2 py-1.5 md:w-16 md:flex-col md:gap-1 md:border-r md:border-b-0 md:px-1.5 md:py-3"
    >
      <div
        aria-hidden
        className="mr-2 flex size-8 items-center justify-center rounded-md bg-primary font-mono text-xs font-semibold text-primary-foreground md:mr-0 md:mb-3"
      >
        M
      </div>
      <ul className="flex gap-1 md:w-full md:flex-col">
        {ITEMS.map(({ path, label, short, icon: Icon }) => {
          const active = !!matchPath({ path, end: false }, pathname)
          return (
            <li key={path}>
              <Tooltip>
                <TooltipTrigger
                  render={
                    <button
                      type="button"
                      onClick={() => nav.go(path)}
                      aria-current={active ? 'page' : undefined}
                      aria-label={label}
                      className={cn(
                        'flex items-center gap-2 rounded-md px-2 py-1.5 text-muted-foreground outline-none hover:bg-muted hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring md:w-full md:flex-col md:gap-1 md:px-1 md:py-2',
                        active && 'bg-muted text-foreground',
                      )}
                    />
                  }
                >
                  <Icon className={cn('size-5', active && 'text-primary')} aria-hidden />
                  <span className="text-xs md:text-[10px] md:leading-none">{short}</span>
                </TooltipTrigger>
                <TooltipContent side="right">{label}</TooltipContent>
              </Tooltip>
            </li>
          )
        })}
      </ul>
      <ThemeToggle />
    </nav>
  )
}

function ThemeToggle() {
  const { resolvedTheme, setTheme } = useTheme()
  const label = 'Toggle light / dark theme'
  return (
    <Tooltip>
      <TooltipTrigger
        render={
          <button
            type="button"
            aria-label={label}
            onClick={() => setTheme(resolvedTheme === 'dark' ? 'light' : 'dark')}
            className="ml-auto flex size-9 items-center justify-center rounded-md text-muted-foreground outline-none hover:bg-muted hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring md:mt-auto md:ml-0"
          />
        }
      >
        <SunIcon className="hidden size-4 dark:block" aria-hidden />
        <MoonIcon className="size-4 dark:hidden" aria-hidden />
      </TooltipTrigger>
      <TooltipContent side="right">{label}</TooltipContent>
    </Tooltip>
  )
}
