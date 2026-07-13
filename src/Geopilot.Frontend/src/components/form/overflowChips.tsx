import { useLayoutEffect, useRef, useState } from "react";
import { AutocompleteRenderGetTagProps, Box, Chip } from "@mui/material";

interface OverflowChipsProps {
  /** The selected option labels, in order. */
  value: string[];
  /** MUI's per-tag prop factory from Autocomplete's renderTags. */
  getTagProps: AutocompleteRenderGetTagProps;
}

/**
 * Renders the selected chips on a single row and collapses only the ones that do not fit into a trailing "+N"
 * chip. The available width is measured off the Autocomplete's input root against a hidden row of all chips at
 * their natural width, so the count adapts to the field width and the chips' actual widths (short values stay
 * visible where a fixed limit would hide them). The output does not depend on focus, so the field never jumps.
 */
export const OverflowChips = ({ value, getTagProps }: OverflowChipsProps) => {
  const measureRef = useRef<HTMLDivElement>(null);
  const [visibleCount, setVisibleCount] = useState(value.length);
  // A stable key so the fit is only recomputed when the selection changes, not on every render.
  const valueKey = JSON.stringify(value);

  useLayoutEffect(() => {
    const measure = measureRef.current;
    const inputRoot = measure?.closest<HTMLElement>(".MuiAutocomplete-inputRoot");
    if (!measure || !inputRoot) return;

    const recompute = () => {
      const styles = getComputedStyle(inputRoot);
      const padding = parseFloat(styles.paddingLeft) + parseFloat(styles.paddingRight);
      const available = inputRoot.clientWidth - padding - 52; // 52px reserves room for text input
      const chips = Array.from(measure.children) as HTMLElement[];

      let count = 0;
      for (let i = 0; i < chips.length; i++) {
        const rightEdge = chips[i].offsetLeft + chips[i].offsetWidth;
        // Reserve room for the "+N" chip unless this is the last chip (nothing would overflow then).
        const reservePlus = i < chips.length - 1 ? 44 : 0;
        if (rightEdge + reservePlus <= available) count = i + 1;
        else break;
      }
      setVisibleCount(Math.max(1, count));
    };

    recompute();
    const observer = new ResizeObserver(recompute);
    observer.observe(inputRoot);
    return () => observer.disconnect();
  }, [valueKey]);

  const hidden = value.length - visibleCount;

  const renderChip = (option: string, index: number) => {
    const { key, ...tagProps } = getTagProps({ index });
    return (
      <Chip
        key={key}
        size="small"
        label={option}
        sx={{ "& .MuiChip-deleteIcon": { fontSize: "18px" } }}
        {...tagProps}
      />
    );
  };

  return (
    <>
      {value.slice(0, visibleCount).map((option, index) => renderChip(option, index))}
      {hidden > 0 && <Chip size="small" label={`+${hidden}`} />}
      {/* Hidden measurement row: the identical chips at natural width, taken off the layout, to size the fit. */}
      <Box
        ref={measureRef}
        aria-hidden
        sx={{ position: "absolute", top: 0, left: 0, display: "flex", visibility: "hidden", pointerEvents: "none" }}>
        {value.map((option, index) => renderChip(option, index))}
      </Box>
    </>
  );
};
