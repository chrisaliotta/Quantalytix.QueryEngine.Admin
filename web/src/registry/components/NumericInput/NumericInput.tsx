import React, { useEffect, useState } from 'react';
import { isNil } from 'lodash-es';
import { ChevronDown, ChevronUp, X } from 'lucide-react';
import { Input } from '@/components/ui/input';
import { cn } from '@/lib/utils';

type NumericInputProps = Omit<React.ComponentProps<'input'>, 'type' | 'value' | 'onChange'> & {
  value?: number | string;
  step?: number;
  clearable?: boolean;
  error?: boolean;
  fractionDigits?: number;
  useGrouping?: boolean;
  prefix?: string;
  suffix?: string;
  onChange?: (value: number | null) => void;
  onClear?: () => void;
};

const NUMERIC_INPUT_PATTERN = /^-?\d*\.?\d*$/;

function parseNumericInput(raw: string): number {
  return parseFloat(stripGrouping(raw));
}

function stripGrouping(value: string): string {
  return value.replace(/,/g, '');
}

function roundToFractionDigits(value: number, fractionDigits: number): number {
  const digits = fractionDigits <= 0 ? 0 : fractionDigits;
  const factor = 10 ** digits;
  return (Math.sign(value) * Math.round(Math.abs(value) * factor)) / factor;
}

function formatNumericDisplay(
  value: number,
  fractionDigits: number | undefined,
  useGrouping: boolean
): string {
  if (!Number.isFinite(value)) return '';

  const digits = fractionDigits !== undefined && fractionDigits <= 0 ? 0 : fractionDigits;

  if (useGrouping) {
    const rounded =
      fractionDigits !== undefined ? roundToFractionDigits(value, digits ?? 0) : value;
    return new Intl.NumberFormat('en-US', {
      ...(fractionDigits !== undefined
        ? {
            minimumFractionDigits: digits,
            maximumFractionDigits: digits,
          }
        : { maximumFractionDigits: undefined }),
    }).format(rounded);
  }

  if (fractionDigits !== undefined) {
    const rounded = roundToFractionDigits(value, digits ?? 0);
    if (fractionDigits <= 0) return String(rounded);
    return rounded.toFixed(fractionDigits);
  }

  return String(value);
}

function normalizeNumericByFractionDigits(value: number, fractionDigits: number): number {
  if (!Number.isFinite(value)) return value;
  return roundToFractionDigits(value, fractionDigits <= 0 ? 0 : fractionDigits);
}

