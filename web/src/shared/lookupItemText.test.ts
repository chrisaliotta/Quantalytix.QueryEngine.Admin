/**
 * Why these tests exist: pickers label lookup items by key and label, and many lookups build the label from the key,
 * which read as "v2 — v2 [2026-07-01 .. open)" or "zz_rel_copy — zz_rel_copy". Each part must show once, while a label
 * that only begins with the key's letters keeps the key in front.
 */
import { describe, expect, it } from 'vitest';
import { lookupItemText } from './lookupItemText';

describe('lookupItemText', () => {
  it('shows a label that starts with the key alone', () => {
    expect(lookupItemText({ id: 1, key: 'v2', label: 'v2 [2026-07-01 .. open)' })).toBe('v2 [2026-07-01 .. open)');
  });

  it('shows a label that is the key once', () => {
    expect(lookupItemText({ id: 2, key: 'zz_rel_copy', label: 'zz_rel_copy' })).toBe('zz_rel_copy');
  });

  it('puts the key in front of any other label', () => {
    expect(lookupItemText({ id: 3, key: 'loan', label: 'loans daily' })).toBe('loan — loans daily');
    expect(lookupItemText({ id: 4, key: 'Branch', label: 'Origination branch' })).toBe('Branch — Origination branch');
  });
});
