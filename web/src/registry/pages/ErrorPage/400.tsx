import { ErrorPage, ERROR_PAGE_VARIANTS, type ErrorPageVariantProps } from './Base';

const config = ERROR_PAGE_VARIANTS[400];

export type ErrorPage400Props = ErrorPageVariantProps;

export function ErrorPage400({
  title = config.title,
  description = config.description,
  ...rest
}: ErrorPage400Props) {
  return <ErrorPage statusCode={400} title={title} description={description} {...rest} />;
}
