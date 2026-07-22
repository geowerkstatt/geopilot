import { Fragment, useEffect, useRef } from "react";
import { useTranslation } from "react-i18next";
import AddIcon from "@mui/icons-material/Add";
import CloseIcon from "@mui/icons-material/Close";
import RemoveIcon from "@mui/icons-material/Remove";
import ZoomOutMapIcon from "@mui/icons-material/ZoomOutMap";
import { Box, ButtonGroup, Link, Stack, Typography } from "@mui/material";
import { alpha, useTheme } from "@mui/material/styles";
import { MapVisualizationConfig } from "../../../api/apiInterfaces";
import { stopStepSwipePropagation } from "../../../hooks/useStepSwipe";
import { IconButton } from "../../buttons";
import { LayerSwitcher } from "./layerSwitcher";
import { useMapVisualization } from "./mapVisualizationContext";

interface MapVisualizationProps {
  /** The map visualization config to render. */
  config: MapVisualizationConfig;
  /** Whether the view should reserve space for filters in fullscreen mode. */
  reserveSpaceForFilters?: boolean;
  fullscreen?: boolean;
  setFullscreen: (fullscreen: boolean) => void;
}

export const MapVisualization = ({
  config,
  reserveSpaceForFilters,
  fullscreen,
  setFullscreen,
}: MapVisualizationProps) => {
  const { t } = useTranslation();
  const theme = useTheme();
  const { map, zoomToExtent, zoomBy, setFitOptions } = useMapVisualization();
  const mapContainerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const container = mapContainerRef.current;
    if (!map || !container) return;
    map.setTarget(container);
    return () => map.setTarget(undefined);
  }, [map]);

  const hasLeftPadding = fullscreen && reserveSpaceForFilters;
  useEffect(() => {
    setFitOptions({ padding: [40, 40, 40, hasLeftPadding ? 440 : 40], maxZoom: 12 });
  }, [hasLeftPadding, setFitOptions]);

  const attributions = config.layers.flatMap(layer =>
    layer.attribution ? [{ text: layer.attribution, url: layer.attributionUrl }] : [],
  );

  return (
    <Box
      {...stopStepSwipePropagation}
      sx={{
        position: "relative",
        width: "100%",
        height: fullscreen ? "100%" : "500px",
        border: theme => `1px solid ${fullscreen ? theme.palette.primary.main : theme.palette.primary.light}`,
        borderRadius: theme.radius.default,
        overflow: "hidden",
        backgroundColor: theme.palette.background.default,
      }}>
      <Box
        ref={mapContainerRef}
        sx={{
          position: "absolute",
          width: "100%",
          height: "100%",
        }}
      />
      {map && (
        <>
          <Stack direction="column" sx={{ position: "absolute", top: 0, right: 0, m: 2 }}>
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
          <LayerSwitcher map={map} />
        </>
      )}
      {attributions.length > 0 && (
        <Typography
          variant="caption"
          sx={{
            position: "absolute",
            bottom: 0,
            left: 0,
            px: 0.75,
            py: 0.25,
            maxWidth: "100%",
            backgroundColor: alpha(theme.palette.background.paper, 0.7),
            borderTopRightRadius: theme.radius.default,
            lineHeight: 1.2,
          }}>
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
    </Box>
  );
};
