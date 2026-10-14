import { LogOut, UserRound } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { useSession, useSignedInSession } from '@/auth/useSession';

/** Signed-in identity and sign out. */
export function UserMenu() {
  const { me } = useSignedInSession();
  const { signOut } = useSession();

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" size="sm" className="max-w-56">
          <UserRound />
          <span className="truncate">{me.subject}</span>
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-60">
        <DropdownMenuLabel className="space-y-1">
          <div className="truncate">{me.subject}</div>
          {me.tenantId && (
            <div className="text-xs font-normal text-muted-foreground">Tenant: {me.tenantId}</div>
          )}
          {me.isSystemAdmin && <Badge variant="secondary">SystemAdmin</Badge>}
        </DropdownMenuLabel>
        <DropdownMenuSeparator />
        <DropdownMenuItem onSelect={signOut}>
          <LogOut />
          Sign out
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
