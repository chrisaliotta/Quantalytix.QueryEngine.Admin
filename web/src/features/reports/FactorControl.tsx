import { useState } from 'react';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Input } from '@/components/ui/input';
import { factorText } from './itemMappings';

/** The factors offered by name: the whole value, the value taken back out, and half of it. */
const PRESETS = [
  { factor: 1, label: 'Whole' },
  { factor: -1, label: 'Take back out' },
  { factor: 0.5, label: 'Half' },
];

export interface FactorControlProps {
  factor: number;
  /** Names the rule the factor belongs to, for assistive technology. */
  ruleLabel: string;
  disabled: boolean;
  onChange: (factor: number) => void;
}

/**
 * The factor a rule takes its item's values with: a menu of the usual factors, or any other factor typed in, which Enter or
 * leaving the field applies and Escape abandons.
 */
export function FactorControl({ factor, ruleLabel, disabled, onChange }: FactorControlProps) {
  const [custom, setCustom] = useState<string | null>(null);

  function apply(text: string) {
    setCustom(null);
    if (text.trim() !== '' && Number.isFinite(Number(text))) {
      onChange(Number(text));
    }
  }

  if (custom !== null) {
    return (
      <Input
        autoFocus
        aria-label={`Factor of ${ruleLabel}`}
        className="h-6 w-20 px-1.5 font-mono text-xs"
        type="number"
        step="any"
        value={custom}
        onChange={event => setCustom(event.target.value)}
        onBlur={() => apply(custom)}
        onKeyDown={event => {
          if (event.key === 'Enter') {
            apply(custom);
          } else if (event.key === 'Escape') {
            setCustom(null);
          }
        }}
      />
    );
  }
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="outline" size="xs" className="w-14 font-mono" disabled={disabled} aria-label={`Factor of ${ruleLabel}`}>
          {factorText(factor)}
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="start">
        {PRESETS.map(preset => (
          <DropdownMenuItem key={preset.factor} onSelect={() => onChange(preset.factor)}>
            <span className="w-8 font-mono">{factorText(preset.factor)}</span>
            {preset.label}
          </DropdownMenuItem>
        ))}
        <DropdownMenuSeparator />
        <DropdownMenuItem onSelect={() => setCustom(String(factor))}>Other factor…</DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
