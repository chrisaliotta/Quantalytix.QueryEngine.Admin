import { ResizableHandle, ResizablePanel, ResizablePanelGroup } from '@/components/ui/resizable'
import { RowPanel } from './row-panel'
import { RowTree } from './row-tree'

export function RowsTab() {
  return (
    <ResizablePanelGroup orientation="horizontal" className="h-full">
      <ResizablePanel defaultSize="45%" minSize="28%" className="flex min-h-0 flex-col bg-card">
        <RowTree />
      </ResizablePanel>
      <ResizableHandle withHandle />
      <ResizablePanel defaultSize="55%" minSize="35%" className="min-h-0">
        <RowPanel />
      </ResizablePanel>
    </ResizablePanelGroup>
  )
}
