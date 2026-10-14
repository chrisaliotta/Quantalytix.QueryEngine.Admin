import { useFormContext, useWatch } from 'react-hook-form';
import { FieldSeparator } from '@/components/ui/field';
import {
  DateTimeField,
  EntityField,
  MultiVocabularyField,
  TextField,
  VocabularyField,
} from '@/shared/form';
import type { BindingFormValues } from './bindingForm';

/**
 * The binding sheet's time section for time bindings: value type and observation kind, the source and default query
 * time zones, the native periodicity, the interval end field, the allowed periodicities and the cumulative origin with
 * its fixed start.
 */
export function TimeBindingFields({ versionId }: { versionId: number }) {
  const { control } = useFormContext<BindingFormValues>();
  const [valueType, origin] = useWatch({ control, name: ['timeBinding.timeValueType', 'timeBinding.cumulativeOrigin'] });

  return (
    <>
      <FieldSeparator>Time axis</FieldSeparator>
      <VocabularyField name="timeBinding.timeValueType" label="Value type" vocabulary="time_value_type" />
      <VocabularyField name="timeBinding.observationKind" label="Observation kind" vocabulary="observation_kind" />
      {valueType === 'timestamp' && (
        <TextField
          name="timeBinding.sourceTimeZone"
          label="Source time zone"
          description="The IANA zone the timestamps are recorded in, such as America/Chicago or Europe/London."
        />
      )}
      <TextField
        name="timeBinding.defaultQueryTimeZone"
        label="Default query time zone"
        description="The IANA zone of queries that name none, such as UTC or America/New_York."
      />
      <VocabularyField
        name="timeBinding.nativePeriodicity"
        label="Native periodicity"
        vocabulary="periodicity"
        allowEmpty
        description="Empty for irregular or instant observations."
      />
      <EntityField
        name="timeBinding.intervalEndFieldId"
        label="Interval end field"
        lookupKind="fields"
        parentId={versionId}
        allowEmpty
        description="The exclusive end of each interval, where the native periodicity cannot derive it."
      />
      <MultiVocabularyField
        name="timeBinding.allowedPeriodicities"
        label="Allowed periodicities"
        vocabulary="periodicity"
        description="The periodicities queries may group this time axis by."
      />
      <VocabularyField name="timeBinding.cumulativeOrigin" label="Cumulative origin" vocabulary="cumulative_origin" />
      {origin === 'fixed_start' && (
        <DateTimeField name="timeBinding.cumulativeFixedStart" label="Cumulative fixed start" description="Where cumulative calculations begin." />
      )}
    </>
  );
}
