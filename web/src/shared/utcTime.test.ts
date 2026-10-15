/**
 * Why these tests exist: the metadata stores times in UTC and the version pickers show UTC dates, so every page shows
 * and edits times in UTC as well, whatever the browser's zone. The test run's zone is east of UTC (see `test.env` in
 * vite.config.ts), where a time shown or edited in local time would be nine hours off.
 */
import { describe, expect, it } from 'vitest';
import { formatDateTime, fromDateTimeInput, toDateTimeInput } from './utcTime';

describe('formatDateTime', () => {
  it('shows the UTC date and time to the minute', () => {
    expect(formatDateTime('2026-07-01T00:30:15.592578Z')).toBe('2026-07-01 00:30 UTC');
  });
});

describe('the datetime-local input conversion', () => {
  it('edits the UTC wall-clock time and writes it back as the same UTC time', () => {
    expect(toDateTimeInput('2026-07-01T00:30:00Z')).toBe('2026-07-01T00:30');
    expect(fromDateTimeInput('2026-07-01T00:30')).toBe('2026-07-01T00:30:00.000Z');
  });

  it('maps an empty input to no time', () => {
    expect(toDateTimeInput(null)).toBe('');
    expect(fromDateTimeInput('')).toBeNull();
  });
});
