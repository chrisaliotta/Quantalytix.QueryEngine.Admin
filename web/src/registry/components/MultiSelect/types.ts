export interface MultiSelectOption {
  label: string;
  value: string;
  disabled?: boolean;
}

export interface SelectAllOption {
  label?: string;
  value?: string;
}

/** Trigger density — aligned with Combobox / Button `xs` / `sm` / `default` / `lg`. */
export type MultiSelectSize = 'xs' | 'sm' | 'default' | 'lg';

/**
 * Design tokens for multi-select layout CSS variables.
 * Override via `tokens` or upstream CSS.
 */
export interface MultiSelectTokens {
  /**
   * Popover content width CSS value.
   */
  popoverWidth?: string;
  /**
   * Popover content max-width CSS value.
   */
  popoverMaxWidth?: string;
}
