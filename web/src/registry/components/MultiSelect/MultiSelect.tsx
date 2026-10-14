'use client';

import * as React from 'react';
import { Check, ChevronDown, X } from 'lucide-react';
import { TruncatedText } from '@registry/components/TruncatedText';
import { Badge } from '@/components/ui/badge';
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
import { Skeleton } from '@/components/ui/skeleton';
import { cn } from '@/lib/utils';
import {
  BADGE_REMOVE_ICON_CLASSES,
  BADGE_SIZE_CLASSES,
  CHEVRON_PAD_CLASSES,
  CHEVRON_SIZE_CLASSES,
  SELECT_ALL_DEFAULT_LABEL,
  SELECT_ALL_DEFAULT_VALUE,
  TRIGGER_PAD_CLASSES,
  TRIGGER_SIZE_CLASSES,
} from './constants';
import type {
  MultiSelectOption,
  MultiSelectSize,
  MultiSelectTokens,
  SelectAllOption,
} from './types';

interface MultiSelectProps {
  options?: MultiSelectOption[];
  value: string[];
  onChange: (selected: string[]) => void;
  placeholder?: string;
  className?: string;
  contentClassName?: string;
  /** Design tokens for layout CSS variables. */
  tokens?: MultiSelectTokens;
  isLoading?: boolean;
  disabled?: boolean;
  showSelectionOrder?: boolean;
  selectAll?: SelectAllOption | true;
  renderItemLabel?: (option: MultiSelectOption) => React.ReactNode;
  renderSelectedItemLabel?: (option: MultiSelectOption) => React.ReactNode;
  onFilter?: (option: MultiSelectOption, search: string) => boolean;
  onOpenChange?: (open: boolean) => void;
  showApplyAction?: boolean;
  applyActionDisabled?: boolean;
  applyActionLabel?: string;
  onApplyAction?: () => void;
  /**
   * Maximum number of selected badges rendered before collapsing into +N.
   * Defaults to unlimited.
   */
  maxVisibleBadges?: number;
  /** Trigger density. Defaults to `default` (h-9). Aligned with Button: `xs` | `sm` | `default` | `lg`. */
  size?: MultiSelectSize;
  /** Sort options by label. `'none'` keeps the original options order. */
  sort?: 'asc' | 'desc' | 'none';
}

