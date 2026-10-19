import { useMutation } from '@tanstack/react-query';
import { Controller, FormProvider, useForm } from 'react-hook-form';
import { Navigate, useLocation } from 'react-router';
import { Loading } from '@registry/components/Loading';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Field, FieldLabel } from '@/components/ui/field';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { TextField } from '@/shared/form';
import type { SignInRequest } from './AuthClient';
import type { LoginRedirectState } from './RequireSession';
import { useSession } from './useSession';

/** Development sign-in: requests a token for a tenant, optionally as SystemAdmin. */
export function LoginPage() {
  const { session, isLoading, signIn } = useSession();
  const location = useLocation();
  const form = useForm<SignInRequest>({
    defaultValues: { tenantId: 'development', systemAdmin: true },
  });
  const signInMutation = useMutation({ mutationFn: signIn });

  if (isLoading) {
    return <Loading className="h-screen" description="Restoring session..." />;
  }
  if (session) {
    const state = location.state as LoginRedirectState | null;
    return <Navigate to={state?.from ?? '/'} replace />;
  }

  return (
    <div data-slot="login-page" className="flex min-h-screen items-center justify-center p-4">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle>Query Engine Admin</CardTitle>
          <CardDescription>
            Development sign-in: the API issues a token for the tenant below.
          </CardDescription>
        </CardHeader>
        <FormProvider {...form}>
          <form
            className="flex flex-col gap-6"
            onSubmit={form.handleSubmit(values => signInMutation.mutate(values))}
          >
            <CardContent className="space-y-4">
              <TextField name="tenantId" label="Tenant id" />
              <Controller
                control={form.control}
                name="systemAdmin"
                render={({ field }) => (
                  <Field orientation="horizontal">
                    <Checkbox
                      id="systemAdmin"
                      checked={field.value}
                      onCheckedChange={checked => field.onChange(checked === true)}
                    />
                    <FieldLabel htmlFor="systemAdmin">Sign in as SystemAdmin</FieldLabel>
                  </Field>
                )}
              />
              <ApiErrorAlert error={signInMutation.error} />
            </CardContent>
            <CardFooter>
              <Button type="submit" className="w-full" disabled={signInMutation.isPending}>
                Sign in
              </Button>
            </CardFooter>
          </form>
        </FormProvider>
      </Card>
    </div>
  );
}
