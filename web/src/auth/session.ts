const STORAGE_KEY = 'qe-admin.session';

/** Access token kept for the browser tab (sessionStorage). */
export interface StoredSession {
  accessToken: string;
  /** ISO 8601 UTC expiry of the access token. */
  expiresUtc: string;
}

/** Returns the stored token, or null when there is none or it has expired. */
export function readSession(): StoredSession | null {
  const stored = sessionStorage.getItem(STORAGE_KEY);
  if (!stored) {
    return null;
  }
  const session = JSON.parse(stored) as StoredSession;
  if (Date.parse(session.expiresUtc) <= Date.now()) {
    return null;
  }
  return session;
}

/** Persists the token for the current tab. */
export function writeSession(session: StoredSession): void {
  sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session));
}

/** Forgets the stored token. */
export function clearSession(): void {
  sessionStorage.removeItem(STORAGE_KEY);
}
