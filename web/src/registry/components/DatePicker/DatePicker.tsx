'use client';

import * as React from 'react';
import { Dropdown as DayPickerDropdown, type DropdownProps } from 'react-day-picker';
import { format as formatDate, isValid as isValidDate, parse as parseDate } from 'date-fns';
import { CalendarIcon, CheckIcon, ChevronsDownIcon } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Calendar } from '@/components/ui/calendar';
import {
  Command,
  CommandEmpty,
  CommandGroup,
  CommandItem,
  CommandList,
} from '@/components/ui/command';
import { Input } from '@/components/ui/input';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { cn } from '@/lib/utils';
import { ValidationPopover } from '../ValidationPopover';
import {
  DEFAULT_DATE_PICKER_PRESETS,
  type DatePickerPresetOption,
  parsePeriodDate,
} from './parsers';

const DEFAULT_DATE_PICKER_FORMAT = 'MMMM dd, yyyy';

/**
 * Formats a date with locale fallback used when date-fns formatting fails.
 */
function fallbackFormatDate(date: Date) {
  return date.toLocaleDateString('en-US', {
    day: '2-digit',
    month: 'long',
    year: 'numeric',
  });
}

/**
 * Safely formats a date using a provided pattern and falls back to locale output.
 */
function safeFormatDate(date: Date, formatStr: string) {
  try {
    return formatDate(date, formatStr);
  } catch {
    return fallbackFormatDate(date);
  }
}

/**
 * Strictly parses text against the display format (e.g. "MMMM dd, yyyy") before
 * natural-language parsing is attempted, so input matching the visible format
 * round-trips exactly instead of being reinterpreted by the chrono parser.
 */
function parseDateWithFormat(text: string, formatStr: string): Date | undefined {
  try {
    const parsed = parseDate(text, formatStr, new Date());
    return isValidDate(parsed) ? parsed : undefined;
  } catch {
    return undefined;
  }
}

/**
 * Compares two Date objects using calendar-day precision.
 */
function isSameCalendarDay(left: Date, right: Date): boolean {
  return (
    left.getFullYear() === right.getFullYear() &&
    left.getMonth() === right.getMonth() &&
    left.getDate() === right.getDate()
  );
}

/**
 * Finds a preset option by case-insensitive label match.
 */
function findPresetByLabel(presets: DatePickerPresetOption[], label: string) {
  const normalizedLabel = label.trim().toLowerCase();
  return presets.find(preset => preset.label.toLowerCase() === normalizedLabel);
}

/**
 * Canonical committed value for DatePicker.
 */
export interface DatePickerValue {
  date: Date;
  presetId?: string;
}

/**
 * Inclusive date range entry for DatePicker list/range filters.
 */
export interface DatePickerFilterRange {
  from: Date;
  to: Date;
}

/**
 * Supported list/range filter entry shape.
 */
export type DatePickerFilterEntry = Date | DatePickerFilterRange;

/**
 * Date list/range filter behavior mode.
 *
 * - include-all-except: allow everything except listed entries
 * - exclude-all-except: allow only listed entries
 */
export type DatePickerFilterMode = 'include-all-except' | 'exclude-all-except';

/**
 * List/range filter contract for DatePicker constraints.
 */
export interface DatePickerDateFilter {
  mode?: DatePickerFilterMode;
  entries: DatePickerFilterEntry[];
}

interface NormalizedDatePickerFilterRange {
  from: Date;
  to: Date;
}

type DateConstraintViolation =
  | 'before-min-date'
  | 'after-max-date'
  | 'blocked-by-include-all-except'
  | 'blocked-by-exclude-all-except';

/**
 * Normalizes a Date to day precision (00:00 local time).
 */
function startOfCalendarDay(date: Date): Date {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate());
}

/**
 * Returns true when a Date has a finite timestamp.
 */
function isValidDateValue(date: Date): boolean {
  return Number.isFinite(date.getTime());
}

/**
 * Converts a filter entry into a normalized inclusive day-range.
 *
 * Invalid dates are dropped by returning undefined.
 */
function toNormalizedFilterRange(
  entry: DatePickerFilterEntry
): NormalizedDatePickerFilterRange | undefined {
  if (entry instanceof Date) {
    if (!isValidDateValue(entry)) {
      return undefined;
    }
    const day = startOfCalendarDay(entry);
    return { from: day, to: day };
  }

  if (!isValidDateValue(entry.from) || !isValidDateValue(entry.to)) {
    return undefined;
  }

  const from = startOfCalendarDay(entry.from);
  const to = startOfCalendarDay(entry.to);
  if (from.getTime() <= to.getTime()) {
    return { from, to };
  }

  return { from: to, to: from };
}

/**
 * Type guard used after map/filter normalization of filter entries.
 */
function isNormalizedFilterRange(
  value: NormalizedDatePickerFilterRange | undefined
): value is NormalizedDatePickerFilterRange {
  return Boolean(value);
}

