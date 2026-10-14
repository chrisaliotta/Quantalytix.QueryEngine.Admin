import { useMutation } from '@tanstack/react-query';
import { FormProvider, useForm, useWatch } from 'react-hook-form';
import { toast } from 'sonner';
import { apiClient } from '@/api/client';
import { unwrap } from '@/api/problemDetails';
import { invalidateTenantQueries } from '@/api/queryClient';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { EntityField, FormReadOnlyContext, JsonField, SwitchField, TextField, VocabularyField } from '@/shared/form';
import { useTenantId } from '@/shared/useTenantId';
import { SourceObjectField } from './SourceObjectField';
import { emptyToNull } from './text';
import type { DatasetVersion } from './types';
import { useProblemSubmit } from './useProblemSubmit';

interface SettingsValues {
  sourceObjectKind: string | null;
  sourceObject: string | null;
  supportsAggregate: boolean;
  supportsDetail: boolean;
  defaultTimeBindingId: number | null;
  expressionLanguageVersion: string;
  securityPolicyKey: string;
  properties: string;
}

/**
 * Settings tab of the dataset-version editor: source object, query modes, default time binding, expression language,
 * security policy and properties. Read-only unless the version is a draft; mount it per version.
 */
export function SettingsTab({ version, isDraft }: { version: DatasetVersion; isDraft: boolean }) {
  const tenantId = useTenantId();
  const versionId = version.datasetVersionId;
  const form = useForm<SettingsValues>({
    defaultValues: {
      sourceObjectKind: version.sourceObjectKind,
      sourceObject: version.sourceObject,
      supportsAggregate: version.supportsAggregate,
      supportsDetail: version.supportsDetail,
      defaultTimeBindingId: version.defaultTimeBindingId ?? null,
      expressionLanguageVersion: version.expressionLanguageVersion,
      securityPolicyKey: version.securityPolicyKey ?? '',
      properties: version.properties ?? '',
    },
  });
  const kind = useWatch({ control: form.control, name: 'sourceObjectKind' });
  const save = useMutation({
    mutationFn: async (values: SettingsValues) =>
      unwrap(
        await apiClient.PUT('/api/tenants/{tenantId}/dataset-versions/{versionId}', {
          params: { path: { tenantId, versionId } },
          body: {
            sourceObjectKind: values.sourceObjectKind ?? '',
            sourceObject: values.sourceObject ?? '',
            supportsAggregate: values.supportsAggregate,
            supportsDetail: values.supportsDetail,
            defaultTimeBindingId: values.defaultTimeBindingId,
            expressionLanguageVersion: values.expressionLanguageVersion,
            securityPolicyKey: emptyToNull(values.securityPolicyKey),
            properties: emptyToNull(values.properties),
          },
        })
      ),
    onSuccess: () => {
      toast.success('Settings saved.');
      return invalidateTenantQueries(tenantId);
    },
  });
  const { submit, problem } = useProblemSubmit(form, save.mutateAsync);
  const { isSubmitting } = form.formState;

  return (
    <Card data-slot="settings-tab">
      <CardContent>
        <FormProvider {...form}>
          <form className="space-y-5" onSubmit={submit}>
            <FormReadOnlyContext value={!isDraft}>
              <fieldset disabled={!isDraft || isSubmitting} className="grid min-w-0 gap-5 md:grid-cols-2">
                <VocabularyField name="sourceObjectKind" label="Source kind" vocabulary="source_object_kind" />
                <SourceObjectField name="sourceObject" label="Source object" kind={kind} />
                <SwitchField name="supportsAggregate" label="Aggregate queries" description="The dataset may satisfy aggregate mode." />
                <SwitchField name="supportsDetail" label="Detail queries" description="The dataset may satisfy detail mode." />
                <EntityField
                  name="defaultTimeBindingId"
                  label="Default time binding"
                  description="The time axis of queries whose measures require no other."
                  lookupKind="time-bindings"
                  parentId={versionId}
                  allowEmpty
                />
                <TextField
                  name="expressionLanguageVersion"
                  label="Expression language version"
                  description="The grammar of this version's field and measure expressions."
                />
                <TextField
                  name="securityPolicyKey"
                  label="Security policy key"
                  description="Optional application authorization policy."
                />
                <div className="md:col-span-2">
                  <JsonField name="properties" label="Properties" description="Extension metadata as JSON; empty for none." />
                </div>
              </fieldset>
            </FormReadOnlyContext>
            <ApiErrorAlert error={problem} />
            {isDraft && (
              <div className="flex justify-end">
                <Button type="submit" disabled={isSubmitting}>
                  Save settings
                </Button>
              </div>
            )}
          </form>
        </FormProvider>
      </CardContent>
    </Card>
  );
}
