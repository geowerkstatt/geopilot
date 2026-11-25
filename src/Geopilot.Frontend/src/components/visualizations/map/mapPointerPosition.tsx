import { FC, useEffect, useState } from "react";
import { Box } from "@mui/material";
import { Coordinate } from "ol/coordinate";
import OlMap from "ol/Map";
import { px2rem } from "../../../appTheme";

// LV95 coordinates are written in Swiss notation in every language: 2'600'000.
const SWISS_METERS = new Intl.NumberFormat("de-CH", { maximumFractionDigits: 0 });

const formatCoordinate = (coordinate?: Coordinate): string =>
  coordinate ? `${SWISS_METERS.format(coordinate[0])}, ${SWISS_METERS.format(coordinate[1])}` : "";

interface MapPointerPositionProps {
  map: OlMap;
}

/**
 * The coordinates under the pointer while it is over the map. React draws them because OpenLayers' MousePosition
 * writes its text as innerHTML, which the Trusted Types of our Content-Security-Policy refuse.
 */
export const MapPointerPosition: FC<MapPointerPositionProps> = ({ map }) => {
  const [coordinate, setCoordinate] = useState<Coordinate>();

  useEffect(() => {
    const viewport = map.getViewport();
    const follow = (event: PointerEvent) => setCoordinate(map.getEventCoordinate(event));
    const clear = () => setCoordinate(undefined);
    viewport.addEventListener("pointermove", follow);
    viewport.addEventListener("pointerout", clear);
    return () => {
      viewport.removeEventListener("pointermove", follow);
      viewport.removeEventListener("pointerout", clear);
    };
  }, [map]);

  return (
    <Box
      data-cy="map-pointer-position"
      sx={{
        flexShrink: 0,
        fontSize: px2rem(10),
        fontVariantNumeric: "tabular-nums",
        // A touch screen has no pointer to follow.
        "@media (hover: none)": { display: "none" },
      }}>
      {formatCoordinate(coordinate)}
    </Box>
  );
};
