import type { ReactNode } from 'react';

export type Variant = 'spinner' | 'dots' | 'pulse' | 'bars';

export type Size = 'xs' | 'sm' | 'md' | 'lg' | 'xl';

export type LoadingProps = {
  /** The variant of loading animation to display */
  variant?: Variant;
  /** The size of the loading component */
  size?: Size;
  /** Additional CSS classes to apply */
  className?: string;
  /** Additional CSS classes for the overlay wrapper when children are provided */
  wrapperClassName?: string;
  /** Description to display alongside the loading animation */
  description?: string | ReactNode;
  /** Whether the loading component is visible */
  isLoading?: boolean;
  /** Color of the loading animation and description (CSS color value) */
  color?: string;
  /** Children to render when not in overlay mode. When provided, loading is shown as an overlay on top. */
  children?: ReactNode;
  /** Accessibility label for screen readers */
  'aria-label'?: string;
};
