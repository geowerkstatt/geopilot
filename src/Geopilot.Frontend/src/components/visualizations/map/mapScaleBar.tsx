import { FC, useEffect, useState } from "react";
import { Box } from "@mui/material";
import { useTheme } from "@mui/material/styles";
import OlMap from "ol/Map";
import MapEvent from "ol/MapEvent";
import { getPointResolution } from "ol/proj";
import { px2rem } from "../../../appTheme";
import { getScaleBar, ScaleBar } from "./scaleBar";

interface MapScaleBarProps {
  map: OlMap;
}

/**
 * The scale bar under the map. React draws it because OpenLayers' ScaleLine writes its text as innerHTML, which
 * the Trusted Types of our Content-Security-Policy refuse.
 */
export const MapScaleBar: FC<MapScaleBarProps> = ({ map }) => {
  const theme = useTheme();
  const [scaleBar, setScaleBar] = useState<ScaleBar>();

  useEffect(() => {
    const update = (event: MapEvent) => {
      const viewState = event.frameState?.viewState;
      const next = viewState
        ? getScaleBar(getPointResolution(viewState.projection, viewState.resolution, viewState.center, "m"))
        : undefined;
      // The map renders every frame of an animation, the bar changes far less often.
      setScaleBar(previous => (previous?.label === next?.label && previous?.width === next?.width ? previous : next));
    };
    map.on("postrender", update);
    return () => map.un("postrender", update);
  }, [map]);

  if (!scaleBar) return null;

  return (
    <Box
      data-cy="map-scale-bar"
      sx={{
        flexShrink: 0,
        width: `${scaleBar.width}px`,
        fontSize: px2rem(10),
        textAlign: "center",
        border: `1.5px solid ${theme.palette.text.primary}`,
        borderTop: "none",
      }}>
      {scaleBar.label}
    </Box>
  );
};
