export type BehaviorSwitchProps = {
  /** Accessible switch label and tooltip trigger name. */
  label: string;
  /** Description shown when hovering the behavior switch. */
  description: string;
  /** Current switch state. */
  checked: boolean;
  /** Whether the switch is currently disabled. */
  disabled?: boolean;
  /** Receives switch state changes. */
  onCheckedChange: (checked: boolean) => void;
};