export function MultiSelect({
  options,
  value,
  onChange,
  placeholder = 'Select items...',
  className,
  tokens = {
    popoverWidth: 'var(--popover-width, var(--radix-popover-trigger-width))',
    popoverMaxWidth: 'var(--popover-max-width)',
  },
  isLoading = false,
  disabled = false,
  showSelectionOrder = false,
  selectAll,
  renderItemLabel,
  renderSelectedItemLabel,
  onFilter,
  onOpenChange,
  showApplyAction = false,
  applyActionDisabled = false,
  applyActionLabel = 'Apply',
  onApplyAction,
  maxVisibleBadges = Number.POSITIVE_INFINITY,
  contentClassName,
  size = 'default',
  sort = 'asc',
}: MultiSelectProps) {
  const [open, setOpen] = React.useState(false);
  const handleOpenChange = React.useCallback(
    (nextOpen: boolean) => {
      setOpen(nextOpen);
      onOpenChange?.(nextOpen);
    },
    [onOpenChange]
  );

  const containerRef = React.useRef<HTMLDivElement>(null);
  const badgeRefs = React.useRef<Map<string, HTMLDivElement>>(new Map());
  const overflowBadgeRef = React.useRef<HTMLDivElement>(null);

  const [visibleCount, setVisibleCount] = React.useState<number>(value.length);

  const sortedOptions = React.useMemo(() => {
    if (!options || sort === 'none') return options;
    return [...options].sort((a, b) => {
      const cmp = a.label.localeCompare(b.label);
      return sort === 'asc' ? cmp : -cmp;
    });
  }, [options, sort]);

  const allValues = React.useMemo(() => sortedOptions?.map(o => o.value) ?? [], [sortedOptions]);

  const showSelectAll = selectAll != null;

  const selectAllLabel =
    selectAll === true ? SELECT_ALL_DEFAULT_LABEL : (selectAll?.label ?? SELECT_ALL_DEFAULT_LABEL);

  const selectAllValue =
    selectAll === true ? SELECT_ALL_DEFAULT_VALUE : (selectAll?.value ?? SELECT_ALL_DEFAULT_VALUE);

  const allSelected =
    value.includes(selectAllValue) ||
    (allValues.length > 0 && allValues.every(v => value.includes(v)));

  const valuesToRender = React.useMemo(() => {
    return value.includes(selectAllValue) ? allValues : value;
  }, [value, allValues, selectAllValue]);

  // =========================
  // Calculate overflow badges
  // =========================
  const calculate = React.useCallback(() => {
    const container = containerRef.current;
    if (!container) return;

    const containerWidth = container.offsetWidth;
    const GAP = 4;
    const overflowBadgeWidth = overflowBadgeRef.current
      ? overflowBadgeRef.current.offsetWidth + GAP
      : 36;

    let usedWidth = 0;
    let count = 0;

    for (let i = 0; i < valuesToRender.length; i++) {
      if (count >= maxVisibleBadges) {
        break;
      }
      const val = valuesToRender[i];
      const badge = badgeRefs.current.get(val);
      if (!badge) continue;

      const badgeWidth = badge.offsetWidth + GAP;
      const remaining = valuesToRender.length - (i + 1);
      // If there are more items after this one, reserve space for the +N badge
      const spaceNeeded =
        remaining > 0 ? usedWidth + badgeWidth + overflowBadgeWidth : usedWidth + badgeWidth;

      if (spaceNeeded > containerWidth) break;

      usedWidth += badgeWidth;
      count++;
    }

    setVisibleCount(count);
  }, [maxVisibleBadges, valuesToRender]);

  React.useLayoutEffect(() => {
    calculate();
  }, [calculate]);

  React.useEffect(() => {
    const container = containerRef.current;
    if (!container) return;
    const observer = new ResizeObserver(calculate);
    observer.observe(container);
    return () => observer.disconnect();
  }, [calculate]);

  const handleUnselect = React.useCallback(
    (item: string) => {
      if (value.includes(selectAllValue)) {
        onChange(allValues.filter(v => v !== item));
      } else {
        onChange(value.filter(i => i !== item));
      }
    },
    [allValues, onChange, selectAllValue, value]
  );

  const handleSelect = (item: string) => {
    const option = sortedOptions?.find(currentOption => currentOption.value === item);
    if (option?.disabled) {
      return;
    }
    if (allSelected) {
      onChange(allValues.filter(v => v !== item));
      return;
    }
    if (value.includes(item)) {
      handleUnselect(item);
    } else {
      onChange([...value, item]);
    }
  };

  const handleSelectAll = () => {
    if (allSelected) {
      onChange([]);
    } else {
      onChange([selectAllValue]);
    }
  };

  const getItemLabel = (option: MultiSelectOption): React.ReactNode =>
    renderItemLabel ? renderItemLabel(option) : <TruncatedText text={option.label} />;

  const getSelectedItemLabel = (option: MultiSelectOption) =>
    renderSelectedItemLabel ? renderSelectedItemLabel(option) : option.label;

  const hiddenCount = valuesToRender.length - visibleCount;

  return (
    <div className={cn('w-full', className)}>
      {/*
        `modal` keeps wheel/touch scroll working when MultiSelect is nested in a Dialog.
        Dialog scroll-lock (react-remove-scroll) blocks events on PopoverContent because it
        portals to document.body as a sibling of the Dialog — not inside DialogContent.
        Same pattern as Combobox.
      */}
      <Popover open={open} onOpenChange={handleOpenChange} modal>
        <PopoverTrigger
          data-size={size}
          className={cn(
            'flex w-full transition-all items-center justify-between rounded-md border border-input bg-background outline-none',
            'focus-visible:border-ring! focus-visible:ring-[3px] focus-visible:ring-ring/50',
            'data-[state=open]:border-ring!',
            'hover:bg-[color-mix(in_oklab,var(--accent)80%,transparent)]!',
            TRIGGER_SIZE_CLASSES[size]
          )}
          disabled={disabled}
        >
          <div className="flex flex-1 overflow-hidden">
            <div
              ref={containerRef}
              className={cn(
                'flex flex-1 items-center gap-1 overflow-hidden',
                TRIGGER_PAD_CLASSES[size]
              )}
            >
              {value.length === 0 ? (
                <span className="text-muted-foreground truncate">{placeholder}</span>
              ) : (
                <>
                  {valuesToRender.map((item, index) => {
                    const option = sortedOptions?.find(o => o.value === item);
                    if (!option) return null;
                    const isHidden = index >= visibleCount;

                    return (
                      <Badge
                        key={item}
                        ref={el => {
                          if (el) badgeRefs.current.set(item, el as HTMLDivElement);
                          else badgeRefs.current.delete(item);
                        }}
                        variant="default"
                        className={cn(
                          'shrink-0 whitespace-nowrap',
                          BADGE_SIZE_CLASSES[size],
                          isHidden && 'invisible absolute'
                        )}
                        aria-hidden={isHidden}
                      >
                        {showSelectionOrder ? (
                          <span className="mr-1 inline-flex min-w-4 justify-end text-[10px] font-semibold opacity-85">
                            {index + 1}
                          </span>
                        ) : null}
                        {getSelectedItemLabel(option)}
                        <span
                          role="button"
                          tabIndex={isHidden ? -1 : 0}
                          className={cn(
                            'hover:bg-destructive rounded-full',
                            size === 'xs' ? 'ml-0.5 p-0' : 'ml-1 p-0.5'
                          )}
                          onClick={e => {
                            e.stopPropagation();
                            handleUnselect(item);
                          }}
                        >
                          <X className={BADGE_REMOVE_ICON_CLASSES[size]} />
                        </span>
                      </Badge>
                    );
                  })}

                  <Badge
                    ref={overflowBadgeRef}
                    variant="secondary"
                    className={cn(
                      'shrink-0',
                      BADGE_SIZE_CLASSES[size],
                      hiddenCount === 0 && 'invisible absolute'
                    )}
                    aria-hidden={hiddenCount === 0}
                  >
                    +{hiddenCount || 1}
                  </Badge>
                </>
              )}
            </div>

            <span
              role="button"
              onClick={e => {
                e.stopPropagation();
                handleOpenChange(!open);
              }}
              className={cn('my-auto cursor-pointer', CHEVRON_PAD_CLASSES[size])}
            >
              <ChevronDown className={cn('opacity-50', CHEVRON_SIZE_CLASSES[size])} />
            </span>
          </div>
        </PopoverTrigger>

        <PopoverContent
          align="start"
          className={cn('p-0 rounded-sm overflow-hidden', contentClassName)}
          style={{
            width: tokens?.popoverWidth,
            maxWidth: tokens?.popoverMaxWidth,
          }}
        >
          <div className="space-y-0">
            <Command
              filter={(itemValue, search) => {
                const option = sortedOptions?.find(o => o.value === itemValue);
                if (!option) return 0;
                if (onFilter) return onFilter(option, search) ? 1 : 0;
                return option.label.toLowerCase().includes(search.toLowerCase()) ? 1 : 0;
              }}
            >
              <CommandInput placeholder="Search items..." />
              <CommandList>
                <CommandEmpty className="p-0">
                  {isLoading ? (
                    <div className="p-2">
                      {Array.from({ length: 6 }).map((_, index) => (
                        <Skeleton key={index} className="h-4 w-full mb-1" />
                      ))}
                    </div>
                  ) : (
                    <div className="text-center text-sm py-4 text-muted-foreground">
                      No items found.
                    </div>
                  )}
                </CommandEmpty>

                <CommandGroup>
                  {showSelectAll && sortedOptions && sortedOptions.length > 0 && (
                    <CommandItem onSelect={handleSelectAll} value={selectAllValue}>
                      <Check
                        className={cn('mr-2 h-4 w-4', allSelected ? 'opacity-100' : 'opacity-0')}
                      />
                      {selectAllLabel}
                    </CommandItem>
                  )}

                  {sortedOptions?.map(option => {
                    const optionSelected = value.includes(option.value) || allSelected;

                    return (
                      <CommandItem
                        key={option.value}
                        value={option.value}
                        disabled={option.disabled}
                        onSelect={() => handleSelect(option.value)}
                      >
                        <Check
                          className={cn(
                            'mr-2 h-4 w-4',
                            optionSelected ? 'opacity-100' : 'opacity-0'
                          )}
                        />
                        {getItemLabel(option)}
                      </CommandItem>
                    );
                  })}
                </CommandGroup>
              </CommandList>
            </Command>
            {showApplyAction ? (
              <div className="border-t border-border p-2">
                <Button
                  type="button"
                  size="sm"
                  className="w-full"
                  disabled={applyActionDisabled}
                  onClick={event => {
                    event.preventDefault();
                    event.stopPropagation();
                    onApplyAction?.();
                    handleOpenChange(false);
                  }}
                >
                  {applyActionLabel}
                </Button>
              </div>
            ) : null}
          </div>
        </PopoverContent>
      </Popover>
    </div>
  );
}
