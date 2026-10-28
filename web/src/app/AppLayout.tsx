import { Outlet } from 'react-router'
import { AppSidebar } from '@/components/app-sidebar'
import { ChangesProvider } from '@/components/shell/changes'
import { HistoryProvider } from '@/components/shell/history'
import { TooltipProvider } from '@/components/ui/tooltip'
import { DimensionsApiProvider } from '@/lib/dimensions/client'
import { MappingsApiProvider } from '@/lib/mappings/client'
import { ModelsApiProvider } from '@/lib/models/client'
import { ReleasesApiProvider } from '@/lib/releases/client'
import { ReportsApiProvider } from '@/lib/reports/client'

/** Workspace shell: the shared stores and adapters, the section sidebar and the routed page. */
export function AppLayout() {
  return (
    <HistoryProvider>
      <ReportsApiProvider>
        <DimensionsApiProvider>
          <MappingsApiProvider>
            <ReleasesApiProvider>
              <ModelsApiProvider>
                <TooltipProvider delay={300}>
                  <ChangesProvider>
                    <div className="flex h-dvh min-h-0 flex-col md:flex-row">
                      <AppSidebar />
                      <div className="flex min-h-0 min-w-0 flex-1 flex-col">
                        <Outlet />
                      </div>
                    </div>
                  </ChangesProvider>
                </TooltipProvider>
              </ModelsApiProvider>
            </ReleasesApiProvider>
          </MappingsApiProvider>
        </DimensionsApiProvider>
      </ReportsApiProvider>
    </HistoryProvider>
  )
}
