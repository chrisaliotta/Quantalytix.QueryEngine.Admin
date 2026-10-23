import { useFormContext, useWatch } from 'react-hook-form';
import { EntityField, NumberField, VocabularyField } from '@/shared/form';
import { searchesBack, type MeasureFormValues } from './measureForm';

/**
 * The measure sheet's time policy section: the required time binding, the series grain observations are selected per,
 * the selection, value role and boundary match with its lookback, and the duration, partial interval and missing period
 * behaviors.
 */
export function TimePolicyFields({ versionId }: { versionId: number }) {
  const { control } = useFormContext<MeasureFormValues>();
  const boundaryMatch = useWatch({ control, name: 'timePolicy.boundaryMatch' });

  return (
    <>
      <EntityField
        name="timePolicy.requiredTimeBindingId"
        label="Required time binding"
        lookupKind="time-bindings"
        parentId={versionId}
        allowEmpty
        description="Empty uses the query's time axis or the version's default time binding."
      />
      <EntityField
        name="timePolicy.selectionDatasetGrainId"
        label="Selection grain"
        lookupKind="grains"
        parentId={versionId}
        allowEmpty
        filter={item => item.classifier === 'series'}
        description="The series grain observations are selected per; needed by every selection but all."
      />
      <VocabularyField name="timePolicy.observationSelection" label="Observation selection" vocabulary="observation_selection" />
      <VocabularyField name="timePolicy.valueTimeRole" label="Value time role" vocabulary="value_time_role" />
      <VocabularyField name="timePolicy.boundaryMatch" label="Boundary match" vocabulary="boundary_match" />
      {searchesBack(boundaryMatch) && (
        <NumberField
          name="timePolicy.maxLookbackSeconds"
          label="Lookback seconds"
          description="How far back to look for a prior observation, such as 86400 for one day; the lookback must be finite."
        />
      )}
      <VocabularyField name="timePolicy.durationUnit" label="Duration unit" vocabulary="duration_unit" />
      <VocabularyField name="timePolicy.partialIntervalBehavior" label="Partial intervals" vocabulary="partial_interval_behavior" />
      <VocabularyField name="timePolicy.missingPeriodBehavior" label="Missing periods" vocabulary="missing_period_behavior" />
    </>
  );
}
