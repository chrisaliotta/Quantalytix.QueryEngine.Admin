import { ErrorPage, ERROR_PAGE_VARIANTS, type ErrorPageVariantProps } from './Base';

const config = ERROR_PAGE_VARIANTS[503];

export type ErrorPage503Props = ErrorPageVariantProps;

export function ErrorPage503({
  title = config.title,
  description = config.description,
  ...rest
}: ErrorPage503Props) {
  return <ErrorPage statusCode={503} title={title} description={description} {...rest} />;
}
