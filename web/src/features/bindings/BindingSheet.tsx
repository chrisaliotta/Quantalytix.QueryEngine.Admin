import { useMutation } from '@tanstack/react-query';
import { useEffect, useRef } from 'react';
import { useForm, useWatch, type UseFormReturn } from 'react-hook-form';
import { apiClient } from '@/api/client';
import { unwrap } from '@/api/problemDetails';
import { invalidateTenantQueries } from '@/api/queryClient';
import { FieldSeparator } from '@/components/ui/field';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { EntityField, SwitchField, VocabularyField } from '@/shared/form';
import type { LookupItem } from '@/shared/types';
import { useLookup } from '@/shared/useLookup';
import { useTenantId } from '@/shared/useTenantId';
import { bindingFormValues, toBindingInput, type BindingFormValues } from './bindingForm';
import { MemberBehaviorFields } from './MemberBehaviorFields';
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
 * Sheet that creates or edits a binding. A breakout binding binds a role, labeled like "Servicing branch · Branch", and a time
 * binding names its time dimension. It shows the references of the chosen kind: a mapping set for mapped bindings, a source
 * binding and bridge for bridge bindings, and the time section for time bindings; mapping sets and bridges are those of the
 * role's template. Range bindings offer only roles of range dimensions. A mapping set or bridge is named by identity, so
 * requests read the version their assumption set pins. Mounted only while open.
 */
