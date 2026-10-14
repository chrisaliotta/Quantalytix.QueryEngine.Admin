const SMALL_NUMBER_WORDS: Record<string, number> = {
  zero: 0,
  one: 1,
  two: 2,
  three: 3,
  four: 4,
  five: 5,
  six: 6,
  seven: 7,
  eight: 8,
  nine: 9,
  ten: 10,
  eleven: 11,
  twelve: 12,
  thirteen: 13,
  fourteen: 14,
  fifteen: 15,
  sixteen: 16,
  seventeen: 17,
  eighteen: 18,
  nineteen: 19,
};

const TENS_NUMBER_WORDS: Record<string, number> = {
  twenty: 20,
  thirty: 30,
  forty: 40,
  fifty: 50,
  sixty: 60,
  seventy: 70,
  eighty: 80,
  ninety: 90,
};

const NUMBER_WORD_TOKEN =
  '(?:zero|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve|thirteen|fourteen|fifteen|sixteen|seventeen|eighteen|nineteen|twenty|thirty|forty|fifty|sixty|seventy|eighty|ninety|hundred|thousand|and)';

const SPELLED_TEMPORAL_COUNT_PATTERN = new RegExp(
  String.raw`\b(?<countWords>${NUMBER_WORD_TOKEN}(?:[\s-]+${NUMBER_WORD_TOKEN})*)\s+(?<unit>days?|weeks?|months?|years?|quarters?)\b`,
  'gi'
);

function parseSpelledNumberWords(words: string): number | undefined {
  const tokens = words.toLowerCase().trim().split(/[\s-]+/).filter(Boolean);
  if (tokens.length === 0) {
    return undefined;
  }

  let total = 0;
  let current = 0;
  let seenNumericToken = false;

  for (const token of tokens) {
    if (token === 'and') {
      continue;
    }

    const smallValue = SMALL_NUMBER_WORDS[token];
    if (smallValue !== undefined) {
      current += smallValue;
      seenNumericToken = true;
      continue;
    }

    const tensValue = TENS_NUMBER_WORDS[token];
    if (tensValue !== undefined) {
      current += tensValue;
      seenNumericToken = true;
      continue;
    }

    if (token === 'hundred') {
      current = (current || 1) * 100;
      seenNumericToken = true;
      continue;
    }

    if (token === 'thousand') {
      total += (current || 1) * 1000;
      current = 0;
      seenNumericToken = true;
      continue;
    }

    return undefined;
  }

  if (!seenNumericToken) {
    return undefined;
  }

  return total + current;
}

export function normalizeSpelledTemporalCounts(text: string) {
  return text.replace(SPELLED_TEMPORAL_COUNT_PATTERN, (match, _countWords, _unit, _offset, _source, groups) => {
    const parsedGroups = groups as { countWords?: string; unit?: string } | undefined;
    if (!parsedGroups?.countWords || !parsedGroups.unit) {
      return match;
    }

    const numericCount = parseSpelledNumberWords(parsedGroups.countWords);
    if (numericCount === undefined) {
      return match;
    }

    return `${numericCount} ${parsedGroups.unit}`;
  });
}
