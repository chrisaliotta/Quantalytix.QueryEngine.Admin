'use client';

import * as React from 'react';
import { ChevronDown } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Command,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from '@/components/ui/command';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { cn } from '@/lib/utils';
import { TruncatedText } from '../TruncatedText';
import { CHEVRON_SIZE_CLASSES, INPUT_SIZE_CLASSES, TRIGGER_SIZE_CLASSES } from './constants';
import { type ComboboxItem, type ComboboxSize, type ComboboxTokens } from './types';

interface ComboboxProps {
  data: ComboboxItem[];
  placeholder: string;
  searchPlaceholder?: string;
  emptyMessage: string;
  itemRenderer?: (item: ComboboxItem) => React.ReactNode;
  selectedItemRenderer?: (item: ComboboxItem | undefined) => React.ReactNode;
  className?: string;
  triggerClassName?: string;
  /** Design tokens applied as CSS variables on the combobox root. */
  tokens?: ComboboxTokens;
  /**
   * Controlled value. Use this when the parent owns the selected value and may
   * change it after mount — e.g. clearing the selection when a dependent field
   * resets, or reverting after a cancelled confirmation dialog. `defaultValue`
   * only seeds the initial useState and is ignored on subsequent renders, so any
   * external mutation would leave the trigger out of sync without this prop.
   */
  value?: string;
  defaultValue?: string;
  disabled?: boolean;
  onValueChange?: (value: string) => void;
  onFilter?: (item: ComboboxItem, search: string) => boolean;
  variant?: 'default' | 'outline' | 'destructive' | 'secondary' | 'ghost' | 'link';
  /** Trigger density. Defaults to `default` (h-9). Aligned with Button: `xs` | `sm` | `default` | `lg`. */
  size?: ComboboxSize;
  /** Sort items by label. `'none'` keeps the original data order. */
  sort?: 'asc' | 'desc' | 'none';
  /**
   * Custom trigger element. When provided, replaces the default value+chevron
   * button — useful for icon-only pickers (e.g. theme toggle).
   */
  trigger?: React.ReactNode;
}

export function Combobox({
  data,
  placeholder,
  searchPlaceholder = 'Search...',
  emptyMessage,
  itemRenderer = item => <TruncatedText text={item.label} />,
  selectedItemRenderer = item => item?.label,
  className,
  triggerClassName,
  tokens = {
    popoverWidth: 'var(--popover-width, var(--radix-popover-trigger-width))',
    popoverMaxWidth: 'var(--popover-max-width)',
  },
  value: controlledValue,
  defaultValue = '',
  disabled = false,
  onValueChange,
  onFilter,
  variant = 'outline',
  size = 'default',
  sort = 'asc',
  trigger,
}: ComboboxProps) {
  const [open, setOpen] = React.useState(false);
  const [internalValue, setInternalValue] = React.useState(defaultValue);
  const isControlled = controlledValue !== undefined;
  const value = isControlled ? controlledValue : internalValue;
  const selectedItemRef = React.useRef<HTMLDivElement | null>(null);

  const sortedData =
    sort === 'none'
      ? data
      : [...data].sort((a, b) => {
          const cmp = a.label.localeCompare(b.label);
          return sort === 'asc' ? cmp : -cmp;
        });

  React.useEffect(() => {
    if (!open || !value) return;

    // Wait for Popover/Command to mount & layout, then scroll selected item.
    const frameId = requestAnimationFrame(() => {
      selectedItemRef.current?.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
    });

    return () => cancelAnimationFrame(frameId);
  }, [open, value]);

  const defaultTrigger = (
    <Button
      variant={variant}
      size={size}
      role="combobox"
      aria-expanded={open}
      data-slot="combobox-trigger"
      data-size={size}
      disabled={disabled}
      className={cn(
        'w-full justify-between overflow-hidden',
        'focus-visible:border-ring! data-[state=open]:border-ring!',
        'hover:bg-[color-mix(in_oklab,var(--accent)80%,transparent)]!',
        TRIGGER_SIZE_CLASSES[size],
        triggerClassName
      )}
    >
      <div data-slot="combobox-value" className="flex-1 truncate text-left">
        {value ? (
          selectedItemRenderer(sortedData.find(item => item.value === value))
        ) : (
          <span className="text-muted-foreground">{placeholder}</span>
        )}
      </div>
      <ChevronDown className={cn('ml-2 shrink-0 opacity-50', CHEVRON_SIZE_CLASSES[size])} />
    </Button>
  );

  return (
    <div data-slot="combobox" className={cn(className)}>
      <Popover open={open} onOpenChange={setOpen} modal={true}>
        <PopoverTrigger asChild>{trigger ?? defaultTrigger}</PopoverTrigger>
        <PopoverContent
          align="start"
          className="p-0 rounded-sm overflow-hidden"
          style={{
            width: tokens?.popoverWidth,
            maxWidth: tokens?.popoverMaxWidth,
          }}
        >
          <Command
            filter={(itemValue, search) => {
              const option = sortedData.find(o => o.value === itemValue);
              if (!option) return 0;
              if (onFilter) return onFilter(option, search) ? 1 : 0;
              return option.label.toLowerCase().includes(search.toLowerCase()) ? 1 : 0;
            }}
            data-slot="combobox-command"
          >
            <CommandInput
              placeholder={searchPlaceholder}
              className={INPUT_SIZE_CLASSES[size]}
              data-slot="combobox-input"
              disabled={disabled}
            />
            <CommandList data-slot="combobox-list">
              <CommandEmpty data-slot="combobox-empty">{emptyMessage}</CommandEmpty>
              <CommandGroup data-slot="combobox-group">
                {sortedData.map(item => (
                  <CommandItem
                    key={item.value}
                    value={item.value}
                    data-slot="combobox-item"
                    data-value-selected={value === item.value}
                    className="data-[value-selected=true]:bg-accent data-[value-selected=true]:text-accent-foreground"
                    ref={value === item.value ? selectedItemRef : undefined}
                    onSelect={(currentValue: string) => {
                      if (currentValue === value) return;
                      if (!isControlled) setInternalValue(currentValue);
                      onValueChange?.(currentValue);
                      setOpen(false);
                    }}
                  >
                    {itemRenderer(item)}
                  </CommandItem>
                ))}
              </CommandGroup>
            </CommandList>
          </Command>
        </PopoverContent>
      </Popover>
    </div>
  );
}
