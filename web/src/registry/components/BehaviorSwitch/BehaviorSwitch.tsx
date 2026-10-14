import { Switch } from '@/components/ui/switch';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import type { BehaviorSwitchProps } from './types';

/**
 * Behavior toggle with a tooltip that remains available even for disabled switches.
 */
export function BehaviorSwitch({
  label,
  description,
  checked,
  disabled,
  onCheckedChange,
}: BehaviorSwitchProps) {
  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <span className="inline-flex w-fit">
          <Switch
            disabled={disabled}
            aria-label={label}
            checked={checked}
            onCheckedChange={onCheckedChange}
          />
        </span>
      </TooltipTrigger>
      <TooltipContent side="top" className="max-w-xs text-left">
        {description}
      </TooltipContent>
    </Tooltip>
  );
}
