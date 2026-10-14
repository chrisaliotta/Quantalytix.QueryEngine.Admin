import type { MultiSelectSize } from './types';

export const SELECT_ALL_DEFAULT_VALUE = '__select_all__';

export const SELECT_ALL_DEFAULT_LABEL = 'Select all';

export const TRIGGER_SIZE_CLASSES: Record<MultiSelectSize, string> = {
  default: 'h-9 text-sm',
  lg: 'h-10 text-sm',
  sm: 'h-8 gap-1.5 text-xs [&_svg]:size-3.5',
  xs: 'h-6 gap-1 text-xs [&_svg]:size-3',
};

export const TRIGGER_PAD_CLASSES: Record<MultiSelectSize, string> = {
  default: 'px-3',
  lg: 'px-4',
  sm: 'px-2.5',
  xs: 'px-2',
};

/** Selected / overflow badge density — must fit inside the trigger height. */
export const BADGE_SIZE_CLASSES: Record<MultiSelectSize, string> = {
  default: 'rounded-md text-xs px-2 py-0.5',
  lg: 'rounded-md text-xs px-2 py-0.5',
  sm: 'rounded h-5 px-1.5 py-0 text-[11px] leading-none',
  xs: 'rounded h-4 px-1 py-0 text-[10px] leading-none',
};

export const BADGE_REMOVE_ICON_CLASSES: Record<MultiSelectSize, string> = {
  default: 'h-3 w-3',
  lg: 'h-3.5 w-3.5',
  sm: 'h-2.5 w-2.5',
  xs: 'h-2.5 w-2.5',
};

export const CHEVRON_SIZE_CLASSES: Record<MultiSelectSize, string> = {
  default: 'size-4',
  lg: 'size-4',
  sm: 'size-3.5',
  xs: 'size-3',
};

export const CHEVRON_PAD_CLASSES: Record<MultiSelectSize, string> = {
  default: 'p-1 mx-1.5',
  lg: 'p-1 mx-1.5',
  sm: 'p-0.5 mx-1',
  xs: 'p-0.5 mx-1',
};
