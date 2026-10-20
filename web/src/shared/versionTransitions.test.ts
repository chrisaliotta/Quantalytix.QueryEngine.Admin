/**
 * Why these tests exist: the version header offers edits and status changes by kind. A living kind's published version is
 * the one requests read, so it must stay editable and offer no Retire (the next publish retires it), while a strict kind's
 * published version must freeze and keep Retire, because releases pin it.
 */
import { describe, expect, it } from 'vitest';
import { getVersionTransitions, isEditable } from './versionTransitions';

describe('isEditable', () => {
  it("edits drafts of every kind and a living kind's published version, never a retired one", () => {
    expect(isEditable('dimension_version', 'draft')).toBe(true);
    expect(isEditable('report_version', 'published')).toBe(true);
    expect(isEditable('dataset_version', 'published')).toBe(false);
    expect(isEditable('mapping_version', 'retired')).toBe(false);
  });
});

describe('getVersionTransitions', () => {
  it('retires a published version by hand only for strict kinds', () => {
    expect(getVersionTransitions('bridge_version', 'published')).toEqual([]);
    expect(getVersionTransitions('semantic_release', 'published').map(transition => transition.to)).toEqual(['retired']);
  });
});
