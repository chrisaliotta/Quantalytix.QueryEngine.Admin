import { RowPanel } from './row-panel'
import { RowTree } from './row-tree'

/** The report's rows: the tree on the left, the selected row's properties, rules and diagnostics on the right. */
export function RowsTab() {
  return (
    <div className="flex min-h-0 flex-1 flex-col lg:flex-row">
      <div className="min-h-0 overflow-y-auto border-b lg:w-[28rem] lg:shrink-0 lg:border-r lg:border-b-0">
        <RowTree />
      </div>
      <div className="min-h-0 min-w-0 flex-1 overflow-y-auto">
        <RowPanel />
      </div>
    </div>
  )
}
