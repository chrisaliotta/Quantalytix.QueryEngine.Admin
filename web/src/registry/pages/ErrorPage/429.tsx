import { ErrorPage, ERROR_PAGE_VARIANTS, type ErrorPageVariantProps } from './Base';

const config = ERROR_PAGE_VARIANTS[429];

export type ErrorPage429Props = ErrorPageVariantProps;

export function ErrorPage429({
  title = config.title,
  description = config.description,
  ...rest
}: ErrorPage429Props) {
  return <ErrorPage statusCode={429} title={title} description={description} {...rest} />;
}
