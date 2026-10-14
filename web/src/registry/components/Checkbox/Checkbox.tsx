import * as CheckboxPrimitive from '@radix-ui/react-checkbox';
import { CheckIcon, MinusIcon } from 'lucide-react';

import { cn } from '@/lib/utils';
import { useState, type ComponentProps, type Ref } from 'react';

export interface CheckboxProps
  extends Omit<
    ComponentProps<typeof CheckboxPrimitive.Root>,
    'onCheckedChange' | 'onChange'
  > {
  ref?: Ref<HTMLButtonElement>;
  /** Controlled checked state */
  checked?: boolean;
  /** Uncontrolled default checked state */
  defaultChecked?: boolean;
  /** Whether the checkbox is disabled */
  disabled?: boolean;
  /** Indeterminate state (e.g. "some items selected") */
  indeterminate?: boolean;
  /** Callback when checked state changes */
  onChange?: (checked: boolean | 'indeterminate') => void;
}

const Checkbox = ({
  className,
  checked,
  defaultChecked,
  disabled,
  indeterminate,
  onChange,
  ref: forwardedRef,
  ...props
}: CheckboxProps) => {
  const isControlled = checked !== undefined;
  const isIndeterminateControlled = indeterminate !== undefined;
  const [internalIndeterminate, setInternalIndeterminate] = useState(
    indeterminate ?? false
  );

  const resolvedIndeterminate = isIndeterminateControlled
    ? indeterminate
    : internalIndeterminate;

  const resolvedChecked = resolvedIndeterminate
    ? ('indeterminate' as const)
    : isControlled
      ? checked
      : undefined;
  const resolvedDefaultChecked =
    !isControlled && resolvedIndeterminate
      ? ('indeterminate' as const)
      : !isControlled
        ? defaultChecked
        : undefined;

  const handleCheckedChange = (value: boolean | 'indeterminate') => {
    if (value !== 'indeterminate') setInternalIndeterminate(false);
    onChange?.(value);
  };

  return (
    <CheckboxPrimitive.Root
      ref={forwardedRef}
      data-slot="checkbox"
      checked={resolvedChecked}
      defaultChecked={resolvedDefaultChecked}
      disabled={disabled}
      onCheckedChange={handleCheckedChange}
      className={cn(
        'peer border-input dark:bg-input/30 data-[state=checked]:bg-primary data-[state=checked]:text-primary-foreground data-[state=indeterminate]:bg-primary data-[state=indeterminate]:text-primary-foreground dark:data-[state=checked]:bg-primary data-[state=checked]:border-primary data-[state=indeterminate]:border-primary focus-visible:border-ring focus-visible:ring-ring/50 aria-invalid:ring-destructive/20 dark:aria-invalid:ring-destructive/40 aria-invalid:border-destructive size-4 shrink-0 rounded-[4px] border shadow-xs transition-shadow outline-none focus-visible:ring-[3px] disabled:cursor-not-allowed disabled:opacity-50',
        className
      )}
      {...props}
    >
      <CheckboxPrimitive.Indicator
        data-slot="checkbox-indicator"
        className="grid place-content-center text-current transition-none"
      >
        {resolvedIndeterminate ? (
          <MinusIcon className="size-3.5" />
        ) : (
          <CheckIcon className="size-3.5" />
        )}
      </CheckboxPrimitive.Indicator>
    </CheckboxPrimitive.Root>
  );
};

Checkbox.displayName = 'Checkbox';

export { Checkbox };
