import { useParams } from 'react-router'
import { ReportEditor } from '@/components/report/report-editor'

/** `/reports/:reportId`: the report editor of the report the URL names. */
export function ReportPage() {
  const { reportId } = useParams()
  return <ReportEditor key={reportId} reportId={reportId!} />
}
