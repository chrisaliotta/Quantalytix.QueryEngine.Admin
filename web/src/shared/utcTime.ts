/** The UTC date and time of an ISO 8601 time to the minute, as `yyyy-MM-ddTHH:mm`. */
function utcMinute(iso: string): string {
  return new Date(iso).toISOString().slice(0, 16);
}

/**
 * Formats an API timestamp (ISO 8601 UTC) in UTC, the way the metadata stores it, or an em dash when there is none, e.g.
 * an unpublished release.
 */
export function formatDateTime(value: string | null | undefined): string {
  if (!value) {
    return '—';
  }
  return `${utcMinute(value).replace('T', ' ')} UTC`;
}

/** The text a `datetime-local` input shows for an ISO 8601 UTC time: its UTC wall-clock time, or empty for none. */
export function toDateTimeInput(utcIso: string | null): string {
  if (!utcIso) {
    return '';
  }
  return utcMinute(utcIso);
}

/** The ISO 8601 UTC time a `datetime-local` input's text names, read as UTC; null for an empty input. */
export function fromDateTimeInput(text: string): string | null {
  if (!text) {
    return null;
  }
  return new Date(`${text}Z`).toISOString();
}
