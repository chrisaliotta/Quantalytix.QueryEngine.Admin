import { Fragment } from 'react';
import { Link, useMatches, type UIMatch } from 'react-router';
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from '@/components/ui/breadcrumb';
import type { RouteHandle } from '@/shared/routeHandle';

type CrumbMatch = UIMatch<unknown, RouteHandle>;

/** Breadcrumbs built from the `handle.crumb` of every matched route. */
export function AppBreadcrumbs() {
  const crumbs = useMatches().filter(
    (match): match is CrumbMatch => (match.handle as RouteHandle | undefined)?.crumb !== undefined
  );

  return (
    <Breadcrumb>
      <BreadcrumbList>
        {crumbs.map((match, index) => (
          <Fragment key={match.id}>
            {index > 0 && <BreadcrumbSeparator />}
            <BreadcrumbItem>
              <Crumb match={match} isCurrent={index === crumbs.length - 1} />
            </BreadcrumbItem>
          </Fragment>
        ))}
      </BreadcrumbList>
    </Breadcrumb>
  );
}

function Crumb({ match, isCurrent }: { match: CrumbMatch; isCurrent: boolean }) {
  const { crumb } = match.handle;
  const label = typeof crumb === 'string' ? crumb : crumb(match);
  if (isCurrent) {
    return <BreadcrumbPage>{label}</BreadcrumbPage>;
  }
  return (
    <BreadcrumbLink asChild>
      <Link to={match.pathname}>{label}</Link>
    </BreadcrumbLink>
  );
}
