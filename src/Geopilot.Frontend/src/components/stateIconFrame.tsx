import { FC, ReactNode } from "react";
import { Stack } from "@mui/material";
import { px2rem } from "../appTheme.ts";

interface StateIconFrameProps {
  /** The name of the state the icon shows, read out by a screen reader. Without it the frame is empty space. */
  label?: string;
  dataCy?: string;
  children?: ReactNode;
}

/**
 * The square the icon of a state stands in, the same for every step and event, so icons and titles line up. A span,
 * because it stands inside the button of a step or an accordion header.
 */
export const StateIconFrame: FC<StateIconFrameProps> = ({ label, dataCy, children }) => (
  <Stack
    component="span"
    role={label ? "img" : undefined}
    aria-label={label}
    data-cy={dataCy}
    sx={{
      width: px2rem(28),
      height: px2rem(28),
      flexShrink: 0,
      position: "relative",
      alignItems: "center",
      justifyContent: "center",
    }}>
    {children}
  </Stack>
);
