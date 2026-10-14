import type { ComboboxSize } from './types';

/** Extra trigger styles layered on Button's size tokens. */
export const TRIGGER_SIZE_CLASSES: Record<ComboboxSize, string> = {
  default: '',
  lg: '',
  sm: 'font-normal shadow-none',
  xs: 'font-normal shadow-none',
};

export const CHEVRON_SIZE_CLASSES: Record<ComboboxSize, string> = {
  default: 'size-4',
  lg: 'size-4',
  sm: 'size-3.5',
  xs: 'size-3',
};

export const INPUT_SIZE_CLASSES: Record<ComboboxSize, string> = {
  default: 'h-9',
  lg: 'h-10',
  sm: 'h-8',
  xs: 'h-7',
};
