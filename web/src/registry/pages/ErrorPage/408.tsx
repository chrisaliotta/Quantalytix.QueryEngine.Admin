import { ErrorPage, ERROR_PAGE_VARIANTS, type ErrorPageVariantProps } from './Base';

const config = ERROR_PAGE_VARIANTS[408];

export type ErrorPage408Props = ErrorPageVariantProps;

export function ErrorPage408({
  title = config.title,
  description = config.description,
  ...rest
}: ErrorPage408Props) {
  return <ErrorPage statusCode={408} title={title} description={description} {...rest} />;
}
