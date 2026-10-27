import { FileTextIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import type { DimensionOwnerReport } from '@/lib/dimensions/contract'
import { useEditor } from '@/lib/report/store'
import { useAppNav } from '@/lib/navigation'
import { Mono } from './shared'

/** Links a report-owned object to its report in the editor, never to the dimension. */
export function ReportLink({ owner }: { owner: DimensionOwnerReport }) {
  const { library } = useEditor()
  const nav = useAppNav()
  const item = library.find((i) => i.report.settings.key === owner.reportKey)
  if (!item)
    return (
      <span className="flex flex-col">
        <span>{owner.reportName}</span>
        <Mono>{owner.reportKey}</Mono>
      </span>
    )
  return (
    <Button variant="link" size="sm" className="h-auto justify-start gap-1.5 p-0" onClick={() => nav.report(item.id)}>
      <FileTextIcon aria-hidden className="size-3.5" />
      {item.report.settings.name || owner.reportName}
      <span className="sr-only"> (open in report editor)</span>
    </Button>
  )
}
