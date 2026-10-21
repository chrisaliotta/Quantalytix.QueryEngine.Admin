import { emptyToNull } from '@/shared/text';
import type { LookupItem } from '@/shared/types';

/** How a comparison's period maps each run period to the one it reads, in the report contract's shape. */
export type ComparisonPeriod =
  | { type: 'shift'; length?: number; unit?: string }
  | { type: 'period' | 'periodEnd'; periodicity?: string; offset?: number }
  | { type: 'range'; start?: string; endExclusive?: string };

/** A default comparison as a report's settings store it under `comparisons`: a report contract 2.1 request's comparison. */
export interface Comparison {
  key: string;
  transforms: string[];
  profileKey?: string;
  scenario?: string | { latest: string };
  period?: ComparisonPeriod;
  assumptionSet?: string;
  base?: string;
}

/** How a comparison names its scenario: one version, or the latest version under a folder; null keeps the run's. */
export type ScenarioMode = 'member' | 'latest';

/** One default comparison as the settings sheet edits it; a choice not made is null, and a null period type keeps the run's periods. */
export interface ComparisonValues {
  key: string;
  transforms: string[];
  profileKey: string | null;
  scenarioMode: ScenarioMode | null;
  scenarioKey: string | null;
  periodType: ComparisonPeriod['type'] | null;
  length: number | null;
  unit: string | null;
  periodicity: string | null;
  offset: number | null;
  start: string | null;
  endExclusive: string | null;
  assumptionSet: string | null;
  base: string | null;
}

/** A report's default settings split for the settings sheet: the JSON text it edits as such, and the comparisons it lists. */
export interface SplitSettings {
  settingsJson: string;
  comparisons: ComparisonValues[];
}

/** A new comparison of the run with itself as the change, ready for a period, scenario or profile. */
export function newComparison(): ComparisonValues {
  return {
    key: '',
    transforms: ['change'],
    profileKey: null,
    scenarioMode: null,
    scenarioKey: null,
    periodType: null,
    length: null,
    unit: null,
    periodicity: null,
    offset: null,
    start: null,
    endExclusive: null,
    assumptionSet: null,
    base: null,
  };
}

/** Splits stored settings into the JSON the sheet edits as text and the comparisons it lists; settings without comparisons stay as stored. */
export function splitSettings(settingsJson: string | null | undefined): SplitSettings {
  if (!settingsJson) {
    return { settingsJson: '', comparisons: [] };
  }
  const { comparisons, ...settings } = JSON.parse(settingsJson) as { comparisons?: Comparison[] } & Record<string, unknown>;
  if (comparisons === undefined) {
    return { settingsJson, comparisons: [] };
  }
  return {
    settingsJson: Object.keys(settings).length === 0 ? '' : JSON.stringify(settings),
    comparisons: comparisons.map(toComparisonValues),
  };
}

/**
 * The settings to store: the edited JSON with the listed comparisons under `comparisons`, or the JSON as typed when there are
 * none, so the API checks it as it is; null for no settings at all.
 */
export function joinSettings(settingsJson: string, comparisons: ComparisonValues[]): string | null {
  if (comparisons.length === 0) {
    return emptyToNull(settingsJson);
  }
  const settings = emptyToNull(settingsJson) === null ? {} : (JSON.parse(settingsJson) as Record<string, unknown>);
  return JSON.stringify({ ...settings, comparisons: comparisons.map(toComparison) });
}

/** A stored comparison as the sheet edits it. */
export function toComparisonValues(comparison: Comparison): ComparisonValues {
  const { scenario, period } = comparison;
  return {
    ...newComparison(),
    key: comparison.key,
    transforms: comparison.transforms,
    profileKey: comparison.profileKey ?? null,
    scenarioMode: scenario === undefined ? null : typeof scenario === 'string' ? 'member' : 'latest',
    scenarioKey: scenario === undefined ? null : typeof scenario === 'string' ? scenario : scenario.latest,
    periodType: period?.type ?? null,
    ...(period?.type === 'shift' && { length: period.length ?? null, unit: period.unit ?? null }),
    ...((period?.type === 'period' || period?.type === 'periodEnd') && { periodicity: period.periodicity ?? null, offset: period.offset ?? null }),
    ...(period?.type === 'range' && { start: period.start ?? null, endExclusive: period.endExclusive ?? null }),
    assumptionSet: comparison.assumptionSet ?? null,
    base: comparison.base ?? null,
  };
}

