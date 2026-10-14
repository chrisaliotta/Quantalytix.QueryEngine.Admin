import { ErrorPage, ERROR_PAGE_VARIANTS, type ErrorPageVariantProps } from './Base';

const config = ERROR_PAGE_VARIANTS[502];

export type ErrorPage502Props = ErrorPageVariantProps;

export function ErrorPage502({
  title = config.title,
  description = config.description,
  ...rest
}: ErrorPage502Props) {
  return <ErrorPage statusCode={502} title={title} description={description} {...rest} />;
}
