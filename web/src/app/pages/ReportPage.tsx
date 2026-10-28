import { Navigate, useParams } from 'react-router'
import { ReportEditor } from '@/components/report/report-editor'
import { paths } from '@/lib/navigation'
import { useEditor } from '@/lib/report/store'

/** `/reports/:reportId`. EditorProvider opens the report named in the URL; an unknown id returns to the list. */
export function ReportPage() {
  const { reportId } = useParams()
  const { library } = useEditor()
  if (!library.some((i) => i.id === reportId)) return <Navigate to={paths.reports} replace />
  return <ReportEditor />
}
