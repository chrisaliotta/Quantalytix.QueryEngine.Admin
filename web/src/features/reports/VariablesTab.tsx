import { Filter } from 'lucide-react';
import { useState } from 'react';
import { Loading } from '@registry/components/Loading';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { useLookup } from '@/shared/useLookup';
import { SourceVariableDialog } from './SourceVariableDialog';
import type { EditorSource, EditorView } from './types';

export interface VariablesTabProps {
  view: EditorView;
  /** Opens the Profiles tab, where the profiles pick between a variable's sources. */
  onShowProfiles: () => void;
}

/** The cell being edited: a source and one of the model's variables. */
interface Binding {
  source: EditorSource;
  variableKey: string;
}

/**
 * The model's variables against the report's sources: each cell is the measure of the source's dataset that supplies the
 * variable, with its filter, and is bound or unbound in a dialog. A variable several sources supply is read from the source the
 * request's profile picks, which the Profiles tab edits.
 */
export function VariablesTab({ view, onShowProfiles }: VariablesTabProps) {
  const variables = useLookup('variables', view.report.modelId);
  const [binding, setBinding] = useState<Binding | null>(null);

  if (variables.error) {
    return <ApiErrorAlert error={variables.error} />;
  }
  if (!variables.data) {
    return <Loading size="sm" />;
  }
  if (view.sources.length === 0) {
    return <p className="py-6 text-center text-sm text-muted-foreground">No sources yet. Add one in the Sources tab, then bind its measures here.</p>;
  }
  return (
    <div data-slot="variables-tab" className="space-y-3">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Variable</TableHead>
            {view.sources.map(source => (
              <TableHead key={source.source.reportSourceId}>{source.source.displayName}</TableHead>
            ))}
            <TableHead />
          </TableRow>
        </TableHeader>
        <TableBody>
          {variables.data.map(variable => {
            const suppliers = view.sources.filter(source => source.variables.some(supplied => supplied.variableKey === variable.key));
            return (
              <TableRow key={variable.key}>
                <TableCell>
                  <div className="font-mono text-xs">{variable.key}</div>
                  <div className="text-muted-foreground">{variable.label}</div>
                </TableCell>
                {view.sources.map(source => {
                  const supplied = source.variables.find(candidate => candidate.variableKey === variable.key);
                  return (
                    <TableCell key={source.source.reportSourceId}>
                      <Button variant="ghost" size="sm" onClick={() => setBinding({ source, variableKey: variable.key })}>
                        {supplied ? (
                          <>
                            <span className="font-mono text-xs">{supplied.measureKey}</span>
                            {supplied.sourceFilterJson && <Filter aria-label="Filtered" />}
                            {supplied.missingBehavior && (
                              <span className="text-xs text-muted-foreground">missing as {supplied.missingBehavior}</span>
                            )}
                          </>
                        ) : (
                          <span className="text-muted-foreground">—</span>
                        )}
                      </Button>
                    </TableCell>
                  );
                })}
                <TableCell>
                  {suppliers.length > 1 && (
                    <Button variant="link" size="sm" onClick={onShowProfiles}>
                      <Badge variant="secondary">{suppliers.length} sources</Badge>
                      Profiles pick one
                    </Button>
                  )}
                </TableCell>
              </TableRow>
            );
          })}
        </TableBody>
      </Table>
      {binding && <SourceVariableDialog view={view} source={binding.source} variableKey={binding.variableKey} onClose={() => setBinding(null)} />}
    </div>
  );
}
