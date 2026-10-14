'use client';

import * as React from 'react';
import { Popover, PopoverAnchor, PopoverContent } from '@/components/ui/popover';
import { cn } from '@/lib/utils';

type PopoverSide = React.ComponentProps<typeof PopoverContent>['side'];
type PopoverAlign = React.ComponentProps<typeof PopoverContent>['align'];

export interface ValidationPopoverProps {
  open: boolean;
  message?: React.ReactNode;
  messageId?: string;
  children: React.ReactElement;
  className?: string;
  side?: PopoverSide;
  align?: PopoverAlign;
  sideOffset?: number;
}

export function ValidationPopover({
  open,
  message,
  messageId,
  children,
  className,
  side = 'bottom',
  align = 'start',
  sideOffset = 6,
}: ValidationPopoverProps) {
  const generatedMessageId = React.useId();
  const resolvedMessageId = messageId ?? generatedMessageId;
  const shouldOpen = Boolean(open && message);

  return (
    <span data-slot="validation-popover" className="contents">
      <Popover open={!message ? false : shouldOpen}>
        <PopoverAnchor asChild>{children}</PopoverAnchor>
        <PopoverContent
          side={side}
          align={align}
          sideOffset={sideOffset}
          onOpenAutoFocus={event => event.preventDefault()}
          onCloseAutoFocus={event => event.preventDefault()}
          className={cn(
            'z-10 w-auto max-w-sm border-destructive/40 bg-popover px-3 py-2 text-sm text-destructive shadow-md',
            className
          )}
        >
          <div data-slot="validation-popover-content">
            <div
              id={resolvedMessageId}
              role="alert"
              aria-live="assertive"
              data-slot="validation-popover-message"
            >
              {message}
            </div>
          </div>
        </PopoverContent>
      </Popover>
    </span>
  );
}
