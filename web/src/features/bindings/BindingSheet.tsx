import { useMutation } from '@tanstack/react-query';
import { useEffect, useRef } from 'react';
import { useForm, useWatch, type UseFormReturn } from 'react-hook-form';
import { apiClient } from '@/api/client';
import { unwrap } from '@/api/problemDetails';
import { invalidateTenantQueries } from '@/api/queryClient';
import { FieldSeparator } from '@/components/ui/field';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { EntityField, SwitchField, TextField, VocabularyField } from '@/shared/form';
import type { LookupItem } from '@/shared/types';
import { useLookup } from '@/shared/useLookup';
import { useTenantId } from '@/shared/useTenantId';
import { bindingFormValues, toBindingInput, type BindingFormValues } from './bindingForm';
import { GroupingFields } from './GroupingFields';
import { useBindings } from './queries';
import { TimeBindingFields } from './TimeBindingFields';
import type { BindingDetail } from './types';

export interface BindingSheetProps {
  versionId: number;
  /** The binding to edit; absent for a new binding. */
  detail?: BindingDetail;
  /** Whether the version is a draft; the sheet is read-only otherwise. */
  isDraft: boolean;
  onClose: () => void;
}

/**
 * Sheet that creates or edits a binding, showing the references of the chosen kind: a mapping set for mapped bindings, a
 * source binding and bridge for bridge bindings, and the time section for time bindings. Range and time bindings offer only
 * dimensions of their kind. A mapping set or bridge is named by identity, so requests read the version their assumption set
 * pins. Mounted only while open.
 */
export function BindingSheet({ versionId, detail, isDraft, onClose }: BindingSheetProps) {
  const tenantId = useTenantId();
  const form = useForm<BindingFormValues>({ defaultValues: bindingFormValues(detail) });
  const [kind, dimensionId, sourceBindingId] = useWatch({
    control: form.control,
    name: ['bindingKind', 'dimensionId', 'sourceBindingId'],
  });
  const dimensions = useLookup('dimensions');
  const bindings = useBindings(versionId);
  const sourceDimensionId = bindings.data?.find(item => item.binding.bindingId === sourceBindingId)?.binding.dimensionId ?? null;
  useStaleReferenceClearing(form, { kind, dimensionId, sourceBindingId }, dimensions.data);
  const save = useMutation({
    mutationFn: async (values: BindingFormValues) => {
      const body = toBindingInput(values);
      if (detail) {
        return unwrap(
          await apiClient.PUT('/api/tenants/{tenantId}/dataset-versions/{versionId}/bindings/{bindingId}', {
            params: { path: { tenantId, versionId, bindingId: detail.binding.bindingId } },
            body,
          })
        );
      }
      return unwrap(
        await apiClient.POST('/api/tenants/{tenantId}/dataset-versions/{versionId}/bindings', {
          params: { path: { tenantId, versionId } },
          body,
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
      title={detail?.binding.displayName ?? 'New binding'}
      description="A semantic route from a field of this version to a canonical dimension."
      form={form}
      onSubmit={save.mutateAsync}
      readOnly={!isDraft}
      width="40rem"
    >
      <TextField name="displayName" label="Display name" description="The role the dimension plays here, such as Origination branch." />
      <VocabularyField name="bindingKind" label="Kind" vocabulary="binding_kind" />
      <EntityField name="dimensionId" label="Dimension" lookupKind="dimensions" filter={dimensionKindFilter(kind)} />
      <EntityField
        name="sourceFieldId"
        label="Source field"
        lookupKind="fields"
        parentId={versionId}
        allowEmpty={kind === 'bridge'}
        description={sourceFieldDescription(kind)}
      />
      {kind === 'mapped' && (
        <EntityField
          name="mappingSetId"
          label="Mapping set"
          lookupKind="mapping-sets"
          parentId={dimensionId}
          description="The source vocabulary whose entries map the field's values onto members; requests read the version their assumption set pins."
        />
      )}
      {kind === 'bridge' && (
        <>
          <EntityField
            name="sourceBindingId"
            label="Source binding"
            lookupKind="bindings"
            parentId={versionId}
            filter={item => item.id !== detail?.binding.bindingId}
            description="The binding of this version whose members the bridge remaps."
          />
          <EntityField
            name="bridgeId"
            label="Bridge"
            lookupKind="bridges"
            parentId={sourceDimensionId}
            filter={item => item.parentId === dimensionId}
            description="A bridge from the source binding's dimension into this binding's, which remaps the source binding's members; requests read the version their assumption set pins."
          />
        </>
      )}
      <GroupingFields dimensionId={dimensionId} />
      {kind === 'time' && <TimeBindingFields versionId={versionId} />}
      <FieldSeparator>Flags</FieldSeparator>
      <SwitchField name="isRequired" label="Required" description="A dataset fixture: it cannot be deleted or disabled." />
      <SwitchField
        name="isGroupable"
        label="Groupable"
        description="The semantic route may group aggregate queries; its source field must be groupable too."
      />
      <SwitchField name="isFilterable" label="Filterable" description="The dimension may take part in dimension-aware filters." />
      <SwitchField name="isEnabled" label="Enabled" description="Disabled bindings are not offered to queries." />
    </EntityFormSheet>
  );
}

/** The sheet's choices that other references depend on. */
interface ReferenceChoices {
  kind: string | null;
  dimensionId: number | null;
  sourceBindingId: number | null;
}

/**
 * Clears the references that belong to a dimension once the user picks another one: the mapping set, the bridge and the
 * level and member keys then name another dimension's rows, so the old choices could only fail validation. A new source
 * binding clears the bridge the same way, because the bridge leads from the source binding's dimension. A kind that binds
 * only dimensions of its own kind clears a dimension of another kind. It runs after render, never inside a form
 * notification, so watchers keep seeing the cleared values.
 */
function useStaleReferenceClearing(form: UseFormReturn<BindingFormValues>, choices: ReferenceChoices, dimensions: LookupItem[] | undefined) {
  const { kind, dimensionId, sourceBindingId } = choices;
  const previous = useRef(choices);
  useEffect(() => {
    const before = previous.current;
    previous.current = { kind, dimensionId, sourceBindingId };
    const fitsKind = dimensionKindFilter(kind);
    const dimension = dimensions?.find(item => item.id === dimensionId);
    if (kind !== before.kind && fitsKind && dimension && !fitsKind(dimension)) {
      form.setValue('dimensionId', null);
    }
    if (dimensionId !== before.dimensionId) {
      form.setValue('mappingSetId', null);
      form.setValue('groupingLevelKey', null);
      form.setValue('groupingAncestorMemberKey', null);
      form.setValue('unknownMemberKey', null);
    }
    if (dimensionId !== before.dimensionId || sourceBindingId !== before.sourceBindingId) {
      form.setValue('bridgeId', null);
    }
  }, [form, dimensions, kind, dimensionId, sourceBindingId]);
}

/** Range and time bindings bind only dimensions of their own kind; other kinds offer every dimension. */
function dimensionKindFilter(kind: string | null) {
  if (kind === 'range' || kind === 'time') {
    return (item: LookupItem) => item.classifier === kind;
  }
  return undefined;
}

/** What the source field means for a binding kind. */
function sourceFieldDescription(kind: string | null) {
  if (kind === 'bridge') {
    return 'Optional; gives the bridge binding a request-facing field key.';
  }
  return 'The request-facing field whose values the binding classifies.';
}
