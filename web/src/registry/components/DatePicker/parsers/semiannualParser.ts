import {
  getMonthEndDay,
  parseHalf,
  resolveMostRecentHalfYearEnd,
  resolveReferenceDate,
  resolveYear,
  type ParserGroups,
} from './utils';

const SEMIANNUAL_PATTERN =
  /\b(?:(?:(?<yearsAgo>\d+)\s+years?\s+ago|(?<yearToken>last\s+year|this\s+year|next\s+year|(?:19|20)\d{2}))\s+)?(?:(?<halfWord>first|1st|second|2nd)\s+half|(?<halfCode>h1|h2)|(?<semiannual>semi\s*annual(?:\s+statement(?:s)?)?|semiannual(?:\s+statement(?:s)?)?))(?:\s+(?:end(?:\s+of\s+month)?|eom))?\b/i;

export const semiannualParser = {
  pattern: () => SEMIANNUAL_PATTERN,
  extract: (context: unknown, match: RegExpMatchArray) => {
    const groups = (match.groups ?? {}) as ParserGroups;
    const referenceDate = resolveReferenceDate(context);
    const referenceYear = referenceDate.getFullYear();
    const yearOverride = resolveYear(groups, referenceYear);

    const halfToken = groups.halfWord ?? groups.halfCode;
    const half = parseHalf(halfToken);

    let year: number;
    let month: number;

    if (half) {
      year = yearOverride ?? referenceYear;
      month = half === 1 ? 5 : 11;
    } else if (yearOverride !== undefined) {
      year = yearOverride;
      month = 11;
    } else {
      const mostRecent = resolveMostRecentHalfYearEnd(referenceDate);
      year = mostRecent.year;
      month = mostRecent.monthIndex;
    }

    const day = getMonthEndDay(year, month);
    return { year, month: month + 1, day };
  },
};
