import { Loader2 } from 'lucide-react';
import { cn } from '@/lib/utils';
import './Loading.css';
import {
  BARS_SIZE_CLASS,
  DOTS_SIZE_CLASS,
  PULSE_SIZE_CLASS,
  SIZE_PX,
  TEXT_SIZE_CLASS,
} from './constants';
import type { LoadingProps, Size, Variant } from './types';

function Indicator({ variant, size }: { variant: Variant; size: Size }) {
  const pxSize = SIZE_PX[size];

  if (variant === 'spinner') {
    return <Loader2 className="animate-spin" size={pxSize} />;
  }

  if (variant === 'dots') {
    return (
      <div className="flex space-x-1">
        {[0, 1, 2].map(i => (
          <div
            key={i}
            className={cn('rounded-full bg-current', DOTS_SIZE_CLASS[size])}
            style={{
              animationDelay: `${i * 0.2}s`,
              animation: 'loading-bounce 1.4s ease-in-out infinite both',
            }}
          />
        ))}
      </div>
    );
  }

  if (variant === 'pulse') {
    return (
      <div
        className={cn('rounded-full bg-current', PULSE_SIZE_CLASS[size])}
        style={{
          animation: 'pulse 2s cubic-bezier(0.4, 0, 0.6, 1) infinite',
        }}
      />
    );
  }

  if (variant === 'bars') {
    return (
      <div className="flex space-x-1">
        {[0, 1, 2].map(i => (
          <div
            key={i}
            className={cn('bg-current', BARS_SIZE_CLASS[size])}
            style={{
              animationDelay: `${i * 0.15}s`,
              animation: 'loading-bars 1.2s ease-in-out infinite',
            }}
          />
        ))}
      </div>
    );
  }

  return null;
}

export function Loading({
  variant = 'spinner',
  size = 'md',
  className,
  wrapperClassName,
  description,
  isLoading = true,
  color,
  children,
  'aria-label': ariaLabel = 'Loading...',
}: LoadingProps) {
  const hasChildren = children !== undefined;

  if (!isLoading && !hasChildren) return null;

  const loadingContent = (
    <div
      data-slot="loading"
      data-variant={variant}
      data-size={size}
      className={cn(
        'flex items-center justify-center',
        description && 'flex-col gap-2',
        !hasChildren && 'min-h-6',
        className
      )}
      style={color ? { color } : undefined}
      role="status"
      aria-label={ariaLabel}
    >
      <Indicator variant={variant} size={size} />
      {description && (
        <span className={cn(TEXT_SIZE_CLASS[size], !color && 'text-muted-foreground')}>
          {description}
        </span>
      )}
    </div>
  );

  if (hasChildren) {
    return (
      <div className={cn('relative', wrapperClassName)}>
        {children}
        {isLoading && (
          <div data-testid="loading-wrapper" className="absolute inset-0 flex items-center justify-center bg-background/40 backdrop-blur-[1px] z-50">
            {loadingContent}
          </div>
        )}
      </div>
    );
  }

  return loadingContent;
}
