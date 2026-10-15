import { format } from 'date-fns';

/** Formats an API timestamp (ISO 8601 UTC) in local time, or an em dash when there is none, e.g. an unpublished release. */
export function formatDateTime(value: string | null | undefined): string {
  if (!value) {
    return '—';
  }
  return format(new Date(value), 'yyyy-MM-dd HH:mm');
}
