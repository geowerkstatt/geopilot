import { FC, PropsWithChildren, useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Theme, useTheme } from "@mui/material/styles";
import { defaults as defaultControls } from "ol/control";
import { getCenter } from "ol/extent";
import BaseLayer from "ol/layer/Base";
import VectorLayer from "ol/layer/Vector";
import Map from "ol/Map";
import { unByKey } from "ol/Observable";
import Overlay from "ol/Overlay";
import { get as getProjection } from "ol/proj";
import { register } from "ol/proj/proj4";
import View, { FitOptions } from "ol/View";
import proj4 from "proj4";
import { LocalizedText, MapVisualizationConfig } from "../../../api/apiInterfaces";
import { useLocalized } from "../../../hooks/useLocalized";
import { buildFeatureLayer, buildWmtsLayer, fitToFeatures, fitToLayers } from "./layers";
import { LayerSwitcherProperties } from "./layerSwitcherProps";
import { MapVisualizationContext } from "./mapVisualizationContext";
import "ol/ol.css";

// The map visualization config uses Swiss LV95 (EPSG:2056) coordinates, matching the swisstopo base
// map and the coordinate reference system of INTERLIS error coordinates. OpenLayers only ships EPSG:4326
// and EPSG:3857 out of the box, so EPSG:2056 has to be registered with proj4 once.
const SWISS_PROJECTION = "EPSG:2056";
// Bounding extent of Switzerland in LV95, used as the WMTS tile grid extent and default map view extent.
const SWISS_EXTENT: [number, number, number, number] = [2420000, 1030000, 2900000, 1350000];

const ZOOM_TO_NODE_MAX_ZOOM = 13;
const ZOOM_TO_NODE_DURATION = 400;

const LOCALIZABLE_TITLE_PROPERTY = "localizableTitle";

proj4.defs(
  SWISS_PROJECTION,
  "+proj=somerc +lat_0=46.95240555555556 +lon_0=7.439583333333333 +k_0=1 " +
    "+x_0=2600000 +y_0=1200000 +ellps=bessel " +
    "+towgs84=674.374,15.056,405.346,0,0,0,0 +units=m +no_defs",
);
register(proj4);
getProjection(SWISS_PROJECTION)?.setExtent(SWISS_EXTENT);

const createPopupArrow = (size: number, color: string, top: string): HTMLDivElement => {
  const arrow = document.createElement("div");
  Object.assign(arrow.style, {
    position: "absolute",
    top,
    left: "50%",
    transform: "translateX(-50%)",
    width: "0",
    height: "0",
    borderLeft: `${size}px solid transparent`,
    borderRight: `${size}px solid transparent`,
    borderTop: `${size}px solid ${color}`,
  });
  return arrow;
};

const createSelectionOverlay = (theme: Theme): [Overlay, (text: string) => void] => {
  // The popup element is created imperatively rather than rendered by React, because OpenLayers'
  // Overlay moves the element out of its original parent into the map's overlay container. A
  // React-rendered node handed to OL would no longer be where React expects it, and the next sibling
  // insert/remove would throw "insertBefore … not a child of this node".
  const popupElement = document.createElement("div");
  Object.assign(popupElement.style, {
    position: "relative",
    backgroundColor: theme.palette.background.paper,
    border: `1px solid ${theme.palette.primary.light}`,
    borderRadius: theme.radius.default,
    padding: `${theme.spacing(1)} ${theme.spacing(1.5)}`,
    maxWidth: "300px",
    fontSize: "0.875rem",
    pointerEvents: "none",
  } satisfies Partial<CSSStyleDeclaration>);
  const popupContent = document.createElement("div");
  popupElement.appendChild(popupContent);
  popupElement.appendChild(createPopupArrow(8, theme.palette.primary.light, "100%"));
  popupElement.appendChild(createPopupArrow(7, theme.palette.background.paper, "calc(100% - 1px)"));
  const overlay = new Overlay({
    element: popupElement,
    positioning: "bottom-center",
    offset: [0, -12],
    stopEvent: false,
  });
  return [
    overlay,
    (text: string) => {
      popupContent.textContent = text;
    },
  ];
};

