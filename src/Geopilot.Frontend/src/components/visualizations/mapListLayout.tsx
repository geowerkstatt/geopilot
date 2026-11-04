import { FC, ReactNode, RefObject, useContext } from "react";
import OpenInFullIcon from "@mui/icons-material/OpenInFull";
import { Box, Modal, Stack, useTheme } from "@mui/material";
import { SxProps, Theme } from "@mui/material/styles";
import { px2rem } from "../../appTheme";
import { stopStepSwipePropagation } from "../../hooks/useStepSwipe";
import { IconButton } from "../buttons";
import { ScrollMarginContext } from "../scrollMargin/ScrollMarginContext";
import { ScrollMarginProvider } from "../scrollMargin/ScrollMarginProvider";
import { GeopilotBox } from "../styledComponents";

interface MapListLayoutProps {
  map?: ReactNode;
  filter?: ReactNode;
  list?: ReactNode;
  fullscreen: boolean;
  setFullscreen: (fullscreen: boolean) => void;
  /** Receives the inline map, so the caller can scroll to it. */
  inlineMapRef: RefObject<HTMLDivElement | null>;
  /** The panel that holds the filter and the list beside the map in fullscreen. */
  panelSx?: SxProps<Theme>;
}

/**
 * The filter, the map and the list one below the other, or, in fullscreen, the map with the filter and the list in a
 * panel on it.
 */
export const MapListLayout: FC<MapListLayoutProps> = ({
  map,
  filter,
  list,
  fullscreen,
  setFullscreen,
  inlineMapRef,
  panelSx,
}) => {
  const theme = useTheme();
  const { scrollMarginTop, scrollMarginBottom } = useContext(ScrollMarginContext);

  return (
    <>
      {map && (
        <IconButton
          size="small"
          label="fullscreen"
          icon={<OpenInFullIcon />}
          sx={{
            position: "absolute",
            top: 0,
            right: theme.spacing(6),
            padding: theme.spacing(2),
            display: { xs: "none", md: "block" },
          }}
          onClick={() => setFullscreen(!fullscreen)}
        />
      )}
      {map && fullscreen ? (
        <ScrollMarginProvider scrollMarginTop="0px" scrollMarginBottom="0px">
          <Modal open onClose={() => setFullscreen(false)} sx={{ padding: 4 }}>
            <Box {...stopStepSwipePropagation} sx={{ width: "100%", height: "100%", position: "relative" }}>
              {map}
              {list && (
                <GeopilotBox
                  sx={[
                    {
                      position: "absolute",
                      top: 0,
                      left: 0,
                      m: 2,
                      width: px2rem(440),
                      maxHeight: `calc(100% - ${theme.spacing(4)})`,
                    },
                    ...(Array.isArray(panelSx) ? panelSx : [panelSx]),
                  ]}>
                  {filter}
                  {list}
                </GeopilotBox>
              )}
            </Box>
          </Modal>
        </ScrollMarginProvider>
      ) : (
        <Stack sx={{ width: "100%" }}>
          {filter}
          <Box ref={inlineMapRef} sx={{ scrollMarginTop, scrollMarginBottom }}>
            {map}
          </Box>
          {list}
        </Stack>
      )}
    </>
  );
};
