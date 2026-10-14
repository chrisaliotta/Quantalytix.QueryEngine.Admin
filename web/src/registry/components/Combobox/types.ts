export interface ComboboxItem {
  value: string;
  label: string;
}

/** Trigger density — aligned with Button `xs` / `sm` / `default` / `lg`. */
export type ComboboxSize = 'xs' | 'sm' | 'default' | 'lg';

/**
 * Design tokens for combobox layout CSS variables.
 * Override via `tokens` or upstream CSS.
 */
export interface ComboboxTokens {
  /**
   * Popover content width CSS value.
   */
  popoverWidth?: string;
  /**
   * Popover content max-width CSS value.
   */
  popoverMaxWidth?: string;
}
