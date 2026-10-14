import { createContext, use } from 'react';
import type { Me } from '@/shared/types';
import type { SignInRequest } from './AuthClient';
import type { StoredSession } from './session';

/** The signed-in user: access token plus the `GET /api/me` identity. */
export interface Session extends StoredSession {
  me: Me;
}

/** Value provided by `SessionProvider`. */
export interface SessionState {
  /** Null while signed out or while `isLoading`. */
  session: Session | null;
  /** True while the identity of a restored token loads. */
  isLoading: boolean;
  /** Set when `GET /api/me` failed for a reason other than 401. */
  error: Error | null;
  /** Obtains a token through the AuthClient and loads the identity. */
  signIn: (request: SignInRequest) => Promise<void>;
  /** Forgets the token and every cached query. */
  signOut: () => void;
}

/** Provided by `SessionProvider`; read it through `useSession()`. */
export const SessionContext = createContext<SessionState | null>(null);

/** Session state with signIn/signOut; use below `SessionProvider`. */
export function useSession(): SessionState {
  return use(SessionContext) as SessionState;
}

/** The signed-in session; use below `RequireSession`, which guarantees one. */
export function useSignedInSession(): Session {
  return useSession().session as Session;
}
