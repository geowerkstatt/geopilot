import { FC } from "react";
import { Box, Stack, useTheme } from "@mui/material";
import { MapLayerStyle } from "../../../api/apiInterfaces";
import { px2rem } from "../../../appTheme";

interface DiffStateSwatchesProps {
  /** The states the object is drawn in on the map, old before new. */
  states: readonly MapLayerStyle[];
}

/** Squares in the map colours of the states an object is drawn in: orange for the old state, blue for the new one. */
export const DiffStateSwatches: FC<DiffStateSwatchesProps> = ({ states }) => {
  const theme = useTheme();
  return (
    <Stack direction="row" sx={{ gap: 0.5 }}>
      {states.map(state => (
        <Box
          key={state}
          data-cy={`diff-state-${state}`}
          sx={{ width: px2rem(12), height: px2rem(12), backgroundColor: theme.palette.map[state].fill }}
        />
      ))}
    </Stack>
  );
};
