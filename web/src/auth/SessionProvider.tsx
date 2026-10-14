import { queryOptions, useQuery, useQueryClient } from '@tanstack/react-query';
import { useMemo, useState, type ReactNode } from 'react';
import { apiClient } from '@/api/client';
import { unwrap } from '@/api/problemDetails';
import { authClient, type SignInRequest } from './AuthClient';
import { clearSession, readSession, writeSession, type StoredSession } from './session';
import { SessionContext, type SessionState } from './useSession';

const meQuery = queryOptions({
  queryKey: ['session', 'me'],
  queryFn: async () => unwrap(await apiClient.GET('/api/me')),
  staleTime: Infinity,
});

/** Restores the stored token, loads `GET /api/me` and provides signIn/signOut to `useSession()`. */
export function SessionProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const [token, setToken] = useState<StoredSession | null>(readSession);
  const me = useQuery({ ...meQuery, enabled: token !== null });

  const state = useMemo<SessionState>(() => {
    async function signIn(request: SignInRequest) {
      const signedIn = await authClient.signIn(request);
      writeSession(signedIn);
      await queryClient.fetchQuery({ ...meQuery, staleTime: 0 });
      setToken(signedIn);
    }

    function signOut() {
      clearSession();
      queryClient.clear();
      setToken(null);
    }

    return {
      session: token && me.data ? { ...token, me: me.data } : null,
      isLoading: token !== null && me.isPending,
      error: me.error,
      signIn,
      signOut,
    };
  }, [token, me.data, me.isPending, me.error, queryClient]);

  return <SessionContext value={state}>{children}</SessionContext>;
}
