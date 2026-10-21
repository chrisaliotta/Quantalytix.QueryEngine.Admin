import { Plus, Trash2 } from 'lucide-react';
import { useFieldArray, useFormContext, useFormState, useWatch } from 'react-hook-form';
import { Button } from '@/components/ui/button';
import { DateField, KeyField, MultiVocabularyField, NumberField, TextField, VocabularyField } from '@/shared/form';
import { useOwnedContent } from '@/shared/ownedContent';
import type { LookupItem } from '@/shared/types';
import { usePublishedDimensionItems } from '@/shared/useLatestVersionItems';
import { useLookup } from '@/shared/useLookup';
import { newComparison, scenarioOptions, type ComparisonValues } from './comparisonSettings';

/** The form values the editor reads: the comparisons of the settings sheet. */
interface ComparisonsForm {
  comparisons: ComparisonValues[];
}

/** The ways a comparison names its scenario, as picker items; none keeps the run's scenario. */
const SCENARIO_MODES: LookupItem[] = [
  { id: 1, key: 'member', label: 'A version' },
  { id: 2, key: 'latest', label: 'The latest version under a folder' },
];

/**
 * The report's default comparisons, the ones a request makes when it names none, as a list of rows: each compares the run, or
 * another comparison named as its base, with the report under another profile, scenario, period rule or captured assumption set,
 * and derives the transforms it lists from every returned column.
 */
export function DefaultComparisonsField({ reportId }: { reportId: number }) {
  const { control } = useFormContext<ComparisonsForm>();
  const comparisons = useFieldArray({ control, name: 'comparisons' });
  const { errors } = useFormState({ control, name: 'comparisons' });
  const options = useComparisonOptions(reportId);

  return (
    <section data-slot="default-comparisons" className="space-y-2">
      <div className="text-sm font-medium">Default comparisons</div>
      <p className="text-sm text-muted-foreground">
        What a request compares the run with when it names no comparisons, at most six. Each derived column is named
        column.comparison.transform.
      </p>
      {errors.comparisons?.root?.message && <p className="text-sm text-destructive">{errors.comparisons.root.message}</p>}
      {comparisons.fields.map((field, index) => (
        <ComparisonRow key={field.id} index={index} options={options} onRemove={() => comparisons.remove(index)} />
      ))}
      <Button type="button" variant="outline" size="sm" onClick={() => comparisons.append(newComparison())}>
        <Plus />
        Add comparison
      </Button>
    </section>
  );
}

/** What a comparison's pickers offer. */
interface ComparisonOptions {
  profiles: LookupItem[] | null;
  /** The members of the scenario dimension's published version; none when the report has no scenario dimension. */
  scenarioMembers: LookupItem[] | null;
  /** The captured historical assumption sets. */
  historicalSets: LookupItem[] | null;
  /** Whether the report has an enabled scenario dimension. */
  hasScenarioDimension: boolean;
}

/** The report's profiles, the members of its scenario dimension's published version, and the captured assumption sets. */
function useComparisonOptions(reportId: number): ComparisonOptions {
  const profiles = useLookup('source-profiles', reportId);
  const dimensions = useOwnedContent('calculation-dimensions', reportId);
  const roles = useLookup('dimension-roles');
  const sets = useLookup('assumption-sets');
  const scenario = dimensions.data?.find(dimension => dimension.isScenario && dimension.isEnabled);
  const templateId = roles.data?.find(role => role.id === scenario?.roleId)?.parentId ?? null;
  const members = usePublishedDimensionItems(templateId, 'members');
  return {
    profiles: profiles.data ?? null,
    scenarioMembers: scenario === undefined ? [] : members,
    historicalSets: sets.data?.filter(set => set.status === 'historical') ?? null,
    hasScenarioDimension: scenario !== undefined,
  };
}

interface ComparisonRowProps {
  index: number;
  options: ComparisonOptions;
  onRemove: () => void;
}

