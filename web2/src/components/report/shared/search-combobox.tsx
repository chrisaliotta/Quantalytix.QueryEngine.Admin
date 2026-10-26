import { useState } from 'react'
import { CheckIcon, ChevronsUpDownIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { cn } from '@/lib/utils'

export interface ComboOption {
  value: string
  label: string
  hint?: string
  description?: string
  group?: string
  /** Tree depth; indents the option while the list is unfiltered. */
  depth?: number
  /** Ancestor labels, shown instead of indentation once a search flattens the tree. */
  path?: string
}

interface SearchComboboxProps {
  value: string
  onChange: (value: string) => void
  options: ComboOption[]
  placeholder?: string
  searchPlaceholder?: string
  id?: string
  className?: string
  /** Caps rendered matches so dimensions with thousands of members stay responsive; search narrows the rest. */
  limit?: number
  'aria-label'?: string
}

export function SearchCombobox({
  value,
  onChange,
  options,
  placeholder = 'Choose…',
  searchPlaceholder = 'Search…',
  id,
  className,
  limit,
  'aria-label': ariaLabel,
}: SearchComboboxProps) {
  const [open, setOpen] = useState(false)
  const [query, setQuery] = useState('')
  const selected = options.find((o) => o.value === value)

  const q = query.trim().toLowerCase()
  const matches = limit && q ? options.filter((o) => o.label.toLowerCase().includes(q) || o.value.toLowerCase().includes(q)) : options
  const shown = limit ? matches.slice(0, limit) : matches
  const hidden = matches.length - shown.length
  const groups = [...new Set(shown.map((o) => o.group ?? ''))]

  return (
    <Popover
      open={open}
      onOpenChange={(next) => {
        setOpen(next)
        if (!next) setQuery('')
      }}
    >
      <PopoverTrigger
        render={
          <Button
            id={id}
            variant="outline"
            role="combobox"
            aria-expanded={open}
            aria-label={ariaLabel}
            className={cn('w-full min-w-0 justify-between font-normal', !selected && 'text-muted-foreground', className)}
          />
        }
      >
        <span className="flex min-w-0 items-center gap-2">
          <span className="truncate">{selected ? selected.label : placeholder}</span>
          {selected?.hint && <span className="truncate font-mono text-xs text-muted-foreground">{selected.hint}</span>}
        </span>
        <ChevronsUpDownIcon className="shrink-0 text-muted-foreground" />
      </PopoverTrigger>
      <PopoverContent
        className={cn(
          'w-(--anchor-width) min-w-64 p-0',
          (options.some((o) => o.depth !== undefined) || options.some((o) => o.hint)) && 'w-max max-w-md min-w-80',
        )}
        align="start"
      >
        <Command shouldFilter={!limit}>
          <CommandInput placeholder={searchPlaceholder} value={query} onValueChange={setQuery} />
          <CommandList>
            <CommandEmpty>No matches.</CommandEmpty>
            {groups.map((g) => (
              <CommandGroup key={g || 'all'} heading={g || undefined}>
                {shown
                  .filter((o) => (o.group ?? '') === g)
                  .map((o) => (
                    <CommandItem
                      key={o.value}
                      value={`${o.label} ${o.value}`}
                      onSelect={() => {
                        onChange(o.value)
                        setOpen(false)
                      }}
                    >
                      <CheckIcon className={cn(o.value === value ? 'opacity-100' : 'opacity-0')} />
                      {o.depth !== undefined ? (
                        <span
                          className="flex min-w-0 flex-1 items-center gap-2"
                          style={q ? undefined : { paddingLeft: `${o.depth * 1.25}rem` }}
                        >
                          {!q && o.depth > 0 && <span aria-hidden className="h-4 w-px shrink-0 bg-border" />}
                          <span className={cn('truncate', o.depth === 0 && !q && 'font-medium')}>{o.label}</span>
                          {q && o.path && <span className="ml-auto truncate text-xs text-muted-foreground">{o.path}</span>}
                          {o.hint && <span className="ml-auto font-mono text-xs text-muted-foreground">{o.hint}</span>}
                        </span>
                      ) : o.description ? (
                        <span className="flex min-w-0 flex-col">
                          <span className="flex items-center gap-2">
                            <span className="truncate">{o.label}</span>
                            {o.hint && <span className="ml-auto font-mono text-xs text-muted-foreground">{o.hint}</span>}
                          </span>
                          <span className="font-mono text-[11px] leading-snug break-all text-muted-foreground">{o.description}</span>
                        </span>
                      ) : (
                        <>
                          <span className="truncate">{o.label}</span>
                          {o.hint && <span className="ml-auto font-mono text-xs text-muted-foreground">{o.hint}</span>}
                        </>
                      )}
                    </CommandItem>
                  ))}
              </CommandGroup>
            ))}
          </CommandList>
          {hidden > 0 && (
            <p className="border-t px-3 py-2 text-xs text-muted-foreground">
              Showing {shown.length} of {matches.length.toLocaleString()}. Type to narrow the list.
            </p>
          )}
        </Command>
      </PopoverContent>
    </Popover>
  )
}