/**
 * A request to zoom the map to a set of features (those of a tree node's subtree). The token makes each
 * request distinct, so zooming to the same node again re-triggers the zoom.
 */
export interface MapZoomRequest {
  featureIds: string[];
  token: number;
}

interface MapVisualizationProviderProps {
  /** The map visualization config to render; when absent no map is built (e.g. a tree-only visualization). */
  config?: MapVisualizationConfig;
  /** Error ids currently visible (filter result); undefined means no filter (show all). */
  visibleErrorIds?: ReadonlySet<string>;
  /** Error ids to highlight (current selection); empty set means none. */
  highlightedErrorIds: ReadonlySet<string>;
  /** Latest explicit zoom-to-node request, or null. Selection no longer moves the map; this does. */
  zoomRequest?: MapZoomRequest | null;
  /** Called with the error id when a feature is clicked. */
  onSelectFeature?: (errorId: string) => void;
  /** Whether to show the metadata popup when a feature is selected. */
  showMapSelectionPopup?: boolean;
}

/**
 * Owns the OpenLayers map for the XTF error visualization: it builds the map from the config (feature and
 * WMTS base layers, selection popup, event handlers), keeps it reactive to filter/selection/zoom inputs,
 * and exposes it through MapVisualizationContext. Because the provider outlives the view component, the map
 * survives the view unmounting and remounting (e.g. toggling fullscreen), so the user's pan/zoom and layer
 * state persist without any external state capture.
 */
