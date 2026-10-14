import { GripVertical, X } from 'lucide-react';
import { SortableItem, SortableRoot } from '@registry/components/Sortable';
import { Button } from '@/components/ui/button';
import { EntitySelect } from '@/shared/EntitySelect';
import { FormFieldLayout, useFormField, type FormFieldProps } from '@/shared/form';
import { useLookup } from '@/shared/useLookup';

export interface GrainFieldsFieldProps extends FormFieldProps {
  /** The dataset version whose fields can be added. */
  versionId: number;
}

/** Ordered list of a grain's fields: drag to reorder, add from the version's fields, remove; value is the field IDs. */
export function GrainFieldsField({ name, label, description, disabled, versionId }: GrainFieldsFieldProps) {
  const { field, id, error, isDisabled } = useFormField(name, disabled);
  const fields = useLookup('fields', versionId);
  const keys = new Map(fields.data?.map(item => [item.id, item.key]));
  const fieldIds: number[] = field.value;

  function add(fieldId: number | null) {
    if (fieldId !== null) {
      field.onChange([...fieldIds, fieldId]);
    }
  }

  return (
    <FormFieldLayout id={id} label={label} description={description} error={error}>
      <SortableRoot
        items={fieldIds}
        onItemsChange={next => field.onChange(next)}
        activationConstraint={{ distance: 4 }}
      >
        <ol data-slot="grain-fields" className="space-y-1">
          {fieldIds.map((fieldId, index) => (
            <SortableItem key={fieldId} id={fieldId} index={index} handle disabled={isDisabled}>
              {({ setNodeRef, setActivatorNodeRef, attributes, activatorListeners, style, isDragging }) => (
                <li
                  ref={setNodeRef}
                  style={style}
                  {...attributes}
                  className="flex items-center gap-2 rounded-md border bg-background px-2 py-1 text-sm data-[dragging=true]:opacity-70"
                  data-dragging={isDragging}
                >
                  <button
                    type="button"
                    ref={setActivatorNodeRef}
                    {...activatorListeners}
                    aria-label="Drag to reorder"
                    disabled={isDisabled}
                    className="cursor-grab text-muted-foreground disabled:cursor-default"
                  >
                    <GripVertical className="size-4" />
                  </button>
                  <span className="w-5 text-muted-foreground">{index + 1}.</span>
                  <span className="min-w-0 flex-1 truncate">{keys.get(fieldId) ?? fieldId}</span>
                  {!isDisabled && (
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon-xs"
                      aria-label="Remove field"
                      onClick={() => field.onChange(fieldIds.filter(other => other !== fieldId))}
                    >
                      <X />
                    </Button>
                  )}
                </li>
              )}
            </SortableItem>
          ))}
        </ol>
      </SortableRoot>
      {!isDisabled && (
        <EntitySelect
          lookupKind="fields"
          parentId={versionId}
          value={null}
          onChange={add}
          filter={item => !fieldIds.includes(item.id)}
          placeholder="Add a field..."
        />
      )}
    </FormFieldLayout>
  );
}
