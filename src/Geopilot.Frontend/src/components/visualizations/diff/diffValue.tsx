import { FC, useLayoutEffect, useRef, useState } from "react";
import { Box, Stack, Typography } from "@mui/material";
import { DiffSide } from "../../../api/apiInterfaces";
import { Button } from "../../buttons";
import { MARK_BACKGROUNDS } from "./diffObjects";
import { ValueSegment } from "./valueDiff";

const CLAMPED_LINES = 4;

interface DiffValueTextProps {
  segments: ValueSegment[];
  side: DiffSide;
  monospace?: boolean;
}

/** Offers "show more" only when the value actually overflows four lines at the current width. */
export const DiffValueText: FC<DiffValueTextProps> = ({ segments, side, monospace = false }) => {
  const textRef = useRef<HTMLSpanElement>(null);
  const [expanded, setExpanded] = useState(false);
  const [overflowing, setOverflowing] = useState(false);

  // Only the clamped text can tell whether it overflows; expanded, the last measurement keeps the button.
  useLayoutEffect(() => {
    const element = textRef.current;
    if (!element || expanded) return;
    const measure = () => setOverflowing(element.scrollHeight > element.clientHeight + 1);
    measure();
    const observer = new ResizeObserver(measure);
    observer.observe(element);
    return () => observer.disconnect();
  }, [segments, expanded]);

  return (
    <Stack sx={{ gap: 0, minWidth: 0, alignItems: "flex-start" }}>
      <Typography
        ref={textRef}
        variant="body2"
        component="span"
        sx={{
          display: "-webkit-box",
          WebkitBoxOrient: "vertical",
          WebkitLineClamp: expanded ? "none" : CLAMPED_LINES,
          overflow: "hidden",
          overflowWrap: "anywhere",
          whiteSpace: "pre-wrap",
          fontFamily: monospace ? "monospace" : undefined,
        }}>
        {segments.map((segment, index) =>
          segment.changed ? (
            <Box
              key={index}
              component="mark"
              sx={{ bgcolor: MARK_BACKGROUNDS[side], color: "inherit", borderRadius: theme => theme.radius.default }}>
              {segment.text}
            </Box>
          ) : (
            segment.text
          ),
        )}
      </Typography>
      {overflowing && (
        <Button
          size="small"
          variant="text"
          label={expanded ? "diffShowLess" : "diffShowMore"}
          onClick={() => setExpanded(previous => !previous)}
          data-cy="diff-show-more"
          sx={{ px: 0, minWidth: 0 }}
        />
      )}
    </Stack>
  );
};
