import type { CSSProperties } from 'react';
import { emptyToNull } from '@/shared/text';
import type { ReportStyle, ReportStyleInput } from './types';

/** Values of the style sheet; a choice not made yet is null, and an optional text is empty for none. */
export interface StyleValues {
  styleKey: string;
  displayName: string;
  defaultRole: string | null;
  fontFamily: string;
  fontSize: number | null;
  fontWeight: string | null;
  isItalic: boolean;
  textColor: string;
  backgroundColor: string;
  borderTop: string | null;
  borderBottom: string | null;
  indent: number | null;
}

/** The CSS weight of each `font_weight` value. */
const FONT_WEIGHTS: Record<string, number> = { normal: 400, medium: 500, semibold: 600, bold: 700 };

/** The CSS border of each `border_style` value, drawn in the text color. */
const BORDERS: Record<string, string> = { none: 'none', thin: '1px solid', medium: '2px solid', thick: '3px solid', double: '3px double' };

/** The width of one indent level, in rem. */
const INDENT_REM = 1.25;

/** A stored style as the sheet edits it, or a new style that sets nothing but what the sheet fills in. */
export function toStyleValues(style: ReportStyle | null): StyleValues {
  return {
    styleKey: style?.styleKey ?? '',
    displayName: style?.displayName ?? '',
    defaultRole: style?.defaultRole ?? null,
    fontFamily: style?.fontFamily ?? '',
    fontSize: style?.fontSize ?? null,
    fontWeight: style?.fontWeight ?? null,
    isItalic: style?.isItalic ?? false,
    textColor: style?.textColor ?? '',
    backgroundColor: style?.backgroundColor ?? '',
    borderTop: style?.borderTop ?? null,
    borderBottom: style?.borderBottom ?? null,
    indent: style?.indent ?? null,
  };
}

/** The body the API takes; a blank text is null, which keeps the client's own. */
export function toStyleInput(values: StyleValues): ReportStyleInput {
  return {
    displayName: values.displayName,
    defaultRole: values.defaultRole,
    fontFamily: emptyToNull(values.fontFamily),
    fontSize: values.fontSize,
    fontWeight: values.fontWeight,
    isItalic: values.isItalic,
    textColor: emptyToNull(values.textColor),
    backgroundColor: emptyToNull(values.backgroundColor),
    borderTop: values.borderTop,
    borderBottom: values.borderBottom,
    indent: values.indent,
  };
}

/** How a style shows a row, as CSS on the row; a property the style leaves null keeps the page's own, and the indent indents the label. */
export function styleCss(style: ReportStyleInput): CSSProperties {
  return {
    fontFamily: style.fontFamily ?? undefined,
    fontSize: style.fontSize == null ? undefined : `${style.fontSize}pt`,
    fontWeight: style.fontWeight == null ? undefined : FONT_WEIGHTS[style.fontWeight],
    fontStyle: style.isItalic ? 'italic' : undefined,
    color: style.textColor ?? undefined,
    backgroundColor: style.backgroundColor ?? undefined,
    borderTop: style.borderTop == null ? undefined : BORDERS[style.borderTop],
    borderBottom: style.borderBottom == null ? undefined : BORDERS[style.borderBottom],
    textIndent: style.indent == null ? undefined : `${style.indent * INDENT_REM}rem`,
  };
}