export const MapVisualizationProvider: FC<PropsWithChildren<MapVisualizationProviderProps>> = ({
  config,
  visibleErrorIds,
  highlightedErrorIds,
  zoomRequest,
  onSelectFeature,
  showMapSelectionPopup = false,
  children,
}) => {
  const { t } = useTranslation();
  const localized = useLocalized();
  const theme = useTheme();

  const [map, setMap] = useState<Map | null>(null);
  const [featureLayers, setFeatureLayers] = useState<BaseLayer[]>([]);

  // The feature style function and the map's event handlers read the current filter/selection/callbacks
  // from refs, so changing them only restyles the existing layers (cheap) instead of rebuilding the map
  // (which would re-fetch the WMTS base map).
  const visibleIdsRef = useRef<ReadonlySet<string> | undefined>(visibleErrorIds);
  const highlightedIdsRef = useRef<ReadonlySet<string>>(highlightedErrorIds);
  const lastZoomTokenRef = useRef<number | undefined>(undefined);

  // Padding and max zoom used whenever the view is fit to features, on the initial fit and via the
  // reset-viewport button and zoom requests, so they stay in sync.
  // Used as a ref so it does not trigger a re-render.
  const fitOptions = useRef<FitOptions>({ padding: [40, 40, 40, 40], maxZoom: 12 });
  const setFitOptions = useCallback((options: FitOptions) => {
    fitOptions.current = options;
  }, []);

  const [selectionOverlay, setSelectionOverlayText] = useMemo(() => createSelectionOverlay(theme), [theme]);

  const zoomToExtent = useCallback(() => {
    if (!map) return;
    fitToLayers(map, fitOptions.current, SWISS_EXTENT);
  }, [map]);

  const zoomBy = useCallback(
    (delta: number) => {
      const view = map?.getView();
      const zoom = view?.getZoom();
      if (view && zoom !== undefined) {
        view.animate({ zoom: zoom + delta, duration: 200 });
      }
    },
    [map],
  );

  // translate layer titles
  useEffect(() => {
    for (const layer of featureLayers) {
      const localizableTitle = layer.get(LOCALIZABLE_TITLE_PROPERTY) as LocalizedText | undefined;
      let title = localized(localizableTitle);
      if (!title && layer instanceof VectorLayer) {
        title = t("mapVisualizationFeatureLayer");
      }
      layer.set(LayerSwitcherProperties.TITLE, title);
      layer.changed();
    }
  }, [featureLayers, localized, map, t]);

  // initialize map
  useEffect(() => {
    if (!config) return;

    const map = new Map({
      overlays: [selectionOverlay],
      controls: defaultControls({ zoom: false, attribution: false }),
      view: new View({ projection: SWISS_PROJECTION, extent: SWISS_EXTENT }),
    });

    // Defer until the map is mounted in the DOM
    map.once("change:size", () => {
      fitToLayers(map, fitOptions.current, SWISS_EXTENT);
    });

    map.on("pointermove", event => {
      const hit = map?.hasFeatureAtPixel(event.pixel) ?? false;
      map!.getTargetElement().style.cursor = hit ? "pointer" : "";
    });

    let cancelled = false;

    const initializeLayersFromConfig = async () => {
      const layers: BaseLayer[] = config.layers
        .filter(layer => layer.features)
        .map(layer =>
          buildFeatureLayer(
            layer,
            theme.palette.map.fill,
            layer.title,
            theme.palette.map.stroke,
            SWISS_PROJECTION,
            visibleIdsRef,
            highlightedIdsRef,
          ),
        );

      const wmtsLayerConfig = config.layers.find(layer => layer.wmts);
      if (wmtsLayerConfig?.wmts) {
        const wmtsLayer = await buildWmtsLayer(
          wmtsLayerConfig.wmts,
          SWISS_PROJECTION,
          wmtsLayerConfig.layerIds,
          wmtsLayerConfig.title,
        );
        // Base map is drawn below the features.
        if (wmtsLayer) layers.unshift(wmtsLayer);
      }
      if (cancelled) return;
      map.setLayers(layers);
      setFeatureLayers(layers);
      setMap(map);
      fitToLayers(map, fitOptions.current, SWISS_EXTENT);
    };
    initializeLayersFromConfig();

    return () => {
      cancelled = true;
      map.setTarget(undefined);
    };
  }, [config, selectionOverlay, theme]);

  // map event handlers with dependencies to component props
  useEffect(() => {
    if (!map) return;

    const handler = map.on("click", event => {
      const feature = map?.forEachFeatureAtPixel(event.pixel, f => f);
      const info = feature?.get("info") as string | undefined;
      const errorId = feature?.get("errorId") as string | undefined;
      if (showMapSelectionPopup && feature && info) {
        setSelectionOverlayText(info);
        const geometry = feature.getGeometry();
        selectionOverlay.setPosition(geometry ? getCenter(geometry.getExtent()) : event.coordinate);
      } else {
        selectionOverlay.setPosition(undefined);
      }
      if (errorId) onSelectFeature?.(errorId);
    });

    return () => {
      unByKey(handler);
    };
  }, [map, onSelectFeature, selectionOverlay, setSelectionOverlayText, showMapSelectionPopup]);

  // Apply filter (hide non-matching) and selection (highlight) without rebuilding the map and without moving
  // the view: update the refs the style function reads and restyle the existing layers. Moving the map is
  // the job of the explicit zoom request below, not of selection.
  useEffect(() => {
    visibleIdsRef.current = visibleErrorIds;
    highlightedIdsRef.current = highlightedErrorIds;
    if (!map) return;
    featureLayers.forEach(layer => layer.changed());
  }, [map, visibleErrorIds, highlightedErrorIds, featureLayers]);

  // Zoom to the features of an explicit zoom request (a tree node's features: one for a leaf, many for a
  // group). Guarded by the request token so a re-render does not re-run the last zoom.
  useEffect(() => {
    if (!map || !zoomRequest || zoomRequest.token === lastZoomTokenRef.current) return;
    lastZoomTokenRef.current = zoomRequest.token;
    fitToFeatures(map, new Set(zoomRequest.featureIds), {
      padding: fitOptions.current.padding,
      maxZoom: ZOOM_TO_NODE_MAX_ZOOM,
      duration: ZOOM_TO_NODE_DURATION,
    });
  }, [map, zoomRequest]);

  return (
    <MapVisualizationContext.Provider value={{ map, zoomToExtent, zoomBy, setFitOptions }}>
      {children}
    </MapVisualizationContext.Provider>
  );
};
