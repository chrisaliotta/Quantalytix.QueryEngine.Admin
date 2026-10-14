import { CircleAlert } from 'lucide-react';
import { ApiProblem } from '@/api/problemDetails';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';

/**
 * Destructive alert for a failed request: the ProblemDetails title and detail plus any validation
 * messages or rule violations. Renders nothing when `error` is null.
 */
export function ApiErrorAlert({ error }: { error: Error | null }) {
  if (!error) {
    return null;
  }
  return (
    <Alert variant="destructive" data-slot="api-error-alert">
      <CircleAlert />
      <AlertTitle>{error instanceof ApiProblem ? error.title : 'Request failed'}</AlertTitle>
      <AlertDescription>
        <ErrorDescription error={error} />
      </AlertDescription>
    </Alert>
  );
}

function ErrorDescription({ error }: { error: Error }) {
  if (!(error instanceof ApiProblem)) {
    return <p>{error.message}</p>;
  }
  return (
    <>
      {error.detail && <p>{error.detail}</p>}
      {error.errors && (
        <ul data-slot="api-error-alert-errors" className="list-disc pl-4">
          {Object.entries(error.errors).flatMap(([field, messages]) =>
            messages.map(message => <li key={`${field}:${message}`}>{message}</li>)
          )}
        </ul>
      )}
      {error.violations && (
        <ul data-slot="api-error-alert-violations" className="list-disc pl-4">
          {error.violations.map(violation => (
            <li key={`${violation.rule}:${violation.message}`}>
              {violation.message} <code className="text-xs opacity-70">{violation.rule}</code>
            </li>
          ))}
        </ul>
      )}
    </>
  );
}
