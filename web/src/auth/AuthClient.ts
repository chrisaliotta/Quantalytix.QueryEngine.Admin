import { unwrap } from '@/api/problemDetails';
import type { StoredSession } from './session';

/** What the login page collects. */
export interface SignInRequest {
  tenantId: string;
  systemAdmin: boolean;
}

/** Obtains API access tokens. */
export interface AuthClient {
  signIn(request: SignInRequest): Promise<StoredSession>;
}

/** Body of a `POST /development/token` response. */
interface DevelopmentTokenResponse {
  accessToken: string;
  tenantId: string;
  systemAdmin: boolean;
  expiresUtc: string;
}

/**
 * AuthClient backed by the API's `POST /development/token` endpoint. The endpoint exists only when the API runs in
 * Development, so it is not part of the OpenAPI contract and is called with plain `fetch`.
 */
export function createDevelopmentAuthClient(): AuthClient {
  return {
    async signIn(request) {
      const response = await fetch(`${import.meta.env.VITE_API_BASE_URL}/development/token`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(request),
      });
      const body: unknown = await response.json();
      const token = unwrap({ data: body as DevelopmentTokenResponse, error: body, response });
      return { accessToken: token.accessToken, expiresUtc: token.expiresUtc };
    },
  };
}

/** The AuthClient the app signs in with; swap in a production OIDC implementation here. */
export const authClient: AuthClient = createDevelopmentAuthClient();
