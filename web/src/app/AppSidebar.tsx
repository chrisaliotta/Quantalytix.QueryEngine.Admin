import { DatabaseZap } from 'lucide-react';
import { Link, useLocation } from 'react-router';
import {
  Sidebar,
  SidebarContent,
  SidebarGroup,
  SidebarGroupContent,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarRail,
} from '@/components/ui/sidebar';
import { useSignedInSession } from '@/auth/useSession';
import { useTenantId } from '@/shared/useTenantId';
import { getNavigation } from './navigation';

/** Collapsible sidebar with the navigation of the current tenant. */
export function AppSidebar() {
  const { me } = useSignedInSession();
  const tenantId = useTenantId();
  const { pathname } = useLocation();

  return (
    <Sidebar collapsible="icon">
      <SidebarHeader>
        <SidebarMenu>
          <SidebarMenuItem>
            <SidebarMenuButton size="lg" asChild>
              <Link to={`/t/${tenantId}/overview`}>
                <DatabaseZap />
                <span className="font-semibold">Query Engine Admin</span>
              </Link>
            </SidebarMenuButton>
          </SidebarMenuItem>
        </SidebarMenu>
      </SidebarHeader>
      <SidebarContent>
        {getNavigation(me.isSystemAdmin).map(group => (
          <SidebarGroup key={group.title}>
            <SidebarGroupLabel>{group.title}</SidebarGroupLabel>
            <SidebarGroupContent>
              <SidebarMenu>
                {group.items.map(item => {
                  const to = `/t/${tenantId}/${item.path}`;
                  return (
                    <SidebarMenuItem key={item.path}>
                      <SidebarMenuButton
                        asChild
                        isActive={pathname.startsWith(to)}
                        tooltip={item.title}
                      >
                        <Link to={to}>
                          <item.icon />
                          <span>{item.title}</span>
                        </Link>
                      </SidebarMenuButton>
                    </SidebarMenuItem>
                  );
                })}
              </SidebarMenu>
            </SidebarGroupContent>
          </SidebarGroup>
        ))}
      </SidebarContent>
      <SidebarRail />
    </Sidebar>
  );
}
