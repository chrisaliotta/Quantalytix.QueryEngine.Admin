import { useRelease } from './useReleases';

/** Breadcrumb of a release page: the release key once it has loaded. */
export function ReleaseCrumb({ semanticReleaseId }: { semanticReleaseId: number }) {
  const release = useRelease(semanticReleaseId);
  return <>{release.data?.releaseKey ?? 'Release'}</>;
}