/** One comparison: its key and transforms, then what it changes from the run, each optional. */
function ComparisonRow({ index, options, onRemove }: ComparisonRowProps) {
  const { control } = useFormContext<ComparisonsForm>();
  const path = `comparisons.${index}` as const;
  const [scenarioMode, periodType, comparisons] = useWatch({
    control,
    name: [`${path}.scenarioMode`, `${path}.periodType`, 'comparisons'],
  });
  const others = comparisons
    .map<LookupItem>((comparison, other) => ({ id: other, key: comparison.key, label: comparison.key }))
    .filter(item => item.id !== index && item.key !== '');
  const scenarioItems = scenarioMode && options.scenarioMembers && scenarioOptions(options.scenarioMembers, scenarioMode);

  return (
    <div data-slot="default-comparison" className="space-y-3 rounded-md border p-3">
      <div className="flex items-center">
        <span className="text-sm font-medium">Comparison {index + 1}</span>
        <Button type="button" variant="ghost" size="icon-xs" className="ml-auto" aria-label={`Remove comparison ${index + 1}`} onClick={onRemove}>
          <Trash2 />
        </Button>
      </div>
      <div className="grid gap-3 md:grid-cols-2">
        <TextField name={`${path}.key`} label="Key" description="Letters, digits and underscores, such as bud or py." />
        <KeyField
          name={`${path}.base`}
          label="Base"
          items={others}
          allowEmpty
          description="Optional: another comparison whose values take the run's place, such as forecast against budget."
        />
      </div>
      <MultiVocabularyField name={`${path}.transforms`} label="Transforms" vocabulary="report_transform" />
      <KeyField
        name={`${path}.profileKey`}
        label="Profile"
        items={options.profiles}
        allowEmpty
        description="Optional: the profile the comparison reads, such as a plan profile; empty keeps the run's."
      />
      <div className="grid gap-3 md:grid-cols-2">
        <KeyField
          name={`${path}.scenarioMode`}
          label="Scenario"
          items={SCENARIO_MODES}
          allowEmpty
          description="Optional: a budget or forecast version; empty keeps the run's scenario."
        />
        {scenarioMode !== null && (
          <KeyField
            name={`${path}.scenarioKey`}
            label={scenarioMode === 'latest' ? 'Folder' : 'Version'}
            items={scenarioItems}
            description={
              !options.hasScenarioDimension
                ? 'The report has no scenario dimension to name a version of.'
                : scenarioMode === 'latest'
                  ? "A folder of the report's scenario dimension with a version under it; the comparison reads the latest one."
                  : "An assignable member of the published version of the report's scenario dimension."
            }
          />
        )}
      </div>
      <VocabularyField
        name={`${path}.periodType`}
        label="Period"
        vocabulary="comparison_period_type"
        allowEmpty
        description="Optional: how each run period maps to the period the comparison reads; empty keeps the run's periods."
      />
      {periodType === 'shift' && (
        <div className="grid gap-3 md:grid-cols-2">
          <NumberField name={`${path}.length`} label="Length" description="How many units back, 1 or more." />
          <VocabularyField name={`${path}.unit`} label="Unit" vocabulary="periodicity" />
        </div>
      )}
      {(periodType === 'period' || periodType === 'periodEnd') && (
        <div className="grid gap-3 md:grid-cols-2">
          <VocabularyField name={`${path}.periodicity`} label="Periodicity" vocabulary="periodicity" />
          <NumberField
            name={`${path}.offset`}
            label="Offset"
            description="Periods back from the one that holds the run period: 0 or below; empty is 0."
          />
        </div>
      )}
      {periodType === 'range' && (
        <div className="grid gap-3 md:grid-cols-2">
          <DateField name={`${path}.start`} label="Start" />
          <DateField name={`${path}.endExclusive`} label="End (exclusive)" />
        </div>
      )}
      <KeyField
        name={`${path}.assumptionSet`}
        label="Assumption set"
        items={options.historicalSets}
        allowEmpty
        description="Optional: a captured set, to read the report as it was reported then; empty reads the active set."
      />
    </div>
  );
}
