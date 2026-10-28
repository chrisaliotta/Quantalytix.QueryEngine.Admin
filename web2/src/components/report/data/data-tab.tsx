import { PlusIcon } from 'lucide-react'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { useReportEditor } from '../editor-state'
import { SourceCard } from './source-card'
import { SourceForm } from './source-form'

/** The report's sources: the datasets it reads and how their rows reach the report's rows (P6). */
export function DataTab() {
  const { report } = useReportEditor()
  const [adding, setAdding] = useState(false)
  return (
    <div className="flex flex-col gap-4 p-4 md:p-6">
      <div className="flex items-center justify-between gap-2">
        <div>
          <h2 className="text-lg font-semibold">Sources</h2>
          <p className="text-sm text-muted-foreground">
            A bridge source maps a role's members to rows through its own report bridge; a direct source reads row keys from a field.
          </p>
        </div>
        {!adding && (
          <Button onClick={() => setAdding(true)}>
            <PlusIcon /> Add source
          </Button>
        )}
      </div>
      {adding && <SourceForm onDone={() => setAdding(false)} />}
      {report.sources.map((source) => (
        <SourceCard key={source.key} source={source} />
      ))}
      {!adding && report.sources.length === 0 && <p className="text-sm text-muted-foreground">No sources yet.</p>}
    </div>
  )
}
