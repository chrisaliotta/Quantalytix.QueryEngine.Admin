import { ErrorPage, ERROR_PAGE_VARIANTS, type ErrorPageVariantProps } from './Base';

const config = ERROR_PAGE_VARIANTS[500];

export type ErrorPage500Props = ErrorPageVariantProps;

export function ErrorPage500({
  title = config.title,
  description = config.description,
  ...rest
}: ErrorPage500Props) {
  return <ErrorPage statusCode={500} title={title} description={description} {...rest} />;
}
