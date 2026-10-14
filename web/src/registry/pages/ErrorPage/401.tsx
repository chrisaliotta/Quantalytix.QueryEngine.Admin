import { ErrorPage, ERROR_PAGE_VARIANTS, type ErrorPageVariantProps } from './Base';

const config = ERROR_PAGE_VARIANTS[401];

export type ErrorPage401Props = ErrorPageVariantProps;

export function ErrorPage401({
  title = config.title,
  description = config.description,
  ...rest
}: ErrorPage401Props) {
  return <ErrorPage statusCode={401} title={title} description={description} {...rest} />;
}
