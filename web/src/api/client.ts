import createClient, { type Middleware } from 'openapi-fetch';
import { clearSession, readSession } from '@/auth/session';
import type { paths } from './schema';

/** Sends the stored bearer token; a 401 ends the session and returns the user to /login. */
const authMiddleware: Middleware = {
  onRequest({ request }) {
    const session = readSession();
    if (session) {
      request.headers.set('Authorization', `Bearer ${session.accessToken}`);
    }
    return request;
  },
  onResponse({ response }) {
    if (response.status === 401) {
      clearSession();
      window.location.assign('/login');
    }
    return response;
  },
};

/**
 * Typed admin API client: paths, parameters and bodies are checked against the generated `paths`.
 * Pair every call with `unwrap` to get the data or an `ApiProblem`.
 */
export const apiClient = createClient<paths>({ baseUrl: import.meta.env.VITE_API_BASE_URL });

apiClient.use(authMiddleware);
