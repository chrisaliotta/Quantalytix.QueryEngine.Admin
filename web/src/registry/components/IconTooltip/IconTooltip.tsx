import type { AriaAttributes, ComponentType, ReactNode } from 'react';
import { Info } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { cn } from '@/lib/utils';

export interface IconTooltipProps {
  /** Tooltip description. Accepts plain text or any React node. */
  description?: string | ReactNode;
  /** The icon component to render. Defaults to Info. */
  icon?: ComponentType<{ className?: string } & AriaAttributes>;
  /** Extra classes for the trigger button. */
  className?: string;
  /** Extra classes for the icon. */
  iconClassName?: string;
  /** Extra classes for the tooltip content surface. */
  tooltipClassName?: string;
}

export function IconTooltip({
  description,
  icon: Icon = Info,
  className,
  iconClassName,
  tooltipClassName,
}: IconTooltipProps) {
  if (!description) return null;

  return (
    <Tooltip data-slot="icon-tooltip">
      <TooltipTrigger asChild>
        <Button
          tabIndex={-1} // prevent autofocus when used in dialog
          data-slot="icon-tooltip-trigger"
          variant="ghost"
          size="icon"
          className={cn(
            'inline-flex h-4 w-4 items-center justify-center rounded-full text-muted-foreground hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:ring-offset-background',
            className
          )}
        >
          <Icon className={cn('h-4 w-4', iconClassName)} aria-hidden="true" />
        </Button>
      </TooltipTrigger>
      <TooltipContent
        data-slot="icon-tooltip-description"
        className={cn('max-w-xs text-wrap', tooltipClassName)}
      >
        {description}
      </TooltipContent>
    </Tooltip>
  );
}
