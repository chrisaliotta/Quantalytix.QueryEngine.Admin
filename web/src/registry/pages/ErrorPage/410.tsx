import { ErrorPage, ERROR_PAGE_VARIANTS, type ErrorPageVariantProps } from './Base';

const config = ERROR_PAGE_VARIANTS[410];

export type ErrorPage410Props = ErrorPageVariantProps;

export function ErrorPage410({
  title = config.title,
  description = config.description,
  ...rest
}: ErrorPage410Props) {
  return <ErrorPage statusCode={410} title={title} description={description} {...rest} />;
}
