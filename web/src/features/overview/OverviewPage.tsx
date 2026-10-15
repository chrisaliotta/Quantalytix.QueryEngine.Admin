import type { ReactNode } from 'react';
import { Link } from 'react-router';
import { Loading } from '@registry/components/Loading';
import type { components } from '@/api/schema';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { formatDateTime } from '@/shared/formatDateTime';
import { PageHeader } from '@/shared/PageHeader';
import { StatusBadge } from '@/shared/StatusBadge';
import { useTenantId } from '@/shared/useTenantId';
import { useOverview } from './useOverview';

type TenantOverview = components['schemas']['TenantOverview'];

/** The tenant at a glance: identity and revision, the active release, metadata counts and drafts in progress. */
export function OverviewPage() {
  const overview = useOverview();
  return (
    <div data-slot="overview-page" className="space-y-6">
      <PageHeader title="Overview" description="What queries use now, and the work in progress." />
      <ApiErrorAlert error={overview.error} />
      {overview.isPending && <Loading />}
      {overview.data && <OverviewCards overview={overview.data} />}
    </div>
  );
}

function OverviewCards({ overview }: { overview: TenantOverview }) {
  const tenantId = useTenantId();
  const base = `/t/${tenantId}`;
  const { counts, drafts } = overview;
  return (
    <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
      <OverviewCard title="Tenant" description="The metadata database's identity.">
        <StatList>
          <Stat label="Tenant key" value={overview.tenantKey} />
          <Stat label="Schema version" value={overview.metadataSchemaVersion} />
          <Stat label="Semantic revision" value={overview.semanticRevision} />
        </StatList>
      </OverviewCard>
      <OverviewCard title="Active release" description="The release queries use.">
        <ActiveRelease base={base} release={overview.activeRelease} />
      </OverviewCard>
      <OverviewCard title="Metadata" description="What the tenant defines.">
        <StatList>
          <Stat label="Datasets" value={counts.datasets} to={`${base}/datasets`} />
          <Stat label="Dimensions" value={counts.dimensions} to={`${base}/dimensions`} />
          <Stat label="Mapping sets" value={counts.mappingSets} to={`${base}/mappings`} />
          <Stat label="Bridges" value={counts.bridges} to={`${base}/bridges`} />
          <Stat label="Releases" value={counts.releases} to={`${base}/releases`} />
          <Stat label="Assumption sets" value={counts.assumptionSets} to={`${base}/assumption-sets`} />
          <Stat label="Active measures" value={counts.activeMeasures} />
        </StatList>
      </OverviewCard>
      <OverviewCard title="Drafts in progress" description="Versions that queries do not use yet.">
        <StatList>
          <Stat label="Dataset versions" value={drafts.datasetVersions} to={`${base}/datasets`} />
          <Stat label="Dimension versions" value={drafts.dimensionVersions} to={`${base}/dimensions`} />
          <Stat label="Mapping versions" value={drafts.mappingVersions} to={`${base}/mappings`} />
          <Stat label="Bridge versions" value={drafts.bridgeVersions} to={`${base}/bridges`} />
          <Stat label="Releases" value={drafts.releases} to={`${base}/releases`} />
          <Stat label="Assumption sets" value={drafts.assumptionSets} to={`${base}/assumption-sets`} />
        </StatList>
      </OverviewCard>
    </div>
  );
}

function OverviewCard({ title, description, children }: { title: string; description: string; children: ReactNode }) {
  return (
    <Card data-slot="overview-card">
      <CardHeader>
        <CardTitle>{title}</CardTitle>
        <CardDescription>{description}</CardDescription>
      </CardHeader>
      <CardContent className="text-sm">{children}</CardContent>
    </Card>
  );
}

function StatList({ children }: { children: ReactNode }) {
  return <dl className="space-y-2">{children}</dl>;
}

/** One labelled value; the label links to the list it counts, when there is one. */
function Stat({ label, value, to }: { label: string; value: ReactNode; to?: string }) {
  return (
    <div className="flex items-baseline justify-between gap-4">
      <dt className="text-muted-foreground">
        <StatLabel label={label} to={to} />
      </dt>
      <dd className="font-medium tabular-nums">{value}</dd>
    </div>
  );
}

function StatLabel({ label, to }: { label: string; to?: string }) {
  if (!to) {
    return <>{label}</>;
  }
  return (
    <Link to={to} className="hover:text-foreground hover:underline">
      {label}
    </Link>
  );
}

function ActiveRelease({ base, release }: { base: string; release: TenantOverview['activeRelease'] }) {
  if (!release) {
    return (
      <p className="text-muted-foreground">
        No release is active, so queries have no datasets.{' '}
        <Link to={`${base}/releases`} className="underline">
          Open releases
        </Link>
      </p>
    );
  }
  return (
    <StatList>
      <Stat
        label="Release"
        value={
          <Link to={`${base}/releases/${release.releaseId}`} className="hover:underline">
            {release.releaseKey}
          </Link>
        }
      />
      <Stat label="Status" value={<StatusBadge status={release.status} />} />
      <Stat label="Published" value={formatDateTime(release.publishedAt)} />
    </StatList>
  );
}
