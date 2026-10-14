import { ErrorPage, ERROR_PAGE_VARIANTS, type ErrorPageVariantProps } from './Base';

const config = ERROR_PAGE_VARIANTS[504];

export type ErrorPage504Props = ErrorPageVariantProps;

export function ErrorPage504({
  title = config.title,
  description = config.description,
  ...rest
}: ErrorPage504Props) {
  return <ErrorPage statusCode={504} title={title} description={description} {...rest} />;
}
