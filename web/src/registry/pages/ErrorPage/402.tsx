import { ErrorPage, ERROR_PAGE_VARIANTS, type ErrorPageVariantProps } from './Base';

const config = ERROR_PAGE_VARIANTS[402];

export type ErrorPage402Props = ErrorPageVariantProps;

export function ErrorPage402({
  title = config.title,
  description = config.description,
  ...rest
}: ErrorPage402Props) {
  return <ErrorPage statusCode={402} title={title} description={description} {...rest} />;
}
