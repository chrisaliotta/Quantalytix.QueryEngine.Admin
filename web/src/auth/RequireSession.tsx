import { Navigate, Outlet, useLocation, type Location } from 'react-router';
import { Loading } from '@registry/components/Loading';
import { ErrorPage } from '@registry/pages/ErrorPage';
import { ApiProblem } from '@/api/problemDetails';
import { useSession } from './useSession';

/** Router state given to /login so it can return the user to the page they asked for. */
export interface LoginRedirectState {
  from: Location;
}

/** Layout route that renders its child routes for a signed-in user and redirects to /login otherwise. */
export function RequireSession() {
  const { session, isLoading, error, signOut } = useSession();
  const location = useLocation();

  if (isLoading) {
    return <Loading className="h-screen" description="Loading session..." />;
  }
  if (error) {
    return (
      <ErrorPage
        statusCode={error instanceof ApiProblem ? error.status : 503}
        title="Your session could not be loaded"
        description={error.message}
        actions={[{ label: 'Sign in again', onClick: signOut }]}
      />
    );
  }
  if (!session) {
    const state: LoginRedirectState = { from: location };
    return <Navigate to="/login" replace state={state} />;
  }
  return <Outlet />;
}
