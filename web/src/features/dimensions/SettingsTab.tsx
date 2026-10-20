import { Save } from 'lucide-react';
import { useMemo } from 'react';
import { FormProvider, useForm } from 'react-hook-form';
import { Loading } from '@registry/components/Loading';
import { Button } from '@/components/ui/button';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { FormReadOnlyContext, useProblemSubmit, VocabularyField } from '@/shared/form';
import { StatusBadge } from '@/shared/StatusBadge';
import { formatDateTime } from '@/shared/utcTime';
import { useDimensionVersion, useUpdateDimensionVersion } from './api';
import type { DimensionVersion, DimensionVersionInput } from './types';

export interface SettingsTabProps {
  versionId: number;
  isRange: boolean;
  isEditable: boolean;
}

/** A version's range data type, editable while it is a draft or the published version, plus its status and publication time. */
export function SettingsTab({ versionId, isRange, isEditable }: SettingsTabProps) {
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
      <FormReadOnlyContext value={!isEditable}>
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
          <ApiErrorAlert error={problem} />
          {isEditable && (
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
  return { rangeDataType: version.rangeDataType ?? null };
}
