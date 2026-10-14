import { ErrorPage, ERROR_PAGE_VARIANTS, type ErrorPageVariantProps } from './Base';

const config = ERROR_PAGE_VARIANTS[403];

export type ErrorPage403Props = ErrorPageVariantProps;

export function ErrorPage403({
  title = config.title,
  description = config.description,
  ...rest
}: ErrorPage403Props) {
  return <ErrorPage statusCode={403} title={title} description={description} {...rest} />;
}
