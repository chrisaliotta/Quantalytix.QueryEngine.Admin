import { useCallback } from 'react';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import './BasePage.css';
import { DEFAULT_DESCRIPTION, DEFAULT_STATUS_CODE, DEFAULT_TITLE } from './constants';
import type { ErrorPageAction, ErrorPageProps } from './types';

export function ErrorPage({
  statusCode = DEFAULT_STATUS_CODE,
  title = DEFAULT_TITLE,
  description = DEFAULT_DESCRIPTION,
  actions: actionsProp,
  onGoBack,
  onReturnHome,
  className,
}: ErrorPageProps) {
  const actions: ErrorPageAction[] =
    actionsProp ??
    ([
      onGoBack && { label: 'Go Back', variant: 'default' as const, onClick: onGoBack },
      onReturnHome && {
        label: 'Return to Home',
        variant: 'outline' as const,
        onClick: onReturnHome,
      },
    ].filter(Boolean) as ErrorPageAction[]);

  const getActionKey = useCallback((action: ErrorPageAction, index: number): string => {
    if (action.render !== undefined) return `render-${index}`;
    const label = typeof action.label === 'string' ? action.label : `action-${index}`;
    return label;
  }, []);

  return (
    <div
      data-slot="error-page"
      role="alert"
      aria-live="polite"
      className={cn(
        'flex h-screen bg-transparent w-full flex-col items-center justify-center gap-4',
        className
      )}
    >
      <div data-slot="error-page-content">
        <h1 data-slot="error-page-status">{statusCode ?? DEFAULT_STATUS_CODE}</h1>
        <h2 data-slot="error-page-title">{title}</h2>
        <div data-slot="error-page-description">{description}</div>
      </div>

      {actions.length > 0 && (
        <div data-slot="error-page-actions">
          {actions.map((action, index) =>
            action.render ? (
              <div key={getActionKey(action, index)} data-slot="error-page-action">
                {action.render}
              </div>
            ) : (
              <Button
                key={getActionKey(action, index)}
                data-slot="error-page-action"
                data-action-variant={action.variant ?? 'default'}
                variant={action.variant ?? 'default'}
                onClick={action.onClick}
                disabled={action.onClick == null}
              >
                {action.label}
              </Button>
            )
          )}
        </div>
      )}
    </div>
  );
}
