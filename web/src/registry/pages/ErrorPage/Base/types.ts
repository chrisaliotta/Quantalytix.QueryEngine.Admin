import type { ReactNode } from 'react';

/** Button variant supported by shadcn Button (when not using custom render). */
export type ErrorPageButtonVariant =
  | 'default'
  | 'outline'
  | 'link'
  | 'ghost'
  | 'destructive'
  | 'secondary';

export type ErrorPageAction = {
  /**
   * Fully custom action element. When provided, it overrides the default Button
   * rendering and `label` / `variant` / `onClick` are ignored.
   */
  render?: ReactNode;
  /**
   * Button content. Can be a string or ReactNode (e.g. icon + text).
   * Used only when `render` is not set.
   */
  label?: ReactNode;
  /**
   * Shadcn Button visual variant (default, outline, link, ghost, destructive, secondary).
   * Used only when `render` is not set.
   */
  variant?: ErrorPageButtonVariant;
  /**
   * Called when the default Button is clicked. Ignored when `render` is provided.
   */
  onClick?: () => void;
};

export type ErrorPageProps = {
  /** HTTP status code or custom content to display (e.g. 404, 500 or ReactNode). Default: 404 */
  statusCode?: number | ReactNode;
  /** Main heading below the status code. Can be string or ReactNode. Default: "Page Not Found" */
  title?: ReactNode;
  /** Description content. Can be string or ReactNode. Default: "The page you're looking for doesn't exist or has been moved." */
  description?: ReactNode;
  /** Custom action buttons. When provided, overrides onGoBack/onReturnHome. */
  actions?: ErrorPageAction[];
  /** Callback for "Go Back" button. Ignored when actions is provided. */
  onGoBack?: () => void;
  /** Callback for "Return to Home" button. Ignored when actions is provided. */
  onReturnHome?: () => void;
  /** Extra CSS class for the root container */
  className?: string;
};

/** Props for status-specific variants (ErrorPage400, ErrorPage404, etc.). */
export type ErrorPageVariantProps = Omit<
  ErrorPageProps,
  'statusCode' | 'title' | 'description'
> &
  Partial<Pick<ErrorPageProps, 'title' | 'description'>>;
