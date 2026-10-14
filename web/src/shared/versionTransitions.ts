import type { VersionKind, VersionStatus } from './types';

/** A status change offered for a version in its current status. */
export interface VersionTransition {
  label: string;
  /** Status requested from `PUT .../versions/{kind}/{id}/status`. */
  to: VersionStatus;
  /** Irreversible changes are confirmed first. */
  confirm?: { title: string; description: string; destructive: boolean };
}

const publish: VersionTransition = {
  label: 'Publish',
  to: 'published',
  confirm: {
    title: 'Publish this version?',
    description: 'Publishing cannot be undone; a published version can only be retired.',
    destructive: false,
  },
};

const retire: VersionTransition = {
  label: 'Retire',
  to: 'retired',
  confirm: {
    title: 'Retire this version?',
    description: 'Retiring cannot be undone.',
    destructive: true,
  },
};

/** draft -> validated -> published -> retired, and validated -> draft. */
const VERSION_TRANSITIONS: Record<VersionStatus, VersionTransition[]> = {
  draft: [{ label: 'Validate', to: 'validated' }],
  validated: [publish, { label: 'Back to draft', to: 'draft' }],
  published: [retire],
  retired: [],
};

/** Assumption sets skip validation: draft -> published -> retired. */
const ASSUMPTION_SET_TRANSITIONS: Record<VersionStatus, VersionTransition[]> = {
  draft: [publish],
  validated: [],
  published: [retire],
  retired: [],
};

/** Status changes the lifecycle of `kind` allows from `status`. */
export function getVersionTransitions(kind: VersionKind, status: VersionStatus) {
  if (kind === 'assumption_set') {
    return ASSUMPTION_SET_TRANSITIONS[status];
  }
  return VERSION_TRANSITIONS[status];
}
