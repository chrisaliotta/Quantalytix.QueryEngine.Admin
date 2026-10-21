import { useBridge } from './api';

/** Breadcrumb label of a bridge: its key once loaded. */
export function BridgeCrumb({ bridgeId }: { bridgeId: number }) {
  const summary = useBridge(bridgeId);
  return <>{summary.data?.bridge.bridgeKey ?? 'Bridge'}</>;
}
