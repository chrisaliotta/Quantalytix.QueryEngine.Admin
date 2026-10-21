/**
 * Why these tests exist: a report's default comparisons live inside its default settings JSON, which the settings sheet edits in
 * two parts, the rest of the settings as JSON text and the comparisons as a structured list, and must store as one object again.
 * They pin down that a stored set of comparisons comes back unchanged through the sheet, that a scenario is a member key or
 * `{ "latest": folder }`, that a period rule sends only the members its type reads, which the API refuses otherwise, and that
 * settings without comparisons are stored exactly as typed. The scenario picker offers only what the API accepts, assignable
 * members as versions and folders with one under them for the latest version, and the API's errors, keyed by their path inside
 * the settings, land on the comparison field that caused them.
 */
import { describe, expect, it } from 'vitest';
import type { LookupItem } from '@/shared/types';
import {
  joinSettings,
  newComparison,
  scenarioOptions,
  settingsFieldPath,
  splitSettings,
  toComparison,
  type Comparison,
} from './comparisonSettings';

/** The plan's default comparisons: a budget, the latest forecast, forecast against budget, and the prior year as reported. */
const comparisons: Comparison[] = [
  { key: 'bud', transforms: ['comparison', 'change', 'ratio', 'drivers'], profileKey: 'plan', scenario: 'BUD_FY26_V2' },
  { key: 'fc', transforms: ['comparison', 'change'], profileKey: 'plan', scenario: { latest: 'FC_FY2026' } },
  { key: 'fcVsBud', transforms: ['change'], profileKey: 'plan', scenario: 'BUD_FY26_V2', base: 'fc' },
  { key: 'py', transforms: ['change', 'percentChange'], period: { type: 'shift', length: 1, unit: 'year' }, assumptionSet: 'close_2025' },
  { key: 'pme', transforms: ['change'], period: { type: 'periodEnd', periodicity: 'month', offset: -1 } },
  { key: 'fy25', transforms: ['share'], period: { type: 'range', start: '2025-01-01', endExclusive: '2026-01-01' } },
];

describe('splitSettings and joinSettings', () => {
  it('give back the stored comparisons and settings unchanged', () => {
    const stored = JSON.stringify({ periodicity: 'month', layout: 'statement', comparisons });

    const split = splitSettings(stored);

    expect(split.settingsJson).toBe('{"periodicity":"month","layout":"statement"}');
    expect(JSON.parse(joinSettings(split.settingsJson, split.comparisons) ?? '')).toEqual(JSON.parse(stored));
  });

  it('store settings without comparisons as typed, and no settings as null', () => {
    const typed = '{ "periodicity": "month" }';

    expect(splitSettings(typed)).toEqual({ settingsJson: typed, comparisons: [] });
    expect(joinSettings(typed, [])).toBe(typed);
    expect(joinSettings('  ', [])).toBeNull();
    expect(joinSettings('', [{ ...newComparison(), key: 'py' }])).toBe('{"comparisons":[{"key":"py","transforms":["change"]}]}');
  });
});

describe('toComparison', () => {
  it('sends a version as its key, a folder as latest, and an unchosen key empty for the API to refuse', () => {
    const base = { ...newComparison(), key: 'bud' };

    expect(toComparison({ ...base, scenarioMode: 'member', scenarioKey: 'BUD_FY26_V2' }).scenario).toBe('BUD_FY26_V2');
    expect(toComparison({ ...base, scenarioMode: 'latest', scenarioKey: 'FC_FY2026' }).scenario).toEqual({ latest: 'FC_FY2026' });
    expect(toComparison({ ...base, scenarioMode: 'member' }).scenario).toBe('');
    expect(toComparison(base)).not.toHaveProperty('scenario');
  });

  it("sends only the members the period's type reads", () => {
    const edited = { ...newComparison(), key: 'pm', length: 1, unit: 'year', periodicity: 'month', offset: -1 };

    expect(toComparison({ ...edited, periodType: 'period' }).period).toEqual({ type: 'period', periodicity: 'month', offset: -1 });
    expect(toComparison({ ...edited, periodType: 'shift' }).period).toEqual({ type: 'shift', length: 1, unit: 'year' });
    expect(toComparison({ ...edited, periodType: null })).not.toHaveProperty('period');
  });
});

/** A member of the scenario dimension's published version. */
function member(id: number, key: string, parentId: number | null, isAssignable: boolean): LookupItem {
  return { id, key, label: key, parentId, isAssignable };
}

describe('scenarioOptions', () => {
  // Budgets > FY26 > two versions; Forecasts holds an empty FY27 folder and no version yet.
  const members = [
    member(1, 'BUDGETS', null, false),
    member(2, 'BUD_FY26', 1, false),
    member(3, 'BUD_FY26_V1', 2, true),
    member(4, 'BUD_FY26_V2', 2, true),
    member(5, 'FORECASTS', null, false),
    member(6, 'FC_FY27', 5, false),
  ];

  it('offers the assignable members as versions', () => {
    expect(scenarioOptions(members, 'member').map(option => option.key)).toEqual(['BUD_FY26_V1', 'BUD_FY26_V2']);
  });

  it('offers the folders with an assignable member anywhere under them for the latest version', () => {
    expect(scenarioOptions(members, 'latest').map(option => option.key)).toEqual(['BUDGETS', 'BUD_FY26']);
  });
});

describe('settingsFieldPath', () => {
  it("puts a comparison's error on the row field it concerns", () => {
    expect(settingsFieldPath('defaultSettingsJson.comparisons.1.scenario')).toBe('comparisons.1.scenarioKey');
    expect(settingsFieldPath('defaultSettingsJson.comparisons.0.period.length')).toBe('comparisons.0.length');
    expect(settingsFieldPath('defaultSettingsJson.comparisons.0.period.offset')).toBe('comparisons.0.offset');
    expect(settingsFieldPath('defaultSettingsJson.comparisons.2.transforms')).toBe('comparisons.2.transforms');
  });

  it('puts a period member the sheet has no field for on the period type, and a problem with the list on the list', () => {
    expect(settingsFieldPath('defaultSettingsJson.comparisons.0.period.weeks')).toBe('comparisons.0.periodType');
    expect(settingsFieldPath('defaultSettingsJson.comparisons')).toBe('comparisons.root');
  });

  it('leaves every other error on its own field', () => {
    expect(settingsFieldPath('defaultSettingsJson')).toBe('defaultSettingsJson');
    expect(settingsFieldPath('defaultProfileKey')).toBe('defaultProfileKey');
  });
});
