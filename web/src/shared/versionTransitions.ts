import type { VersionKind, VersionStatus } from './types';

/** A status change offered for a version in its current status. */
export interface VersionTransition {
  label: string;
  /** Status requested from `PUT .../versions/{kind}/{id}/status`. */
  to: VersionStatus;
  /** Irreversible changes are confirmed first. */
  confirm?: { title: string; description: string; destructive: boolean };
}

/**
 * The living kinds: an identity has one published version, which requests read by default, which stays editable, and which
 * the next publish retires. Dataset versions and releases are strict: a published version never changes and is retired by hand.
 */
const LIVING_KINDS: ReadonlySet<VersionKind> = new Set<VersionKind>(['dimension_version', 'mapping_version', 'bridge_version']);

/** Whether `kind` is a living kind, whose published version is edited live. */
export function isLivingKind(kind: VersionKind) {
  return LIVING_KINDS.has(kind);
}

/**
 * Whether a version accepts content writes: every draft, and the published version of a living kind, whose edits reach the
 * next request.
 * @example isEditable('dimension_version', 'published') // true
 */
export function isEditable(kind: VersionKind, status: VersionStatus | undefined) {
  return status === 'draft' || (status === 'published' && isLivingKind(kind));
}

const validate: VersionTransition = { label: 'Validate', to: 'validated' };

const backToDraft: VersionTransition = { label: 'Back to draft', to: 'draft' };

const publishStrict: VersionTransition = {
  label: 'Publish',
  to: 'published',
  confirm: {
    title: 'Publish this version?',
    description: 'Publishing cannot be undone; a published version can only be retired.',
    destructive: false,
  },
};

const publishLiving: VersionTransition = {
  label: 'Publish',
  to: 'published',
  confirm: {
    title: 'Publish this version?',
    description:
      'The active assumption set pins this version in place of the one published now, which is retired; historical sets keep the versions they captured. Publishing cannot be undone.',
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
const STRICT_TRANSITIONS: Record<VersionStatus, VersionTransition[]> = {
  draft: [validate],
  validated: [publishStrict, backToDraft],
  published: [retire],
  retired: [],
};

/** draft -> validated -> published, and validated -> draft; the next publish retires the published version. */
const LIVING_TRANSITIONS: Record<VersionStatus, VersionTransition[]> = {
  draft: [validate],
  validated: [publishLiving, backToDraft],
  published: [],
  retired: [],
};

/** Status changes the lifecycle of `kind` allows from `status`. */
export function getVersionTransitions(kind: VersionKind, status: VersionStatus) {
  return (isLivingKind(kind) ? LIVING_TRANSITIONS : STRICT_TRANSITIONS)[status];
}
