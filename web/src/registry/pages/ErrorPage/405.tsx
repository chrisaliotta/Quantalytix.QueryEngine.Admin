import { ErrorPage, ERROR_PAGE_VARIANTS, type ErrorPageVariantProps } from './Base';

const config = ERROR_PAGE_VARIANTS[405];

export type ErrorPage405Props = ErrorPageVariantProps;

export function ErrorPage405({
  title = config.title,
  description = config.description,
  ...rest
}: ErrorPage405Props) {
  return <ErrorPage statusCode={405} title={title} description={description} {...rest} />;
}
