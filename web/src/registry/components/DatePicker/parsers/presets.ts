import {
  getMonthEndDay,
  getQuarterEndMonthIndex,
  getQuarterFromMonth,
  resolveRelativeQuarter,
} from './utils';

function toDateOnly(date: Date) {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate());
}

function resolvePriorDay(referenceDate: Date) {
  return new Date(
    referenceDate.getFullYear(),
    referenceDate.getMonth(),
    referenceDate.getDate() - 1
  );
}

function resolvePriorMonthEnd(referenceDate: Date) {
  return new Date(referenceDate.getFullYear(), referenceDate.getMonth(), 0);
}

function resolvePriorYearEnd(referenceDate: Date) {
  return new Date(referenceDate.getFullYear() - 1, 11, 31);
}

function resolvePriorQuarterEnd(referenceDate: Date) {
  const referenceYear = referenceDate.getFullYear();
  const currentQuarter = getQuarterFromMonth(referenceDate.getMonth());
  const { quarter, year } = resolveRelativeQuarter(currentQuarter, referenceYear, 'last');
  const monthIndex = getQuarterEndMonthIndex(quarter);
  const day = getMonthEndDay(year, monthIndex);
  return new Date(year, monthIndex, day);
}

export interface DatePickerPresetOption {
  id: string;
  label: string;
  resolve: (referenceDate: Date) => Date;
}

export const DEFAULT_DATE_PICKER_PRESETS: DatePickerPresetOption[] = [
  {
    id: 'current-day',
    label: 'Current Day',
    resolve: toDateOnly,
  },
  {
    id: 'prior-day',
    label: 'Prior Day',
    resolve: resolvePriorDay,
  },
  {
    id: 'prior-month-end',
    label: 'Prior Month End',
    resolve: resolvePriorMonthEnd,
  },
  {
    id: 'prior-quarter-end',
    label: 'Prior Quarter End',
    resolve: resolvePriorQuarterEnd,
  },
  {
    id: 'prior-year-end',
    label: 'Prior Year End',
    resolve: resolvePriorYearEnd,
  },
];