/**
 * Returns true when a day is inside an inclusive normalized range.
 */
function isDateInRange(date: Date, range: NormalizedDatePickerFilterRange): boolean {
  const dateValue = date.getTime();
  return dateValue >= range.from.getTime() && dateValue <= range.to.getTime();
}

/**
 * Resolves the first active constraint violation for a candidate date.
 *
 * Constraint order:
 * 1. minDate / maxDate bounds
 * 2. dateFilter entries + mode
 */
function getDateConstraintViolation(params: {
  candidate: Date;
  minDate?: Date;
  maxDate?: Date;
  filterMode?: DatePickerFilterMode;
  filterRanges: NormalizedDatePickerFilterRange[];
}): DateConstraintViolation | undefined {
  const { candidate, minDate, maxDate, filterMode, filterRanges } = params;
  const normalizedCandidate = startOfCalendarDay(candidate);
  if (minDate && normalizedCandidate.getTime() < minDate.getTime()) {
    return 'before-min-date';
  }
  if (maxDate && normalizedCandidate.getTime() > maxDate.getTime()) {
    return 'after-max-date';
  }

  if (!filterMode || filterRanges.length === 0) {
    return undefined;
  }

  const inFilterRanges = filterRanges.some(range => isDateInRange(normalizedCandidate, range));
  if (filterMode === 'include-all-except' && inFilterRanges) {
    return 'blocked-by-include-all-except';
  }
  if (filterMode === 'exclude-all-except' && !inFilterRanges) {
    return 'blocked-by-exclude-all-except';
  }

  return undefined;
}

/**
 * Formats a committed DatePicker value for input/debug rendering.
 */
function formatDatePickerValue(
  value: DatePickerValue | undefined,
  formatStr: string,
  renderValue?: (value: DatePickerValue, format: string) => string
) {
  if (!value) {
    return '';
  }
  if (renderValue) {
    return renderValue(value, formatStr);
  }
  return safeFormatDate(value.date, formatStr);
}

/** Props shared by both selection modes (single date vs multiple dates). */
export type DatePickerCoreProps = {
  /** Controlled text shown in the input. */
  inputValue?: string;
  /** Initial text shown in uncontrolled mode. */
  defaultInputValue?: string;
  /** Called whenever input text changes. */
  onInputValueChange?: (value: string) => void;
  /** Date display format used whenever a committed date is rendered into the input. */
  format?: string;
  /** Optional custom formatter for committed values rendered into the input and debug summary. */
  renderValue?: (value: DatePickerValue, format: string) => string;
  /** Optional visible label for the input. */
  label?: string;
  /** Optional helper text displayed below the field. */
  description?: string;
  /** Optional placeholder text for the date input. */
  placeholder?: string;
  /** Optional id used by the input and label. */
  id?: string;
  /** Optional input name for forms. */
  name?: string;
  /** Enables optional UI variants for preset selection. */
  variant?: 'default' | 'with-presets';
  /** Custom preset options used when `variant` is set to `with-presets`. */
  presetOptions?: DatePickerPresetOption[];
  /** Optional lower bound (inclusive) for allowed selections. */
  minDate?: Date;
  /** Optional upper bound (inclusive) for allowed selections. */
  maxDate?: Date;
  /** Optional custom message when value is after `maxDate`. */
  maxDateValidationMessage?: string;
  /**
   * Optional list/range filtering for allowed dates.
   *
   * `exclude-all-except` allows only listed entries.
   * `include-all-except` allows all except listed entries.
   * When entries are provided and mode is omitted, defaults to `exclude-all-except`.
   */
  dateFilter?: DatePickerDateFilter;
  /**
   * Developer-facing option to cull year dropdown values to years represented
   * in allowed date filter entries (`exclude-all-except` mode).
   */
  cullYearOptionsToAvailableDates?: boolean;
  /**
   * Whether the picker should automatically clear its value when the current
   * selection violates an active constraint (`minDate`, `maxDate`, or `dateFilter`).
   *
   * When `true` (default), the value is silently cleared as soon as a violation
   * is detected. When `false`, the value is kept and an error border is shown
   * instead — matching the `DateRangePicker` behavior.
   */
  clearOnConstraintViolation?: boolean;
  /**
   * When `true` (default), clearing the input then blurring or pressing Enter
   * restores the previously committed value instead of clearing it.
   * Set to `false` to keep the original clear-on-empty behavior.
   */
  restoreOnEmpty?: boolean;
  /**
   * Reference date used when resolving preset options (e.g. "Current Day", "Prior Day").
   * When provided, presets are resolved relative to this date instead of today.
   * Typically set to the latest available data date so that "Current Day" resolves
   * to the most recent date with available data rather than the wall-clock date.
   */
  presetReferenceDate?: Date;
  /** Disables both input editing and calendar interactions. */
  disabled?: boolean;
  /** Additional class names for the component root. */
  className?: string;
  /** Enables debug mode. */
  isDebug?: boolean;
};