function NumericInput({
  value,
  step = 1,
  clearable = false,
  error = false,
  fractionDigits,
  useGrouping = true,
  prefix,
  suffix,
  onChange,
  onBlur,
  onClear,
  className,
  ...props
}: NumericInputProps) {
  const [isFocused, setIsFocused] = useState(false);
  const [internalValue, setInternalValue] = useState<string>(isNil(value) ? '' : String(value));

  const usesFormattedDisplay = fractionDigits !== undefined || useGrouping;

  useEffect(() => {
    if (usesFormattedDisplay) {
      if (isFocused) return;
      if (isNil(value)) {
        if (internalValue !== '') setInternalValue('');
        return;
      }
      const num = parseNumericInput(String(value));
      const nextDisplay = formatNumericDisplay(num, fractionDigits, useGrouping);

      if (nextDisplay !== internalValue) setInternalValue(nextDisplay);
      return;
    }

    const nextDisplay = isNil(value) ? '' : String(value);
    if (isNil(value)) {
      setInternalValue('');
      return;
    }
    const parsed = parseNumericInput(String(value));
    const parsedInternal = parseNumericInput(internalValue);
    if (isNaN(parsedInternal) || parsed !== parsedInternal) {
      setInternalValue(nextDisplay);
    }
  }, [value, fractionDigits, useGrouping, isFocused, internalValue, usesFormattedDisplay]);

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const raw = e.target.value;
    if (raw !== '' && raw !== '-' && !NUMERIC_INPUT_PATTERN.test(raw)) return;

    setInternalValue(raw);

    if (raw === '' || raw === '-') {
      onChange?.(null);
      return;
    }
    const num = parseFloat(raw);
    if (!isNaN(num)) onChange?.(num);
  };

  const handleBlur = (e: React.FocusEvent<HTMLInputElement>) => {
    const num = parseNumericInput(internalValue);

    if (internalValue === '' || isNaN(num)) {
      setInternalValue('');
      return;
    }

    if (fractionDigits !== undefined) {
      const normalized = normalizeNumericByFractionDigits(num, fractionDigits);
      setInternalValue(formatNumericDisplay(normalized, fractionDigits, useGrouping));
      onChange?.(normalized);
      return;
    }

    if (useGrouping) {
      setInternalValue(formatNumericDisplay(num, undefined, true));
      onChange?.(num);
      return;
    }

    setInternalValue(String(num));
    setIsFocused(false);
    onBlur?.(e);
  };

  const handleStep = (direction: 1 | -1) => {
    const current = parseNumericInput(internalValue);
    const base = isNaN(current) ? 0 : current;
    const next = parseFloat((base + direction * step).toPrecision(15));
    const normalized =
      fractionDigits !== undefined ? normalizeNumericByFractionDigits(next, fractionDigits) : next;
    const display = isFocused
      ? String(normalized)
      : usesFormattedDisplay
        ? formatNumericDisplay(normalized, fractionDigits, useGrouping)
        : String(normalized);
    setInternalValue(display);
    onChange?.(normalized);
  };

  const handleClear = () => {
    setInternalValue('');
    onChange?.(null);
    onClear?.();
  };

  const showClearButton = clearable && internalValue !== '';

  return (
    <div className="group relative">
      {prefix ? (
        <span className="text-muted-foreground pointer-events-none absolute left-3 top-1/2 z-10 -translate-y-1/2 text-sm">
          {prefix}
        </span>
      ) : null}
      <Input
        {...props}
        type="text"
        inputMode="decimal"
        value={internalValue}
        onChange={handleChange}
        onFocus={e => {
          setIsFocused(true);
          setInternalValue(prev => stripGrouping(prev));
          props.onFocus?.(e);
        }}
        onBlur={handleBlur}
        className={cn(
          prefix && 'pl-6',
          suffix && 'pr-7 group-hover:pr-8 group-focus-within:pr-8',
          !suffix && (showClearButton ? 'pr-9' : 'pr-5'),
          error ? 'border-destructive' : '',
          className
        )}
      />
      {suffix ? (
        <span
          className={cn(
            'text-muted-foreground pointer-events-none absolute right-3 top-1/2 z-10 -translate-y-1/2 text-sm transition-[right] duration-200',
            'group-hover:right-8 group-focus-within:right-8'
          )}
        >
          {suffix}
        </span>
      ) : null}
      {showClearButton && (
        <button
          type="button"
          tabIndex={-1}
          onClick={handleClear}
          className="text-muted-foreground transition-all duration-200 hover:text-foreground absolute right-3 top-1/2 -translate-y-1/2 flex items-center justify-center group-hover:right-6 group-focus-within:right-6"
          aria-label="Clear value"
        >
          <X className="h-3 w-3" />
        </button>
      )}
      <div
        data-slot="custom-numeric-input-buttons"
        className="border-input absolute right-1 top-1/2 -translate-y-1/2 flex h-3/4 w-4 border flex-col overflow-hidden rounded-r-md border-l opacity-0 transition-opacity duration-200 group-hover:opacity-100 group-focus-within:opacity-100"
      >
        <button
          type="button"
          tabIndex={-1}
          onMouseDown={e => {
            e.preventDefault();
            handleStep(1);
          }}
          className="border-input text-muted-foreground hover:bg-muted hover:text-foreground flex h-1/2 items-center justify-center border-b"
          aria-label="Increase value"
        >
          <ChevronUp className="h-full w-2.5" />
        </button>
        <button
          type="button"
          tabIndex={-1}
          onMouseDown={e => {
            e.preventDefault();
            handleStep(-1);
          }}
          className="text-muted-foreground hover:bg-muted hover:text-foreground flex h-1/2 items-center justify-center"
          aria-label="Decrease value"
        >
          <ChevronDown className="h-full w-2.5" />
        </button>
      </div>
    </div>
  );
}

export { NumericInput };
export type { NumericInputProps };
