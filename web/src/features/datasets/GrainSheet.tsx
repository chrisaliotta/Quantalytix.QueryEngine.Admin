import { useMutation } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { apiClient } from '@/api/client';
import { unwrap } from '@/api/problemDetails';
import { invalidateTenantQueries } from '@/api/queryClient';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { TextField, VocabularyField } from '@/shared/form';
import { useTenantId } from '@/shared/useTenantId';
import { GrainFieldsField } from './GrainFieldsField';
import type { Grain } from './types';

interface GrainFormValues {
  grainKey: string;
  grainKind: string | null;
  fieldIds: number[];
}

export interface GrainSheetProps {
  versionId: number;
  /** The grain to edit; absent for a new grain. */
  grain?: Grain;
  /** Whether the version is a draft; the sheet is read-only otherwise. */
  isDraft: boolean;
  onClose: () => void;
}

/** Sheet that creates or edits a grain: its key, kind and ordered fields. Mounted only while open. */
export function GrainSheet({ versionId, grain, isDraft, onClose }: GrainSheetProps) {
  const tenantId = useTenantId();
  const form = useForm<GrainFormValues>({
    defaultValues: { grainKey: grain?.grainKey ?? '', grainKind: grain?.grainKind ?? 'row', fieldIds: grain?.fieldIds ?? [] },
  });
  const save = useMutation({
    mutationFn: async (values: GrainFormValues) => {
      const body = { grainKind: values.grainKind ?? '', fieldIds: values.fieldIds };
      if (grain) {
        return unwrap(
          await apiClient.PUT('/api/tenants/{tenantId}/dataset-versions/{versionId}/grains/{grainId}', {
            params: { path: { tenantId, versionId, grainId: grain.grainId } },
            body,
          })
        );
      }
      return unwrap(
        await apiClient.POST('/api/tenants/{tenantId}/dataset-versions/{versionId}/grains', {
          params: { path: { tenantId, versionId } },
          body: { grainKey: values.grainKey, ...body },
        })
      );
    },
    onSuccess: () => invalidateTenantQueries(tenantId),
  });

  return (
    <EntityFormSheet
      open
      onOpenChange={open => {
        if (!open) {
          onClose();
        }
      }}
      title={grain?.grainKey ?? 'New grain'}
      description="A row grain identifies one detail row; a series grain identifies one trajectory over time and leaves out the observation time."
      form={form}
      onSubmit={save.mutateAsync}
      readOnly={!isDraft}
    >
      <TextField
        name="grainKey"
        label="Key"
        description="Such as rowIdentity or temporalSeries; fixed once created."
        disabled={grain !== undefined}
      />
      <VocabularyField name="grainKind" label="Kind" vocabulary="grain_kind" />
      <GrainFieldsField
        name="fieldIds"
        label="Fields"
        description="In component order; drag to reorder."
        versionId={versionId}
      />
    </EntityFormSheet>
  );
}
