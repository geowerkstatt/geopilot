/** The nearest ancestor that actually scrolls vertically, or the page. */
const getScroller = (element: HTMLElement): Element => {
  for (let parent = element.parentElement; parent; parent = parent.parentElement) {
    const { overflowY } = getComputedStyle(parent);
    if ((overflowY === "auto" || overflowY === "scroll") && parent.scrollHeight > parent.clientHeight) return parent;
  }
  return document.scrollingElement ?? document.documentElement;
};

/**
 * Scrolls the whole element into view beside its scroll margins (e.g. the page header), or its start where it is
 * taller than that. Computed here, as the browser misplaces an element with scroll margins inside an overflow
 * container that does not scroll itself.
 */
export const scrollIntoFullView = (element: HTMLElement) => {
  const scroller = getScroller(element);
  const isPage = scroller === document.scrollingElement || scroller === document.documentElement;
  const port = isPage ? { top: 0, bottom: window.innerHeight } : scroller.getBoundingClientRect();
  const { scrollMarginTop, scrollMarginBottom } = getComputedStyle(element);
  const minTop = port.top + parseFloat(scrollMarginTop);
  const maxBottom = port.bottom - parseFloat(scrollMarginBottom);
  const { top, bottom } = element.getBoundingClientRect();
  let distance = 0;
  if (top < minTop || bottom - top > maxBottom - minTop) distance = top - minTop;
  else if (bottom > maxBottom) distance = bottom - maxBottom;
  if (distance !== 0) scroller.scrollBy({ top: distance, behavior: "smooth" });
};
