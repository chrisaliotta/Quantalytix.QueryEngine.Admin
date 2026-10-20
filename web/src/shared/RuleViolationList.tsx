import type { RuleViolation } from './types';

/** The publication rules a version breaks, each message followed by its rule code. */
export function RuleViolationList({ violations }: { violations: RuleViolation[] }) {
  return (
    <ul data-slot="rule-violation-list" className="list-disc pl-4">
      {violations.map(violation => (
        <li key={`${violation.rule}:${violation.message}`}>
          {violation.message} <code className="text-xs opacity-70">{violation.rule}</code>
        </li>
      ))}
    </ul>
  );
}
