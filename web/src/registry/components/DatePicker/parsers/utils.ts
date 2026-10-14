export type ParserGroups = Record<string, string | undefined>;

export function resolveReferenceDate(context: unknown): Date {
  const maybeContext = context as {
    reference?: Date | { instant?: Date };
  };

  if (maybeContext?.reference instanceof Date) {
    return maybeContext.reference;
  }

  if (maybeContext?.reference && 'instant' in maybeContext.reference) {
    const instant = maybeContext.reference.instant;
    if (instant instanceof Date) {
      return instant;
    }
  }

  return new Date();
}

export function resolveYear(groups: ParserGroups, baseYear: number) {
  const yearsAgo = groups.yearsAgo ? Number.parseInt(groups.yearsAgo, 10) : undefined;
  if (yearsAgo !== undefined && Number.isFinite(yearsAgo)) {
    return baseYear - yearsAgo;
  }

  const token = groups.yearToken?.toLowerCase();
  if (!token) {
    return undefined;
  }

  if (token === 'last year') {
    return baseYear - 1;
  }
  if (token === 'this year') {
    return baseYear;
  }
  if (token === 'next year') {
    return baseYear + 1;
  }

  const explicitYear = Number.parseInt(token, 10);
  return Number.isFinite(explicitYear) ? explicitYear : undefined;
}

export function getQuarterFromMonth(monthIndex: number) {
  return Math.floor(monthIndex / 3) + 1;
}

export function getQuarterEndMonthIndex(quarter: number) {
  return quarter * 3 - 1;
}

export function getMonthEndDay(year: number, monthIndex: number) {
  return new Date(year, monthIndex + 1, 0).getDate();
}

export function parseOrdinalQuarter(token: string | undefined) {
  const normalized = token?.toLowerCase();
  switch (normalized) {
    case '1st':
    case 'first':
      return 1;
    case '2nd':
    case 'second':
      return 2;
    case '3rd':
    case 'third':
      return 3;
    case '4th':
    case 'fourth':
      return 4;
    default:
      return undefined;
  }
}

export function parseHalf(token: string | undefined) {
  const normalized = token?.toLowerCase();
  switch (normalized) {
    case 'first':
    case '1st':
    case 'h1':
      return 1;
    case 'second':
    case '2nd':
    case 'h2':
      return 2;
    default:
      return undefined;
  }
}

export function resolveRelativeQuarter(
  currentQuarter: number,
  currentYear: number,
  relativeToken: string,
  yearOverride?: number
) {
  const normalized = relativeToken.toLowerCase();
  let quarter = currentQuarter;
  let year = yearOverride ?? currentYear;

  if (normalized === 'last') {
    quarter -= 1;
  } else if (normalized === 'next') {
    quarter += 1;
  }

  if (quarter < 1) {
    quarter = 4;
    year -= 1;
  } else if (quarter > 4) {
    quarter = 1;
    year += 1;
  }

  return { quarter, year };
}

export function resolveMostRecentHalfYearEnd(referenceDate: Date) {
  const monthIndex = referenceDate.getMonth();
  const year = referenceDate.getFullYear();

  // Jul-Dec -> Jun 30 of current year. Jan-Jun -> Dec 31 of previous year.
  if (monthIndex >= 6) {
    return { year, monthIndex: 5 };
  }

  return { year: year - 1, monthIndex: 11 };
}

export function isSameCalendarDay(left: Date, right: Date) {
  return (
    left.getFullYear() === right.getFullYear() &&
    left.getMonth() === right.getMonth() &&
    left.getDate() === right.getDate()
  );
}
