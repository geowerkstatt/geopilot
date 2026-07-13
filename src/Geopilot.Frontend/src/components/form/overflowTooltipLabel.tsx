import { FC, ReactNode, useEffect, useRef, useState } from "react";
import { Box, Tooltip } from "@mui/material";

/**
 * Renders label content on a single line with an ellipsis and, only when the text is actually cut off,
 * wraps it in a tooltip revealing the full content.
 */
export const OverflowTooltipLabel: FC<{ children: ReactNode }> = ({ children }) => {
  const ref = useRef<HTMLSpanElement>(null);
  const [overflowing, setOverflowing] = useState(false);

  useEffect(() => {
    const element = ref.current;
    if (!element) return;
    const update = () => setOverflowing(element.scrollWidth > element.clientWidth);
    update();
    const observer = new ResizeObserver(update);
    observer.observe(element);
    return () => observer.disconnect();
  }, [children]);

  return (
    <Tooltip title={overflowing ? children : ""}>
      <Box
        component="span"
        ref={ref}
        sx={{ display: "block", overflow: "hidden", whiteSpace: "nowrap", textOverflow: "ellipsis" }}>
        {children}
      </Box>
    </Tooltip>
  );
};
