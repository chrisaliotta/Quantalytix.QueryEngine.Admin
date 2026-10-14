import type { Size } from './types';

export const SIZE_PX: Record<Size, number> = {
  xs: 12,
  sm: 16,
  md: 20,
  lg: 24,
  xl: 32,
};

export const DOTS_SIZE_CLASS: Record<Size, string> = {
  xs: 'w-1 h-1',
  sm: 'w-1.5 h-1.5',
  md: 'w-2 h-2',
  lg: 'w-2.5 h-2.5',
  xl: 'w-3 h-3',
};

export const PULSE_SIZE_CLASS: Record<Size, string> = {
  xs: 'w-3 h-3',
  sm: 'w-4 h-4',
  md: 'w-5 h-5',
  lg: 'w-6 h-6',
  xl: 'w-8 h-8',
};

export const BARS_SIZE_CLASS: Record<Size, string> = {
  xs: 'w-1 h-3',
  sm: 'w-1.5 h-4',
  md: 'w-2 h-5',
  lg: 'w-2.5 h-6',
  xl: 'w-3 h-8',
};

export const RING_SIZE_CLASS: Record<Size, string> = {
  xs: 'w-3 h-3',
  sm: 'w-4 h-4',
  md: 'w-5 h-5',
  lg: 'w-6 h-6',
  xl: 'w-8 h-8',
};

export const TEXT_SIZE_CLASS: Record<Size, string> = {
  xs: 'text-xs',
  sm: 'text-sm',
  md: 'text-sm',
  lg: 'text-base',
  xl: 'text-lg',
};
