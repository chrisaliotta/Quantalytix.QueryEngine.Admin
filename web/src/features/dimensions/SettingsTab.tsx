import { Save } from 'lucide-react';
import { useMemo } from 'react';
import { FormProvider, useForm } from 'react-hook-form';
import { Loading } from '@registry/components/Loading';
import { Button } from '@/components/ui/button';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { DateTimeField, FormReadOnlyContext, useProblemSubmit, VocabularyField } from '@/shared/form';
import { StatusBadge } from '@/shared/StatusBadge';
import { formatDateTime } from '@/shared/utcTime';
import { useDimensionVersion, useUpdateDimensionVersion } from './api';
import type { DimensionVersion, DimensionVersionInput } from './types';

export interface SettingsTabProps {
  versionId: number;
  isRange: boolean;
  isDraft: boolean;
}

/** A version's range data type and validity window, editable while it is a draft, plus its status and publication time. */
export function SettingsTab({ versionId, isRange, isDraft }: SettingsTabProps) {
  const version = useDimensionVersion(versionId);
  const update = useUpdateDimensionVersion(versionId);
  const values = useMemo(() => version.data && toSettings(version.data), [version.data]);
  const form = useForm<DimensionVersionInput>({ values });
  const { submit, problem } = useProblemSubmit(form, update.mutateAsync);

  if (version.isPending) {
    return <Loading size="sm" />;
  }
  if (version.error) {
    return <ApiErrorAlert error={version.error} />;
  }

  return (
    <FormProvider {...form}>
      <FormReadOnlyContext value={!isDraft}>
        <form data-slot="settings-tab" className="max-w-xl space-y-5" onSubmit={submit}>
          <dl className="grid grid-cols-[8rem_1fr] gap-y-2 text-sm">
            <dt className="text-muted-foreground">Status</dt>
            <dd>
              <StatusBadge status={version.data.status} />
            </dd>
            <dt className="text-muted-foreground">Published at</dt>
            <dd>{formatDateTime(version.data.publishedAt)}</dd>
          </dl>
          {(isRange || version.data.rangeDataType) && (
            <VocabularyField
              name="rangeDataType"
              label="Range data type"
              vocabulary="range_data_type"
              allowEmpty={!isRange}
              description="The type of the values the range bands compare."
            />
          )}
          <DateTimeField
            name="validFrom"
            label="Valid from"
            description="Inclusive start of the validity window; required before publishing."
          />
          <DateTimeField
            name="validTo"
            label="Valid to"
            description="Exclusive end; empty means open-ended. Publishing a later version closes it."
          />
          <ApiErrorAlert error={problem} />
          {isDraft && (
            <Button type="submit" disabled={!form.formState.isDirty || update.isPending}>
              <Save />
              Save settings
            </Button>
          )}
        </form>
      </FormReadOnlyContext>
    </FormProvider>
  );
}

function toSettings(version: DimensionVersion): DimensionVersionInput {
  return {
    rangeDataType: version.rangeDataType ?? null,
    validFrom: version.validFrom ?? null,
    validTo: version.validTo ?? null,
  };
}