export function BindingSheet({ versionId, detail, isDraft, onClose }: BindingSheetProps) {
  const tenantId = useTenantId();
  const form = useForm<BindingFormValues>({ defaultValues: bindingFormValues(detail) });
  const [kind, dimensionRoleId, timeDimensionId, sourceDimensionBindingId] = useWatch({
    control: form.control,
    name: ['bindingKind', 'dimensionRoleId', 'dimensionId', 'sourceDimensionBindingId'],
  });
  const roles = useLookup('dimension-roles');
  const dimensions = useLookup('dimensions');
  const bindings = useBindings(versionId);
  const templateId = kind === 'time' ? timeDimensionId : (roles.data?.find(role => role.id === dimensionRoleId)?.parentId ?? null);
  const sourceDimensionId = bindings.data?.find(item => item.binding.dimensionBindingId === sourceDimensionBindingId)?.binding.dimensionId ?? null;
  useStaleReferenceClearing(form, { kind, templateId, sourceDimensionBindingId }, roles.data, dimensions.data);
  const save = useMutation({
    mutationFn: async (values: BindingFormValues) => {
      const body = toBindingInput(values);
      if (detail) {
        return unwrap(
          await apiClient.PUT('/api/tenants/{tenantId}/dataset-versions/{versionId}/bindings/{dimensionBindingId}', {
            params: { path: { tenantId, versionId, dimensionBindingId: detail.binding.dimensionBindingId } },
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
      title={sheetTitle(detail)}
      description="Binds a field of this version to a role of a canonical dimension; requests name the binding by the role's key."
      form={form}
      onSubmit={save.mutateAsync}
      readOnly={!isDraft}
      width="40rem"
    >
      <VocabularyField name="bindingKind" label="Kind" vocabulary="binding_kind" />
      {kind === 'time' ? (
        <EntityField name="dimensionId" label="Dimension" lookupKind="dimensions" filter={kindFilter('time')} />
      ) : (
        <EntityField
          name="dimensionRoleId"
          label="Role"
          lookupKind="dimension-roles"
          filter={kindFilter(kind)}
          description="The role the field plays, such as the servicing branch; a version binds each role once."
        />
      )}
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
          parentId={templateId}
          description="The source vocabulary whose entries map the field's values onto members; requests read the version their assumption set pins."
        />
      )}
      {kind === 'bridge' && (
        <>
          <EntityField
            name="sourceDimensionBindingId"
            label="Source binding"
            lookupKind="bindings"
            parentId={versionId}
            filter={item => item.id !== detail?.binding.dimensionBindingId}
            description="The binding of this version whose members the bridge remaps."
          />
          <EntityField
            name="bridgeId"
            label="Bridge"
            lookupKind="bridges"
            parentId={sourceDimensionId}
            filter={item => item.parentId === templateId}
            description="A bridge from the source binding's dimension into the role's, which remaps the source binding's members; requests read the version their assumption set pins."
          />
        </>
      )}
      <MemberBehaviorFields dimensionId={templateId} />
      {kind === 'time' && <TimeBindingFields versionId={versionId} />}
      <FieldSeparator>Flags</FieldSeparator>
      <SwitchField name="isRequired" label="Required" description="A dataset fixture: it cannot be deleted or disabled." />
      <SwitchField
        name="isGroupable"
        label="Groupable"
        description="Aggregate queries may group by the binding; its source field must be groupable too."
      />
      <SwitchField name="isFilterable" label="Filterable" description="The dimension may take part in dimension-aware filters." />
      <SwitchField name="isEnabled" label="Enabled" description="Disabled bindings are not offered to queries." />
    </EntityFormSheet>
  );
}

/** The sheet's choices that other references depend on. */
interface ReferenceChoices {
  kind: string | null;
  /** The binding's dimension: the role's template, or a time binding's own. */
  templateId: number | null;
  sourceDimensionBindingId: number | null;
}

/**
 * Clears the references that belong to a dimension once the binding's dimension changes, through another role or time
 * dimension: the mapping set, the bridge and the unknown member then name another dimension's rows, so the old choices could
 * only fail validation. A new source binding clears the bridge the same way, because the bridge leads from the source
 * binding's dimension. A new kind clears a role or dimension it cannot bind: a time binding binds no role, and range and time
 * bindings bind only dimensions of their own kind. It runs after render, never inside a form notification, so watchers keep
 * seeing the cleared values.
 */
function useStaleReferenceClearing(
  form: UseFormReturn<BindingFormValues>,
  choices: ReferenceChoices,
  roles: LookupItem[] | undefined,
  dimensions: LookupItem[] | undefined
) {
  const { kind, templateId, sourceDimensionBindingId } = choices;
  const previous = useRef(choices);
  useEffect(() => {
    const before = previous.current;
    previous.current = { kind, templateId, sourceDimensionBindingId };
    if (kind !== before.kind) {
      const fitsKind = kindFilter(kind);
      const role = roles?.find(item => item.id === form.getValues('dimensionRoleId'));
      const dimension = dimensions?.find(item => item.id === form.getValues('dimensionId'));
      if (kind === 'time' || (fitsKind && role && !fitsKind(role))) {
        form.setValue('dimensionRoleId', null);
      }
      if (kind !== 'time' || (fitsKind && dimension && !fitsKind(dimension))) {
        form.setValue('dimensionId', null);
      }
    }
    if (templateId !== before.templateId) {
      form.setValue('mappingSetId', null);
      form.setValue('unknownMemberKey', null);
    }
    if (templateId !== before.templateId || sourceDimensionBindingId !== before.sourceDimensionBindingId) {
      form.setValue('bridgeId', null);
    }
  }, [form, roles, dimensions, kind, templateId, sourceDimensionBindingId]);
}

/**
 * Range and time bindings bind only dimensions of their own kind, which a role item carries as its template's kind; other
 * kinds offer every role.
 */
function kindFilter(kind: string | null) {
  if (kind === 'range' || kind === 'time') {
    return (item: LookupItem) => item.classifier === kind;
  }
  return undefined;
}

/** What the source field means for a binding kind. */
function sourceFieldDescription(kind: string | null) {
  if (kind === 'bridge') {
    return "Optional; a bridge binding reads its source binding's members, not a field.";
  }
  if (kind === 'time') {
    return 'The date or timestamp field; requests name the time binding by its key.';
  }
  return "The field whose values the binding classifies into the role's members.";
}

/** The sheet's title: the bound role, or the time binding. */
function sheetTitle(detail: BindingDetail | undefined) {
  if (!detail) {
    return 'New binding';
  }
  return detail.role?.displayName ?? 'Time binding';
}
