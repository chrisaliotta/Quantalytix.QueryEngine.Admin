import { useBridge } from './api';

/** Breadcrumb label of a bridge: its key once loaded. */
export function BridgeCrumb({ bridgeId }: { bridgeId: number }) {
  const bridge = useBridge(bridgeId);
  return <>{bridge.data?.bridgeKey ?? 'Bridge'}</>;
}
