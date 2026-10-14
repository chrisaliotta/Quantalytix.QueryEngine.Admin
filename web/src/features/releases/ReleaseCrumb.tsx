import { useRelease } from './useReleases';

/** Breadcrumb of a release page: the release key once it has loaded. */
export function ReleaseCrumb({ releaseId }: { releaseId: number }) {
  const release = useRelease(releaseId);
  return <>{release.data?.releaseKey ?? 'Release'}</>;
}