/**
 * `mode` controls how `value`, `defaultValue`, and `onValueChange` are interpreted.
 * Use `DatePickerProps<'single'>` / `DatePickerProps<'multiple'>` when you need an explicit alias.
 */
export type DatePickerProps<M extends 'single' | 'multiple' = 'single'> = DatePickerCoreProps & {
  mode?: M;
  value?: M extends 'multiple' ? Date[] : DatePickerValue | undefined;
  defaultValue?: M extends 'multiple' ? Date[] : DatePickerValue | undefined;
  onValueChange?: M extends 'multiple'
  ? (value: Date[]) => void
  : (value: DatePickerValue | undefined) => void;
};

/**
 * Text-first date picker with natural-language parsing, presets, and calendar selection.
 *
 * Supports optional date constraints via:
 * - minDate/maxDate bounds (inclusive)
 * - dateFilter list/range rules
 *
 * All selection paths (calendar, presets, typed input) share the same constraint evaluator.
 */
export function DatePicker<M extends 'single' | 'multiple' = 'single'>(props: DatePickerProps<M>) {
  const mode = props.mode ?? 'single';
  const isSingleMode = mode === 'single';

  const {
    value,
    defaultValue,
    onValueChange,
    inputValue,
    defaultInputValue,
    onInputValueChange,
    format = DEFAULT_DATE_PICKER_FORMAT,
    renderValue,
    label,
    description,
    placeholder = 'Type a date or phrase (e.g. next Friday)',
    id,
    name,
    variant = 'default',
    presetOptions,
    minDate,
    maxDate,
    maxDateValidationMessage,
    dateFilter,
    cullYearOptionsToAvailableDates = false,
    clearOnConstraintViolation = true,
    restoreOnEmpty = true,
    presetReferenceDate,
    disabled = false,
    className,
    isDebug = false,
  } = props;

  const withPresets = variant === 'with-presets';
  const presets = React.useMemo(
    () => presetOptions ?? DEFAULT_DATE_PICKER_PRESETS,
    [presetOptions]
  );
  const isValueControlled = 'value' in props;
  const isInputControlled = 'inputValue' in props;
  const generatedId = React.useId();
  const inputId = id ?? generatedId;
  const presetListId = `${inputId}-preset-list`;
  const descriptionId = description ? `${inputId}-description` : undefined;
  const parseErrorMessageId = `${inputId}-parse-error`;
  const summaryId = `${inputId}-summary`;
  const parseErrorMessage =
    'Could not parse that date phrase. Try a phrase like "next Friday" or a specific date.';

  const normalizedMinDate = React.useMemo(() => {
    if (!minDate || !isValidDateValue(minDate)) {
      return undefined;
    }
    return startOfCalendarDay(minDate);
  }, [minDate]);
  const normalizedMaxDate = React.useMemo(() => {
    if (!maxDate || !isValidDateValue(maxDate)) {
      return undefined;
    }
    return startOfCalendarDay(maxDate);
  }, [maxDate]);
  const { effectiveMinDate, effectiveMaxDate } = React.useMemo(() => {
    if (!normalizedMinDate || !normalizedMaxDate) {
      return {
        effectiveMinDate: normalizedMinDate,
        effectiveMaxDate: normalizedMaxDate,
      };
    }

    if (normalizedMinDate.getTime() <= normalizedMaxDate.getTime()) {
      return {
        effectiveMinDate: normalizedMinDate,
        effectiveMaxDate: normalizedMaxDate,
      };
    }

    return {
      effectiveMinDate: normalizedMaxDate,
      effectiveMaxDate: normalizedMinDate,
    };
  }, [normalizedMaxDate, normalizedMinDate]);
  const normalizedFilterRanges = React.useMemo(
    () => (dateFilter?.entries ?? []).map(toNormalizedFilterRange).filter(isNormalizedFilterRange),
    [dateFilter?.entries]
  );
  const calendarStartMonth = React.useMemo(
    () =>
      effectiveMinDate
        ? new Date(effectiveMinDate.getFullYear(), effectiveMinDate.getMonth(), 1)
        : undefined,
    [effectiveMinDate]
  );
  const calendarEndMonth = React.useMemo(
    () =>
      effectiveMaxDate
        ? new Date(effectiveMaxDate.getFullYear(), effectiveMaxDate.getMonth(), 1)
        : undefined,
    [effectiveMaxDate]
  );
  const minDateConstraintErrorMessage = effectiveMinDate
    ? `Date must be on or after ${safeFormatDate(effectiveMinDate, format)}.`
    : 'Date is before the minimum allowed date.';
  const maxDateConstraintErrorMessage = effectiveMaxDate
    ? (maxDateValidationMessage ??
      `Date must be on or before ${safeFormatDate(effectiveMaxDate, format)}.`)
    : 'Date is after the maximum allowed date.';
  const includeAllExceptConstraintErrorMessage =
    'That date is excluded by the current date filter.';
  const excludeAllExceptConstraintErrorMessage = 'That date is not in the allowed date filter.';
  const effectiveDateFilterMode = React.useMemo(() => {
    if (normalizedFilterRanges.length === 0) {
      return undefined;
    }
    return dateFilter?.mode ?? 'exclude-all-except';
  }, [dateFilter?.mode, normalizedFilterRanges.length]);
  const culledYearOptions = React.useMemo(() => {
    if (!cullYearOptionsToAvailableDates) {
      return undefined;
    }
    if (effectiveDateFilterMode !== 'exclude-all-except') {
      return undefined;
    }
    if (normalizedFilterRanges.length === 0) {
      return undefined;
    }

    const availableYears = new Set<number>();
    normalizedFilterRanges.forEach(range => {
      const fromYear = range.from.getFullYear();
      const toYear = range.to.getFullYear();
      const startYear = Math.min(fromYear, toYear);
      const endYear = Math.max(fromYear, toYear);
      for (let year = startYear; year <= endYear; year += 1) {
        availableYears.add(year);
      }
    });

    if (availableYears.size === 0) {
      return undefined;
    }
    return Array.from(availableYears).sort((left, right) => left - right);
  }, [cullYearOptionsToAvailableDates, effectiveDateFilterMode, normalizedFilterRanges]);
  const culledYearOptionsSet = React.useMemo(
    () => (culledYearOptions ? new Set(culledYearOptions) : undefined),
    [culledYearOptions]
  );
  const yearsDropdownComponent = React.useMemo(() => {
    if (!culledYearOptionsSet || culledYearOptionsSet.size === 0) {
      return undefined;
    }

    const YearsDropdown = (dropdownProps: DropdownProps) => {
      const filteredOptions = (dropdownProps.options ?? []).filter(option =>
        culledYearOptionsSet.has(option.value)
      );
      return (
        <DayPickerDropdown
          {...dropdownProps}
          options={filteredOptions.length > 0 ? filteredOptions : dropdownProps.options}
        />
      );
    };

    return YearsDropdown;
  }, [culledYearOptionsSet]);
  const resolveConstraintMessage = React.useCallback(
    (violation: DateConstraintViolation): string => {
      switch (violation) {
        case 'before-min-date':
          return minDateConstraintErrorMessage;
        case 'after-max-date':
          return maxDateConstraintErrorMessage;
        case 'blocked-by-include-all-except':
          return includeAllExceptConstraintErrorMessage;
        case 'blocked-by-exclude-all-except':
          return excludeAllExceptConstraintErrorMessage;
        default:
          return parseErrorMessage;
      }
    },
    [
      excludeAllExceptConstraintErrorMessage,
      includeAllExceptConstraintErrorMessage,
      maxDateConstraintErrorMessage,
      minDateConstraintErrorMessage,
      parseErrorMessage,
    ]
  );
  const resolveDateConstraintViolation = React.useCallback(
    (candidate: Date): DateConstraintViolation | undefined =>
      getDateConstraintViolation({
        candidate,
        minDate: effectiveMinDate,
        maxDate: effectiveMaxDate,
        filterMode: effectiveDateFilterMode,
        filterRanges: normalizedFilterRanges,
      }),
    [effectiveDateFilterMode, effectiveMaxDate, effectiveMinDate, normalizedFilterRanges]
  );
  const isDateAllowed = React.useCallback(
    (candidate: Date): boolean => !resolveDateConstraintViolation(candidate),
    [resolveDateConstraintViolation]
  );
  const isCalendarDateDisabled = React.useCallback(
    (candidate: Date): boolean => !isDateAllowed(candidate),
    [isDateAllowed]
  );

  const [calendarOpen, setCalendarOpen] = React.useState(false);
  const [presetOpen, setPresetOpen] = React.useState(false);
  const [fieldHasFocus, setFieldHasFocus] = React.useState(false);
  const [validationMessage, setValidationMessage] = React.useState<string | undefined>(undefined);
  const hasValidationError = Boolean(validationMessage);
  const shouldShowValidationPopover =
    hasValidationError && !fieldHasFocus && !calendarOpen && !presetOpen;
  const showDebugSummary = isDebug && !hasValidationError;
  const ariaDescribedBy =
    [
      descriptionId,
      hasValidationError ? parseErrorMessageId : undefined,
      showDebugSummary ? summaryId : undefined,
    ]
      .filter(Boolean)
      .join(' ') || undefined;
  const [uncontrolledValue, setUncontrolledValue] = React.useState<
    DatePickerValue | Date[] | undefined
  >(() => {
    if (isSingleMode && defaultValue) {
      return defaultValue as DatePickerValue | undefined;
    }
    if (defaultInputValue) {
      const initialInput = defaultInputValue.trim();
      if (!initialInput) {
        return undefined;
      }

      if (withPresets) {
        const matchingPreset = findPresetByLabel(presets, initialInput);
        if (matchingPreset) {
          return {
            date: matchingPreset.resolve(presetReferenceDate ?? new Date()),
            presetId: matchingPreset.id,
          };
        }
      }

      const parsedDate = parsePeriodDate(initialInput) ?? undefined;
      return parsedDate ? { date: parsedDate } : undefined;
    }
    return undefined;
  });
  const [uncontrolledInput, setUncontrolledInput] = React.useState(() => {
    if (defaultInputValue !== undefined) {
      return defaultInputValue;
    }
    return formatDatePickerValue(
      isSingleMode ? (defaultValue as DatePickerValue | undefined) : undefined,
      format,
      renderValue
    );
  });
  const selectedValue = isValueControlled ? value : uncontrolledValue;
  // Inline type force — single typed alias used everywhere instead of per-call casts.
  const singleValue = selectedValue as DatePickerValue | undefined;
  const multipleValue = selectedValue as Date[] | undefined;
  const selectedDate = isSingleMode ? singleValue?.date : undefined;
  const selectedPresetId = isSingleMode ? singleValue?.presetId : undefined;
  const resolvedInputValue = isInputControlled ? (inputValue ?? '') : uncontrolledInput;
  const [month, setMonth] = React.useState<Date | undefined>(selectedDate);
  const lastAutoClearedDateRef = React.useRef<number | undefined>(undefined);
  const activePreset = React.useMemo(() => {
    if (!withPresets || !selectedPresetId) {
      return undefined;
    }

    return presets.find(preset => preset.id === selectedPresetId);
  }, [presets, selectedPresetId, withPresets]);

  const setSelectedValue = React.useCallback(
    (nextValue: DatePickerValue | Date[] | undefined) => {
      if (!isValueControlled) {
        setUncontrolledValue(nextValue);
      }

      if (!isSingleMode) {
        (onValueChange as ((v: Date[]) => void) | undefined)?.(nextValue as Date[]);
        return;
      }

      const next = nextValue as DatePickerValue | undefined;
      const isSameValue =
        (singleValue === undefined && next === undefined) ||
        (singleValue !== undefined &&
          next !== undefined &&
          isSameCalendarDay(singleValue.date, next.date) &&
          singleValue.presetId === next.presetId);

      if (!isSameValue) {
        (onValueChange as ((v: DatePickerValue | undefined) => void) | undefined)?.(next);
      }
    },
    [isSingleMode, isValueControlled, onValueChange, singleValue]
  );

  const setInputText = React.useCallback(
    (nextValue: string) => {
      if (!isInputControlled) {
        setUncontrolledInput(nextValue);
      }
      onInputValueChange?.(nextValue);
    },
    [isInputControlled, onInputValueChange]
  );
  const formatCommittedValue = React.useCallback(
    (nextValue: DatePickerValue | undefined) =>
      formatDatePickerValue(nextValue, format, renderValue),
    [format, renderValue]
  );

  const syncDateFromText = React.useCallback(
    (
      rawValue: string,
      options?: {
        formatInputOnSuccess?: boolean;
        setValidationOnFailure?: boolean;
        clearDateOnEmpty?: boolean;
      }
    ) => {
      const {
        formatInputOnSuccess = true,
        setValidationOnFailure = true,
        clearDateOnEmpty = true,
      } = options ?? {};
      const nextValue = rawValue.trim();
      if (!nextValue) {
        if (restoreOnEmpty && singleValue) {
          setInputText(formatCommittedValue(singleValue));
          return;
        }
        if (clearDateOnEmpty) {
          setSelectedValue(undefined);
          if (formatInputOnSuccess) {
            setInputText('');
          }
        }
        if (setValidationOnFailure) {
          setValidationMessage(undefined);
        }
        return;
      }

      if (withPresets) {
        const matchingPreset = findPresetByLabel(presets, nextValue);
        if (matchingPreset) {
          const resolvedPresetDate = matchingPreset.resolve(presetReferenceDate ?? new Date());
          const constraintViolation = resolveDateConstraintViolation(resolvedPresetDate);
          if (constraintViolation) {
            setMonth(resolvedPresetDate);
            if (setValidationOnFailure) {
              setValidationMessage(resolveConstraintMessage(constraintViolation));
            }
            return;
          }
          setSelectedValue({
            date: resolvedPresetDate,
            presetId: matchingPreset.id,
          });
          setMonth(resolvedPresetDate);
          if (formatInputOnSuccess) {
            setInputText(
              formatCommittedValue({
                date: resolvedPresetDate,
                presetId: matchingPreset.id,
              })
            );
          }
          setValidationMessage(undefined);
          return;
        }
      }

      const parsed = parseDateWithFormat(nextValue, format) ?? parsePeriodDate(nextValue) ?? undefined;
      if (!parsed) {
        if (setValidationOnFailure) {
          setValidationMessage(parseErrorMessage);
        }
        return;
      }
      const normalizedParsedDate = startOfCalendarDay(parsed);
      const constraintViolation = resolveDateConstraintViolation(normalizedParsedDate);
      if (constraintViolation) {
        setMonth(normalizedParsedDate);
        if (setValidationOnFailure) {
          setValidationMessage(resolveConstraintMessage(constraintViolation));
        }
        return;
      }

      const preservePresetIdentity =
        withPresets &&
        Boolean(selectedPresetId) &&
        Boolean(selectedDate) &&
        selectedDate !== undefined &&
        isSameCalendarDay(normalizedParsedDate, selectedDate);
      const nextCommittedValue: DatePickerValue = preservePresetIdentity
        ? {
          date: normalizedParsedDate,
          presetId: selectedPresetId,
        }
        : {
          date: normalizedParsedDate,
        };

      setSelectedValue(nextCommittedValue);
      setMonth(normalizedParsedDate);
      if (formatInputOnSuccess) {
        setInputText(formatCommittedValue(nextCommittedValue));
      }
      setValidationMessage(undefined);
    },
    [
      format,
      formatCommittedValue,
      parseErrorMessage,
      presetReferenceDate,
      presets,
      resolveConstraintMessage,
      resolveDateConstraintViolation,
      selectedDate,
      selectedPresetId,
      setInputText,
      setSelectedValue,
      withPresets,
      restoreOnEmpty,
      singleValue,
    ]
  );

  const handlePresetSelect = React.useCallback(
    (presetId: string) => {
      const preset = presets.find(option => option.id === presetId);
      if (!preset) {
        return;
      }

      const resolvedPresetDate = preset.resolve(presetReferenceDate ?? new Date());
      const constraintViolation = resolveDateConstraintViolation(resolvedPresetDate);
      if (constraintViolation) {
        setMonth(resolvedPresetDate);
        setValidationMessage(resolveConstraintMessage(constraintViolation));
        return;
      }
      setSelectedValue({
        date: resolvedPresetDate,
        presetId: preset.id,
      });
      setMonth(resolvedPresetDate);
      setInputText(
        formatCommittedValue({
          date: resolvedPresetDate,
          presetId: preset.id,
        })
      );
      setValidationMessage(undefined);
      setPresetOpen(false);
      setCalendarOpen(false);
    },
    [
      formatCommittedValue,
      presetReferenceDate,
      presets,
      resolveConstraintMessage,
      resolveDateConstraintViolation,
      setInputText,
      setSelectedValue,
    ]
  );

  const handleCalendarSelect = React.useCallback(
    (nextDate: Date | Date[] | undefined) => {
      if (!isSingleMode) {
        setSelectedValue((nextDate as Date[] | undefined) ?? []);
        return;
      }
      const singleDate = nextDate as Date | undefined;
      if (singleDate) {
        if (selectedDate && isSameCalendarDay(singleDate, selectedDate)) {
          return;
        }
        const constraintViolation = resolveDateConstraintViolation(singleDate);
        if (constraintViolation) {
          setMonth(singleDate);
          setValidationMessage(resolveConstraintMessage(constraintViolation));
          return;
        }
        setSelectedValue({ date: singleDate });
        setInputText(formatCommittedValue({ date: singleDate }));
        setMonth(singleDate);
        setCalendarOpen(false);
      } else {
        setSelectedValue(undefined);
        setInputText('');
      }
      setValidationMessage(undefined);
    },
    [
      formatCommittedValue,
      isSingleMode,
      resolveConstraintMessage,
      resolveDateConstraintViolation,
      selectedDate,
      setInputText,
      setSelectedValue,
    ]
  );

  React.useEffect(() => {
    if (!selectedDate) {
      lastAutoClearedDateRef.current = undefined;
      return;
    }

    const constraintViolation = resolveDateConstraintViolation(selectedDate);
    if (!constraintViolation) {
      lastAutoClearedDateRef.current = undefined;
      return;
    }

    if (!clearOnConstraintViolation) {
      // Keep the value but surface the error via border.
      setValidationMessage(resolveConstraintMessage(constraintViolation));
      return;
    }

    const autoClearKey = startOfCalendarDay(selectedDate).getTime();
    if (lastAutoClearedDateRef.current === autoClearKey) {
      return;
    }
    lastAutoClearedDateRef.current = autoClearKey;
    setSelectedValue(undefined);
    setInputText('');
    setValidationMessage(undefined);
  }, [
    clearOnConstraintViolation,
    resolveConstraintMessage,
    resolveDateConstraintViolation,
    selectedDate,
    setInputText,
    setSelectedValue,
  ]);

  React.useEffect(() => {
    if (selectedDate) {
      setMonth(selectedDate);
    }
  }, [selectedDate]);
  React.useEffect(() => {
    if (!month || !culledYearOptions || culledYearOptions.length === 0) {
      return;
    }
    if (culledYearOptions.includes(month.getFullYear())) {
      return;
    }

    const nextAllowedYear =
      culledYearOptions.find(year => year >= month.getFullYear()) ??
      culledYearOptions[culledYearOptions.length - 1];
    setMonth(new Date(nextAllowedYear, 0, 1));
  }, [culledYearOptions, month]);

  React.useEffect(() => {
    if (!isSingleMode || isInputControlled || !isValueControlled) {
      return;
    }
    setUncontrolledInput(formatCommittedValue(value as DatePickerValue | undefined));
    // When clearOnConstraintViolation=false, the auto-clear effect is responsible
    // for setting/clearing the validation message based on the current date.
    // Unconditionally clearing here would erase the error border after a mode
    // switch that rebuilds the value prop reference without changing the date.
    // Only clear when we are in auto-clear mode (or there is no date to check).
    if (clearOnConstraintViolation || !(value as DatePickerValue | undefined)?.date) {
      setValidationMessage(undefined);
    } else {
      const violation = resolveDateConstraintViolation((value as DatePickerValue)?.date);
      setValidationMessage(violation ? resolveConstraintMessage(violation) : undefined);
    }
  }, [
    clearOnConstraintViolation,
    formatCommittedValue,
    isInputControlled,
    isValueControlled,
    isSingleMode,
    value,
    resolveConstraintMessage,
    resolveDateConstraintViolation,
  ]);

  const fieldClassName = cn(
    'flex min-w-0 items-center overflow-hidden rounded-md border bg-background shadow-xs transition-[color,box-shadow]',
    hasValidationError ? 'border-destructive/70' : 'border-input',
    'focus-within:border-ring focus-within:ring-ring/50 focus-within:ring-[3px]',
    hasValidationError &&
    'focus-within:border-destructive focus-within:ring-destructive/20 dark:focus-within:ring-destructive/40'
  );
  const inputClassName =
    'h-9 flex-1 rounded-none border-0 bg-transparent px-3 py-1 text-sm shadow-none focus-visible:border-0 focus-visible:ring-0 aria-invalid:border-0 aria-invalid:ring-0 dark:bg-transparent';
  const triggerButtonClassName =
    'h-9 w-9 shrink-0 rounded-none border-0 border-l border-input bg-transparent text-muted-foreground shadow-none hover:bg-accent hover:text-accent-foreground focus-visible:border-0 focus-visible:ring-0 data-[state=open]:bg-accent data-[state=open]:text-accent-foreground';

  return (
    <div
      data-slot="date-picker"
      data-variant={variant}
      className={cn('flex flex-col gap-3', className)}
    >
      {label ? (
        <label data-slot="date-picker-label" className="text-sm font-medium" htmlFor={inputId}>
          {label}
        </label>
      ) : null}

      <Popover
        modal
        open={calendarOpen}
        onOpenChange={nextOpen => {
          if (nextOpen) {
            if (isSingleMode) {
              // Sync calendar month/selection from draft text before opening.
              syncDateFromText(resolvedInputValue, {
                formatInputOnSuccess: false,
                setValidationOnFailure: false,
                clearDateOnEmpty: false,
              });
            }
            setPresetOpen(false);
          }
          setCalendarOpen(nextOpen);
        }}
      >
        <div
          data-slot="date-picker-field"
          className={fieldClassName}
          onFocusCapture={() => setFieldHasFocus(true)}
          onBlurCapture={event => {
            if (!event.currentTarget.contains(event.relatedTarget as Node | null)) {
              setFieldHasFocus(false);
            }
          }}
        >
          {!isSingleMode ? (
            <span
              data-slot="date-picker-input"
              className={cn(inputClassName, 'flex cursor-default items-center truncate')}
              onClick={() => !disabled && setCalendarOpen(o => !o)}
            >
              {!multipleValue?.length
                ? placeholder
                : multipleValue.length === 1
                  ? safeFormatDate(multipleValue[0], format)
                  : `${multipleValue.length} dates selected`}
            </span>
          ) : withPresets ? (
            <Popover
              open={presetOpen}
              onOpenChange={nextOpen => {
                if (nextOpen) {
                  setCalendarOpen(false);
                }
                setPresetOpen(nextOpen);
              }}
            >
              <div data-slot="date-picker-combobox" className="flex min-w-0 flex-1 items-center">
                <ValidationPopover
                  open={shouldShowValidationPopover}
                  message={validationMessage ?? parseErrorMessage}
                  messageId={parseErrorMessageId}
                >
                  <Input
                    id={inputId}
                    name={name}
                    type="text"
                    data-slot="date-picker-input"
                    className={inputClassName}
                    placeholder={placeholder}
                    value={resolvedInputValue}
                    disabled={disabled}
                    aria-invalid={hasValidationError || undefined}
                    aria-describedby={ariaDescribedBy}
                    role="combobox"
                    aria-expanded={presetOpen}
                    aria-controls={presetListId}
                    aria-autocomplete="list"
                    onChange={event => {
                      setInputText(event.target.value);
                      setValidationMessage(undefined);
                    }}
                    onBlur={event => {
                      syncDateFromText(event.target.value);
                    }}
                    onKeyDown={event => {
                      if (event.key === 'ArrowDown') {
                        event.preventDefault();
                        setPresetOpen(true);
                        return;
                      }
                      if (event.key === 'Enter') {
                        event.preventDefault();
                        syncDateFromText(resolvedInputValue);
                      }
                    }}
                  />
                </ValidationPopover>
                <PopoverTrigger asChild>
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    data-slot="date-picker-combobox-trigger"
                    className={triggerButtonClassName}
                    disabled={disabled || presets.length === 0}
                    aria-label={
                      activePreset
                        ? `Open presets (current: ${activePreset.label})`
                        : 'Open date presets'
                    }
                  >
                    <ChevronsDownIcon className="size-3.5 opacity-70" />
                  </Button>
                </PopoverTrigger>
              </div>
              <PopoverContent align="end" className="w-56 p-0">
                <Command shouldFilter={false}>
                  <CommandList id={presetListId}>
                    <CommandEmpty>No preset options available.</CommandEmpty>
                    <CommandGroup heading="Quick picks">
                      {presets.map(option => (
                        <CommandItem
                          key={option.id}
                          value={option.label}
                          onSelect={() => handlePresetSelect(option.id)}
                        >
                          <CheckIcon
                            className={cn(
                              'size-4',
                              activePreset?.id === option.id ? 'opacity-100' : 'opacity-0'
                            )}
                          />
                          {option.label}
                        </CommandItem>
                      ))}
                    </CommandGroup>
                  </CommandList>
                </Command>
              </PopoverContent>
            </Popover>
          ) : (
            <ValidationPopover
              open={shouldShowValidationPopover}
              message={validationMessage ?? parseErrorMessage}
              messageId={parseErrorMessageId}
            >
              <Input
                id={inputId}
                name={name}
                type="text"
                data-slot="date-picker-input"
                className={inputClassName}
                placeholder={placeholder}
                value={resolvedInputValue}
                disabled={disabled}
                aria-invalid={hasValidationError || undefined}
                aria-describedby={ariaDescribedBy}
                onChange={event => {
                  setInputText(event.target.value);
                  setValidationMessage(undefined);
                }}
                onBlur={event => {
                  syncDateFromText(event.target.value);
                }}
                onKeyDown={event => {
                  if (event.key === 'Enter') {
                    event.preventDefault();
                    syncDateFromText(resolvedInputValue);
                  }
                }}
              />
            </ValidationPopover>
          )}
          <PopoverTrigger asChild>
            <Button
              type="button"
              variant="ghost"
              size="icon"
              data-slot="date-picker-trigger"
              className={triggerButtonClassName}
              disabled={disabled}
              aria-label="Open calendar"
            >
              <CalendarIcon className="size-4" />
            </Button>
          </PopoverTrigger>
        </div>

        <PopoverContent align="end" className="w-auto p-0">
          <Calendar
            mode={mode}
            required={false}
            captionLayout="dropdown"
            selected={(isSingleMode ? selectedDate : (multipleValue ?? [])) as never}
            modifiers={isSingleMode ? { locked_selected: selectedDate } : undefined}
            month={month}
            onMonthChange={setMonth}
            onSelect={handleCalendarSelect as never}
            disabled={isCalendarDateDisabled}
            startMonth={calendarStartMonth}
            endMonth={calendarEndMonth}
            components={
              yearsDropdownComponent ? { YearsDropdown: yearsDropdownComponent } : undefined
            }
          />
        </PopoverContent>
      </Popover>

      {description ? (
        <p
          id={descriptionId}
          data-slot="date-picker-description"
          className="text-muted-foreground px-1 text-sm"
        >
          {description}
        </p>
      ) : null}

      {showDebugSummary && (
        <div
          id={summaryId}
          data-slot="date-picker-summary"
          aria-live="polite"
          className="text-muted-foreground px-1 text-sm"
        >
          {selectedDate ? (
            <>
              Date will be set to{' '}
              <span className="font-medium">
                {singleValue ? formatCommittedValue(singleValue) : ''}
              </span>
              .
              {activePreset ? (
                <>
                  {' '}
                  Preset: <span className="font-medium">{activePreset.label}</span>.
                </>
              ) : null}
            </>
          ) : (
            'No date selected.'
          )}
        </div>
      )}
    </div>
  );
}
