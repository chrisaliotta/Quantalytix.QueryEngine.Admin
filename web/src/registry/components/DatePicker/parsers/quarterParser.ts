import {
  getMonthEndDay,
  getQuarterEndMonthIndex,
  getQuarterFromMonth,
  parseOrdinalQuarter,
  resolveReferenceDate,
  resolveRelativeQuarter,
  resolveYear,
  type ParserGroups,
} from './utils';

const QUARTER_PATTERN =
  /\b(?:(?:(?<yearsAgo>\d+)\s+years?\s+ago|(?<yearToken>last\s+year|this\s+year|next\s+year|(?:19|20)\d{2}))\s+)?(?:(?:end\s+of\s+)?(?<ordinalQuarter>1st|2nd|3rd|4th|first|second|third|fourth)\s+quarter(?:\s+(?:end(?:\s+of\s+month)?|eoq|eom))?|(?<relativeQuarter>last|this|next)\s+quarter(?:\s+(?:end(?:\s+of\s+month)?|eoq|eom))?|quarter\s*end(?:\s+of\s+month)?|eoq)\b/i;

export const quarterParser = {
  pattern: () => QUARTER_PATTERN,
  extract: (context: unknown, match: RegExpMatchArray) => {
    const groups = (match.groups ?? {}) as ParserGroups;
    const referenceDate = resolveReferenceDate(context);
    const referenceYear = referenceDate.getFullYear();
    const currentQuarter = getQuarterFromMonth(referenceDate.getMonth());
    const yearOverride = resolveYear(groups, referenceYear);

    let quarter: number;
    let year: number;

    const ordinalQuarter = parseOrdinalQuarter(groups.ordinalQuarter);
    if (ordinalQuarter) {
      quarter = ordinalQuarter;
      year = yearOverride ?? referenceYear;
    } else if (groups.relativeQuarter) {
      const resolved = resolveRelativeQuarter(
        currentQuarter,
        referenceYear,
        groups.relativeQuarter,
        yearOverride
      );
      quarter = resolved.quarter;
      year = resolved.year;
    } else {
      quarter = currentQuarter;
      year = yearOverride ?? referenceYear;
    }

    const month = getQuarterEndMonthIndex(quarter);
    const day = getMonthEndDay(year, month);
    return { year, month: month + 1, day };
  },
};
