import { ErrorPage, ERROR_PAGE_VARIANTS, type ErrorPageVariantProps } from './Base';

const config = ERROR_PAGE_VARIANTS[409];

export type ErrorPage409Props = ErrorPageVariantProps;

export function ErrorPage409({
  title = config.title,
  description = config.description,
  ...rest
}: ErrorPage409Props) {
  return <ErrorPage statusCode={409} title={title} description={description} {...rest} />;
}
