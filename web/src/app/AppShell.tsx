import { Outlet } from 'react-router';
import { Separator } from '@/components/ui/separator';
import { SidebarInset, SidebarProvider, SidebarTrigger } from '@/components/ui/sidebar';
import { AppBreadcrumbs } from './AppBreadcrumbs';
import { AppSidebar } from './AppSidebar';
import { TenantSwitcher } from './TenantSwitcher';
import { ThemeToggle } from './ThemeToggle';
import { UserMenu } from './UserMenu';

/** Layout of every `/t/:tenantId` page: sidebar, header (breadcrumbs, tenant, theme, user) and content. */
export function AppShell() {
  return (
    <SidebarProvider>
      <AppSidebar />
      <SidebarInset className="min-w-0">
        <header
          data-slot="app-header"
          className="flex h-14 shrink-0 items-center gap-2 border-b px-4"
        >
          <SidebarTrigger className="-ml-1" />
          <Separator orientation="vertical" className="mr-2 data-[orientation=vertical]:h-4" />
          <AppBreadcrumbs />
          <div className="ml-auto flex items-center gap-2">
            <TenantSwitcher />
            <ThemeToggle />
            <UserMenu />
          </div>
        </header>
        <main data-slot="app-content" className="flex-1 p-6">
          <Outlet />
        </main>
      </SidebarInset>
    </SidebarProvider>
  );
}
