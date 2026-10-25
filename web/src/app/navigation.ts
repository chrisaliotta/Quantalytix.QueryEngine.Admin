import {
  Boxes,
  Building2,
  Calculator,
  Database,
  FileSpreadsheet,
  LayoutDashboard,
  Rocket,
  SlidersHorizontal,
  type LucideIcon,
} from 'lucide-react';

/** A sidebar link; `path` is relative to `/t/:tenantId`. */
export interface NavItem {
  title: string;
  path: string;
  icon: LucideIcon;
}

/** A labelled group of sidebar links. */
export interface NavGroup {
  title: string;
  items: NavItem[];
  systemAdminOnly?: boolean;
}

const NAVIGATION: NavGroup[] = [
  {
    title: 'Semantic model',
    items: [
      { title: 'Overview', path: 'overview', icon: LayoutDashboard },
      { title: 'Datasets', path: 'datasets', icon: Database },
      { title: 'Dimensions', path: 'dimensions', icon: Boxes },
      { title: 'Releases', path: 'releases', icon: Rocket },
      { title: 'Assumption sets', path: 'assumption-sets', icon: SlidersHorizontal },
    ],
  },
  {
    title: 'Reporting',
    items: [
      { title: 'Calculation models', path: 'calculation-models', icon: Calculator },
      { title: 'Reports', path: 'reports', icon: FileSpreadsheet },
    ],
  },
  {
    title: 'System',
    systemAdminOnly: true,
    items: [{ title: 'Tenants', path: 'system/tenants', icon: Building2 }],
  },
];

/** Sidebar groups visible to the user; System is for SystemAdmins only. */
export function getNavigation(isSystemAdmin: boolean): NavGroup[] {
  return NAVIGATION.filter(group => !group.systemAdminOnly || isSystemAdmin);
}
