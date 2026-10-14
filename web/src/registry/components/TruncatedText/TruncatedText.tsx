import * as React from 'react';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { cn } from '@/lib/utils';

function getLineClampClass(lines: number) {
  return `line-clamp-${lines}`;
}

// Keep block layout in tooltips; inline paragraphs in the clamped view so line-clamp applies.
const HTML_BLOCK_CLASS =
  '[&_p]:my-0 [&_p:first-child]:mt-0 [&_p:last-child]:mb-0 [&_p]:leading-normal';

function TruncatedText({
  text = '',
  className = '',
  lines = 1,
  isHtml = false,
}: {
  text: string;
  className?: string;
  lines?: number;
  isHtml?: boolean;
}) {
  const ref = React.useRef<HTMLDivElement>(null);
  const measureRef = React.useRef<HTMLDivElement>(null);
  const wrapperRef = React.useRef<HTMLDivElement>(null);
  const [showTooltip, setShowTooltip] = React.useState(false);
  const isMultiLine = lines > 1;

  const checkTextOverflow = () => {
    const el = ref.current;
    if (!el) return;

    if (isHtml) {
      const measureEl = measureRef.current;
      if (!measureEl) return;
      setShowTooltip(measureEl.scrollHeight > el.clientHeight);
      return;
    }

    setShowTooltip(
      isMultiLine ? el.scrollHeight > el.clientHeight : el.scrollWidth > el.clientWidth
    );
  };

  React.useEffect(() => {
    const observeTarget = wrapperRef.current ?? ref.current;
    if (!observeTarget) return;

    checkTextOverflow();

    const resizeObserver = new ResizeObserver(checkTextOverflow);
    resizeObserver.observe(observeTarget);
    window.addEventListener('resize', checkTextOverflow);

    return () => {
      resizeObserver.disconnect();
      window.removeEventListener('resize', checkTextOverflow);
    };
  }, [text, isMultiLine, isHtml]);

  const truncateClass = isHtml
    ? cn('break-words', getLineClampClass(lines))
    : isMultiLine
      ? cn('break-words', getLineClampClass(lines))
      : 'truncate';

  const htmlProps = isHtml ? { dangerouslySetInnerHTML: { __html: text } } : { children: text };

  const truncatedElement = (
    <div
      ref={ref}
      onMouseEnter={checkTextOverflow}
      className={cn(
        'block w-full overflow-hidden',
        truncateClass,
        isHtml && HTML_BLOCK_CLASS,
        className
      )}
      {...htmlProps}
    />
  );

  const content = isHtml ? (
    <div ref={wrapperRef} className="relative w-full min-w-0">
      <div
        ref={measureRef}
        aria-hidden
        className={cn(
          'pointer-events-none invisible absolute top-0 left-0 w-full',
          HTML_BLOCK_CLASS
        )}
        dangerouslySetInnerHTML={{ __html: text }}
      />
      {truncatedElement}
    </div>
  ) : (
    truncatedElement
  );

  if (!showTooltip) return content;

  return (
    <Tooltip>
      <TooltipTrigger asChild>{content}</TooltipTrigger>
      <TooltipContent className="!max-w-[500px] w-[min(500px,max-content)] min-w-0 whitespace-normal !text-wrap text-left">
        {isHtml ? (
          <div className={HTML_BLOCK_CLASS} dangerouslySetInnerHTML={{ __html: text }} />
        ) : (
          text
        )}
      </TooltipContent>
    </Tooltip>
  );
}

export { TruncatedText };
