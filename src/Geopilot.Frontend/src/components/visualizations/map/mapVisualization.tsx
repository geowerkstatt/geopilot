import { Fragment, ReactNode, useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import AddIcon from "@mui/icons-material/Add";
import CloseIcon from "@mui/icons-material/Close";
import RemoveIcon from "@mui/icons-material/Remove";
import ZoomOutMapIcon from "@mui/icons-material/ZoomOutMap";
import { Box, ButtonGroup, Link, Stack, Typography } from "@mui/material";
import { alpha, useTheme } from "@mui/material/styles";
import MousePosition from "ol/control/MousePosition";
import ScaleLine from "ol/control/ScaleLine";
import { Coordinate } from "ol/coordinate";
import { MapVisualizationConfig } from "../../../api/apiInterfaces";
import { px2rem } from "../../../appTheme";
import { stopStepSwipePropagation } from "../../../hooks/useStepSwipe";
import { IconButton } from "../../buttons";
import { LayerSwitcher } from "./layerSwitcher";
import { MAP_FOOTER_HEIGHT, MapPanelContainerContext, useMapVisualization } from "./mapVisualizationContext";

// LV95 coordinates are written in Swiss notation in every language: 2'600'000.
const SWISS_METERS = new Intl.NumberFormat("de-CH", { maximumFractionDigits: 0 });

const formatCoordinate = (coordinate?: Coordinate): string =>
  coordinate ? `${SWISS_METERS.format(coordinate[0])}, ${SWISS_METERS.format(coordinate[1])}` : "";

interface MapVisualizationProps {
  /** The map visualization config to render. */
  config: MapVisualizationConfig;
  /** Whether the view should reserve space for filters in fullscreen mode. */
  reserveSpaceForFilters?: boolean;
  fullscreen?: boolean;
  setFullscreen: (fullscreen: boolean) => void;
  /** Further buttons in a group of their own, above the panel buttons. */
  controls?: ReactNode;
  /** Buttons that open a panel beside them, grouped above the layers button. */
  panelControls?: ReactNode;
  /** Panels laid over the map, positioned by themselves. */
  children?: ReactNode;
}

export const MapVisualization = ({
  config,
  reserveSpaceForFilters,
  fullscreen,
  setFullscreen,
  controls,
  panelControls,
  children,
}: MapVisualizationProps) => {
  const { t } = useTranslation();
  const theme = useTheme();
  const { map, zoomToExtent, zoomBy, setFitOptions } = useMapVisualization();
  const mapContainerRef = useRef<HTMLDivElement>(null);
  const [panelContainer, setPanelContainer] = useState<HTMLDivElement | null>(null);
  const [scaleLineContainer, setScaleLineContainer] = useState<HTMLDivElement | null>(null);
  const [mousePositionContainer, setMousePositionContainer] = useState<HTMLDivElement | null>(null);

  useEffect(() => {
    const container = mapContainerRef.current;
    if (!map || !container) return;
    map.setTarget(container);
    return () => map.setTarget(undefined);
  }, [map]);

  useEffect(() => {
    if (!map || !scaleLineContainer || !mousePositionContainer) return;
    const footerControls = [
      new ScaleLine({ target: scaleLineContainer }),
      // An empty placeholder clears the position when the pointer leaves the map; OpenLayers keeps the last one otherwise.
      new MousePosition({ target: mousePositionContainer, coordinateFormat: formatCoordinate, placeholder: "" }),
    ];
    footerControls.forEach(control => map.addControl(control));
    return () => {
      footerControls.forEach(control => map.removeControl(control));
    };
  }, [map, scaleLineContainer, mousePositionContainer]);

  const hasLeftPadding = fullscreen && reserveSpaceForFilters;
  useEffect(() => {
    setFitOptions({ padding: [40, 40, 40, hasLeftPadding ? 440 : 40], maxZoom: 12 });
  }, [hasLeftPadding, setFitOptions]);

  const attributions = config.layers.flatMap(layer =>
    layer.attribution ? [{ text: layer.attribution, url: layer.attributionUrl }] : [],
  );

  return (
    <Box
      ref={setPanelContainer}
      data-cy="map-visualization"
      {...stopStepSwipePropagation}
      sx={{
        position: "relative",
        width: "100%",
        height: fullscreen ? "100%" : "500px",
        border: theme => `1px solid ${fullscreen ? theme.palette.primary.main : theme.palette.primary.light}`,
        borderRadius: theme.radius.default,
        overflow: "hidden",
        backgroundColor: theme.palette.background.content,
      }}>
      <Box
        ref={mapContainerRef}
        sx={{
          position: "absolute",
          width: "100%",
          height: "100%",
        }}
      />
      <Stack
        direction="row"
        sx={{
          position: "absolute",
          bottom: 0,
          left: 0,
          right: 0,
          height: MAP_FOOTER_HEIGHT,
          gap: 1,
          alignItems: "baseline",
          px: 0.75,
          pt: 0.25,
          ...theme.typography.caption,
          lineHeight: 1.2,
          backgroundColor: alpha(theme.palette.background.content, 0.7),
        }}>
        <Box
          ref={setScaleLineContainer}
          sx={{
            flexShrink: 0,
            fontSize: px2rem(10),
            "& .ol-scale-line-inner": {
              textAlign: "center",
              border: `1.5px solid ${theme.palette.text.primary}`,
              borderTop: "none",
            },
          }}
        />
        {/* A touch screen has no pointer to follow. */}
        <Box
          ref={setMousePositionContainer}
          sx={{
            flexShrink: 0,
            fontSize: px2rem(10),
            fontVariantNumeric: "tabular-nums",
            "@media (hover: none)": { display: "none" },
          }}
        />
        {attributions.length > 0 && (
          <Typography variant="caption" noWrap sx={{ ml: "auto", fontSize: px2rem(10) }}>
            {t("mapCopyrightPrefix")}{" "}
            {attributions.map((attribution, index) => (
              <Fragment key={attribution.text}>
                {index > 0 && ", "}
                {attribution.url ? (
                  <Link
                    href={attribution.url}
                    target="_blank"
                    rel="noopener noreferrer"
                    variant="inherit"
                    color="inherit">
                    {attribution.text}
                  </Link>
                ) : (
                  attribution.text
                )}
              </Fragment>
            ))}
          </Typography>
        )}
      </Stack>
      {map && (
        <>
          <Stack direction="column" sx={{ position: "absolute", top: 0, right: 0, m: { xs: 1, sm: 2 } }}>
            {fullscreen && (
              <IconButton
                color="primaryOutlined"
                icon={<CloseIcon />}
                label="fullscreenExit"
                tooltipPlacement="left"
                onClick={() => setFullscreen(false)}
              />
            )}
            <ButtonGroup orientation="vertical">
              <IconButton
                color="primaryOutlined"
                icon={<AddIcon />}
                label="mapZoomIn"
                tooltipPlacement="left"
                onClick={() => zoomBy(1)}
              />
              <IconButton
                color={"primaryOutlined"}
                icon={<ZoomOutMapIcon />}
                label="zoomToExtent"
                tooltipPlacement="left"
                onClick={zoomToExtent}
              />
              <IconButton
                color="primaryOutlined"
                icon={<RemoveIcon />}
                label="mapZoomOut"
                tooltipPlacement="left"
                onClick={() => zoomBy(-1)}
              />
            </ButtonGroup>
          </Stack>
          <MapPanelContainerContext.Provider value={panelContainer}>
            <Stack sx={{ position: "absolute", bottom: MAP_FOOTER_HEIGHT, right: 0, m: { xs: 1, sm: 2 }, gap: 1 }}>
              {controls}
              <ButtonGroup orientation="vertical">
                {panelControls}
                <LayerSwitcher map={map} />
              </ButtonGroup>
            </Stack>
            {children}
          </MapPanelContainerContext.Provider>
        </>
      )}
    </Box>
  );
};
