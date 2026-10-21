import { FC } from "react";
import { Box, Stack, Tooltip, useTheme } from "@mui/material";
import { MapLayerStyle } from "../../../api/apiInterfaces";
import { px2rem } from "../../../appTheme";

interface DiffStateSwatchesProps {
  /** The states the object is drawn in on the map, old before new. */
  states: readonly MapLayerStyle[];
  /** Names the states in words, as the tooltip of the squares. */
  title: string;
}

/** Squares in the map colours of the states an object is drawn in: orange for the old state, blue for the new one. */
export const DiffStateSwatches: FC<DiffStateSwatchesProps> = ({ states, title }) => {
  const theme = useTheme();
  return (
    <Tooltip title={title}>
      <Stack direction="row" sx={{ gap: 0.5 }}>
        {states.map(state => (
          <Box
            key={state}
            data-cy={`diff-state-${state}`}
            sx={{ width: px2rem(12), height: px2rem(12), backgroundColor: theme.palette.map[state].fill }}
          />
        ))}
      </Stack>
    </Tooltip>
  );
};
