import { isRouteErrorResponse, useNavigate, useRouteError } from 'react-router';
import { ErrorPage, ErrorPage404 } from '@registry/pages/ErrorPage';

/** Page for URLs that match no route. */
export function NotFoundPage() {
  const navigate = useNavigate();
  return <ErrorPage404 className="h-auto py-16" onReturnHome={() => navigate('/')} />;
}

/** `errorElement` of the app routes: shows an error thrown while rendering a page. */
export function RouteErrorPage() {
  const error = useRouteError();
  const navigate = useNavigate();

  if (isRouteErrorResponse(error)) {
    return (
      <ErrorPage
        className="h-auto py-16"
        statusCode={error.status}
        title={error.statusText}
        onReturnHome={() => navigate('/')}
      />
    );
  }
  return (
    <ErrorPage
      className="h-auto py-16"
      statusCode={500}
      title="Something went wrong"
      description={(error as Error).message}
      onReturnHome={() => navigate('/')}
    />
  );
}
