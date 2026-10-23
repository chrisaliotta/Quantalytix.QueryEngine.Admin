import { useForm } from 'react-hook-form';
import { contentSheetTitle } from '@/shared/contentSheet';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { EntityField } from '@/shared/form';
import { useOwnedContent } from '@/shared/ownedContent';
import { useLookup } from '@/shared/useLookup';
import { scenarioDimensionRoles } from './scenarioDimensions';
import type { ReportScenario } from './types';

/** Values of the scenario sheet; a dimension not chosen yet is null. */
interface ReportScenarioValues {
  dimensionId: number | null;
}

export interface ReportScenarioSheetProps {
  reportId: number;
  /** The report's scenarios, whose dimensions the picker leaves out. */
  scenarios: ReportScenario[];
  /** Saves the chosen dimension's role into the report's list. */
  onSave: (roleId: number) => Promise<unknown>;
  onClose: () => void;
}

/**
 * Adds a scenario: a dimension whose members a run chooses one of. A scenario's dimension has exactly one role, so the sheet offers
 * dimensions and saves the chosen one's role, less the report's scenarios and the dimensions its calculation dimensions read.
 */
export function ReportScenarioSheet({ reportId, scenarios, onSave, onClose }: ReportScenarioSheetProps) {
  const dimensions = useOwnedContent('calculation-dimensions', reportId);
  const roles = useLookup('dimension-roles');
  const offered = scenarioDimensionRoles(roles.data ?? [], scenarios, dimensions.data ?? []);
  const form = useForm<ReportScenarioValues>({ defaultValues: { dimensionId: null } });

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title={contentSheetTitle('scenario', null)}
      description="A run or comparison names one member of the dimension for the sources whose data carries it, under the scenario's request key."
      form={form}
      onSubmit={values => onSave(offered.get(values.dimensionId ?? 0) ?? 0)}
    >
      <EntityField
        name="dimensionId"
        label="Dimension"
        lookupKind="dimensions"
        filter={item => offered.has(item.id)}
        description="A scenario's dimension has exactly one role, so dimensions with several roles are not offered, nor dimensions that are a scenario already or that a calculation dimension of the report reads."
      />
    </EntityFormSheet>
  );
}