/**
 * A comparison in the contract's shape, with only what the sheet sets: a scenario mode without a key sends an empty one, and a period
 * only the members its type reads, so the API reports what is missing rather than the comparison reading the run's instead.
 */
export function toComparison(values: ComparisonValues): Comparison {
  const period = toPeriod(values);
  return {
    key: values.key,
    transforms: values.transforms,
    ...(values.profileKey !== null && { profileKey: values.profileKey }),
    ...(values.scenarioMode === 'member' && { scenario: values.scenarioKey ?? '' }),
    ...(values.scenarioMode === 'latest' && { scenario: { latest: values.scenarioKey ?? '' } }),
    ...(period !== null && { period }),
    ...(values.assumptionSet !== null && { assumptionSet: values.assumptionSet }),
    ...(values.base !== null && { base: values.base }),
  };
}

/**
 * What the scenario picker offers from the members of the scenario dimension's published version: the assignable members as
 * versions, or, for the latest version under a folder, the members with an assignable member somewhere under them.
 */
export function scenarioOptions(members: LookupItem[], mode: ScenarioMode): LookupItem[] {
  if (mode === 'member') {
    return members.filter(member => member.isAssignable);
  }
  const children = new Map<number, LookupItem[]>();
  for (const member of members) {
    if (member.parentId != null) {
      children.set(member.parentId, [...(children.get(member.parentId) ?? []), member]);
    }
  }
  const hasAssignableBelow = (folder: LookupItem) => {
    const visited = new Set([folder.id]);
    const pending = [...(children.get(folder.id) ?? [])];
    for (let member = pending.pop(); member; member = pending.pop()) {
      if (visited.has(member.id)) continue;
      visited.add(member.id);
      if (member.isAssignable) return true;
      pending.push(...(children.get(member.id) ?? []));
    }
    return false;
  };
  return members.filter(hasAssignableBelow);
}

/** The settings sheet field each member of a comparison's API error path is shown on. */
const COMPARISON_FIELDS: Record<string, keyof ComparisonValues> = {
  key: 'key',
  transforms: 'transforms',
  profileKey: 'profileKey',
  scenario: 'scenarioKey',
  base: 'base',
  assumptionSet: 'assumptionSet',
  'period.type': 'periodType',
  'period.length': 'length',
  'period.unit': 'unit',
  'period.periodicity': 'periodicity',
  'period.offset': 'offset',
  'period.start': 'start',
  'period.endExclusive': 'endExclusive',
};

/**
 * The settings sheet field an API validation error belongs on. The API keys a comparison's problem by its path inside the default
 * settings, such as `defaultSettingsJson.comparisons.1.scenario`, which lands on that comparison's row field; a period member its
 * type does not read lands on the period type, a problem with the list itself on the list, and every other error stays on its own
 * path.
 */
export function settingsFieldPath(path: string): string {
  const match = /^defaultSettingsJson\.comparisons(?:\.(\d+)(?:\.(.+))?)?$/.exec(path);
  if (match === null) {
    return path;
  }
  const [, index, member] = match;
  if (index === undefined) {
    return 'comparisons.root';
  }
  const field = COMPARISON_FIELDS[member ?? 'key'] ?? (member?.startsWith('period.') ? 'periodType' : 'key');
  return `comparisons.${index}.${field}`;
}

/** The period rule of the comparison's type with that type's members, or null for the run's own periods. */
function toPeriod(values: ComparisonValues): ComparisonPeriod | null {
  switch (values.periodType) {
    case null:
      return null;
    case 'shift':
      return { type: 'shift', ...(values.length !== null && { length: values.length }), ...(values.unit !== null && { unit: values.unit }) };
    case 'range':
      return {
        type: 'range',
        ...(values.start !== null && { start: values.start }),
        ...(values.endExclusive !== null && { endExclusive: values.endExclusive }),
      };
    default:
      return {
        type: values.periodType,
        ...(values.periodicity !== null && { periodicity: values.periodicity }),
        ...(values.offset !== null && { offset: values.offset }),
      };
  }
}
