import { useEditor } from '@/lib/report/store'
import { DeleteReportDialog, DeleteRowDialog, RenameReportDialog, RenameReportKeyDialog } from './confirm-dialogs'
import { ChangeModelDialog } from './change-model-dialog'
import { CoverageDialog } from './coverage-dialog'
import { MappingDialog } from './mapping-dialog'
import {
  DefaultProfileDialog,
  DeleteProfileDialog,
  NewProfileDialog,
  RenameProfileDialog,
  SwitchPickDialog,
} from './profile-dialogs'

export function DialogHost() {
  const { ui, closeDialog } = useEditor()
  const d = ui.dialog
  if (!d) return null

  switch (d.type) {
    case 'mapping':
      return <MappingDialog key={`${d.sourceKey}:${d.rowKey}`} sourceKey={d.sourceKey} rowKey={d.rowKey} onClose={closeDialog} />
    case 'coverage':
      return <CoverageDialog sourceKey={d.sourceKey} onClose={closeDialog} />
    case 'deleteRow':
      return <DeleteRowDialog rowKey={d.rowKey} onClose={closeDialog} />
    case 'renameReport':
      return <RenameReportDialog onClose={closeDialog} />
    case 'renameReportKey':
      return <RenameReportKeyDialog onClose={closeDialog} />
    case 'deleteReport':
      return <DeleteReportDialog onClose={closeDialog} />
    case 'changeModel':
      return <ChangeModelDialog onClose={closeDialog} />
    case 'switchPick':
      return <SwitchPickDialog {...d} onClose={closeDialog} />
    case 'newProfile':
      return <NewProfileDialog onClose={closeDialog} />
    case 'renameProfile':
      return <RenameProfileDialog key={d.profileKey} profileKey={d.profileKey} onClose={closeDialog} />
    case 'defaultProfile':
      return <DefaultProfileDialog profileKey={d.profileKey} onClose={closeDialog} />
    case 'deleteProfile':
      return <DeleteProfileDialog profileKey={d.profileKey} onClose={closeDialog} />
  }
}
