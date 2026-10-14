import * as chrono from 'chrono-node';
import { quarterParser } from './quarterParser';
import { semiannualParser } from './semiannualParser';
import { normalizeSpelledTemporalCounts } from './temporalNumberNormalizer';
export type { DatePickerPresetOption } from './presets';
export { DEFAULT_DATE_PICKER_PRESETS } from './presets';

const periodChrono = chrono.casual.clone();
periodChrono.parsers.unshift(semiannualParser);
periodChrono.parsers.unshift(quarterParser);

export function parsePeriodDate(text: string, referenceDate = new Date()) {
  const normalizedText = normalizeSpelledTemporalCounts(text);
  return periodChrono.parseDate(normalizedText, referenceDate);
}
