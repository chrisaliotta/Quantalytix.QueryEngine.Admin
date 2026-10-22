/**
 * Why these tests exist: the styles tab and the style sheet's live preview render a style as the report's clients would, so each
 * stored value must map to its CSS: weights to CSS weights, rules to borders in the text color, sizes in points and the indent as
 * levels of the label; a value the style leaves null must keep the page's own. The sheet sends a blank text as null, which keeps
 * the client's own rather than storing an empty color or font.
 */
import { describe, expect, it } from 'vitest';
import { styleCss, toStyleInput, toStyleValues } from './styleForm';
import type { ReportStyle } from './types';

/** A bold total style with a rule above and a double rule below, as the seed's reports have. */
const total: ReportStyle = {
  reportId: 60,
  styleKey: 'total',
  displayName: 'Total',
  defaultRole: 'total',
  fontSize: 11,
  fontWeight: 'bold',
  isItalic: false,
  textColor: '#111827',
  backgroundColor: '#F3F4F6',
  borderTop: 'thin',
  borderBottom: 'double',
  indent: 2,
};

describe('styleCss', () => {
  it('maps each stored value to its CSS', () => {
    expect(styleCss(total)).toEqual({
      fontFamily: undefined,
      fontSize: '11pt',
      fontWeight: 700,
      fontStyle: undefined,
      color: '#111827',
      backgroundColor: '#F3F4F6',
      borderTop: '1px solid',
      borderBottom: '3px double',
      textIndent: '2.5rem',
    });
  });

  it('keeps the page\'s own for every value the style leaves null, and sets italics alone', () => {
    const css = styleCss({ displayName: 'Note', isItalic: true });

    expect(Object.entries(css).filter(([, value]) => value !== undefined)).toEqual([['fontStyle', 'italic']]);
  });
});

describe('toStyleInput', () => {
  it('sends blank texts as null and keeps the rest as edited', () => {
    const values = { ...toStyleValues(total), fontFamily: '  ', textColor: '' };

    expect(toStyleInput(values)).toMatchObject({ fontFamily: null, textColor: null, backgroundColor: '#F3F4F6', borderBottom: 'double', indent: 2 });
  });
});
