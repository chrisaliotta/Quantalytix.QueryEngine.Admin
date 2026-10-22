import type { CSSProperties } from 'react';
import { styleCss } from './styleForm';
import type { ReportStyleInput } from './types';

/** A statement row in a style between two rows in the page's own, so the style's rules, indent and colors show in place. */
export function StylePreview({ style }: { style: ReportStyleInput }) {
  return (
    <div data-slot="style-preview" className="overflow-hidden rounded-md border text-sm">
      <PreviewRow label="Row above" amount="1,204.50" />
      <PreviewRow label={style.displayName || 'Styled row'} amount="12,345.67" css={styleCss(style)} />
      <PreviewRow label="Row below" amount="980.25" />
    </div>
  );
}

/** One row of the preview: a label and an amount. */
function PreviewRow({ label, amount, css }: { label: string; amount: string; css?: CSSProperties }) {
  return (
    <div className="flex items-center justify-between gap-4 px-3 py-1.5" style={css}>
      <span className="truncate">{label}</span>
      <span className="tabular-nums">{amount}</span>
    </div>
  );
}
