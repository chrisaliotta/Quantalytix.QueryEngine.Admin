import { ErrorPage, ERROR_PAGE_VARIANTS, type ErrorPageVariantProps } from './Base';

const config = ERROR_PAGE_VARIANTS[404];

export type ErrorPage404Props = ErrorPageVariantProps;

export function ErrorPage404({
  title = config.title,
  description = config.description,
  ...rest
}: ErrorPage404Props) {
  return <ErrorPage statusCode={404} title={title} description={description} {...rest} />;
}
