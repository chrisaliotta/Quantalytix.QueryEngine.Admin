import { useForm, useWatch } from 'react-hook-form';
import { contentSheetTitle } from '@/shared/contentSheet';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { NumberField, SwitchField, TextField, VocabularyField } from '@/shared/form';
import { useSaveReportStyle } from './api';
import { StylePreview } from './StylePreview';
import { toStyleInput, toStyleValues, type StyleValues } from './styleForm';
import type { ReportStyle } from './types';

export interface StyleSheetProps {
  reportId: number;
  /** The style to edit; null to create one. */
  style: ReportStyle | null;
  onClose: () => void;
}

/**
 * Creates or edits a style of the report, with a preview that follows every change: how the rows that name it show, and every row
 * of its default role that names no style. A property left empty keeps the client's own.
 */
export function StyleSheet({ reportId, style, onClose }: StyleSheetProps) {
  const save = useSaveReportStyle(reportId);
  const form = useForm<StyleValues>({ defaultValues: toStyleValues(style) });
  const values = useWatch({ control: form.control }) as StyleValues;

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title={contentSheetTitle('style', style?.styleKey ?? null)}
      description="Rows name a style by its key; a row that names none shows its role's default style. Styles are presentation only: no formula reads them."
      form={form}
      onSubmit={submitted => save.mutateAsync({ styleKey: submitted.styleKey, body: toStyleInput(submitted) })}
      width="40rem"
    >
      <StylePreview style={toStyleInput(values)} />
      <TextField
        name="styleKey"
        label="Key"
        disabled={style !== null}
        description={style === null ? 'A letter, then letters, digits or underscores.' : 'Fixed: rows name the style by it.'}
      />
      <TextField name="displayName" label="Name" />
      <VocabularyField
        name="defaultRole"
        label="Default for"
        vocabulary="row_role"
        allowEmpty
        description="Rows of this role that name no style show this one; a role has at most one default style."
      />
      <div className="grid gap-4 md:grid-cols-3">
        <TextField name="fontFamily" label="Font family" placeholder="The client's own" />
        <NumberField name="fontSize" label="Font size" description="Points, 6 to 72." />
        <VocabularyField name="fontWeight" label="Weight" vocabulary="font_weight" allowEmpty />
      </div>
      <SwitchField name="isItalic" label="Italic" />
      <div className="grid gap-4 md:grid-cols-2">
        <TextField name="textColor" label="Text color" placeholder="#RRGGBB" />
        <TextField name="backgroundColor" label="Background color" placeholder="#RRGGBB" />
      </div>
      <div className="grid gap-4 md:grid-cols-3">
        <VocabularyField name="borderTop" label="Rule above" vocabulary="border_style" allowEmpty />
        <VocabularyField name="borderBottom" label="Rule below" vocabulary="border_style" allowEmpty />
        <NumberField name="indent" label="Indent" description="Levels, 0 to 10, added to the row's depth." />
      </div>
    </EntityFormSheet>
  );
}
