import { FC, PropsWithChildren, useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Theme, useTheme } from "@mui/material/styles";
import { defaults as defaultControls } from "ol/control";
import { Condition, platformModifierKeyOnly } from "ol/events/condition";
import { Extent, getCenter } from "ol/extent";
import { MAC } from "ol/has";
import { defaults as defaultInteractions, DragPan, MouseWheelZoom } from "ol/interaction";
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
import { MapVisualizationContext, MapVisualizationContextInterface } from "./mapVisualizationContext";
import "ol/ol.css";

const ZOOM_TO_NODE_MAX_ZOOM = 13;
const ZOOM_TO_NODE_DURATION = 400;

const LOCALIZABLE_TITLE_PROPERTY = "localizableTitle";

const SWISS_PROJECTION = "EPSG:2056";
const SWISS_EXTENT: Extent = [2420000, 1030000, 2900000, 1350000];

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

const createInteractionHint = (
  theme: Theme,
): { element: HTMLDivElement; show: (text: string) => void; hide: () => void } => {
  const INTERACTION_HINT_DURATION_MS = 1500;

  const element = document.createElement("div");
  Object.assign(element.style, {
    position: "absolute",
    inset: "0",
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    textAlign: "center",
    padding: theme.spacing(2),
    fontSize: "1.25rem",
    color: theme.palette.map.hintText,
    backgroundColor: theme.palette.map.hintBackground,
    opacity: "0",
    transition: "opacity 0.2s ease",
    pointerEvents: "none",
    zIndex: "1",
  } satisfies Partial<CSSStyleDeclaration>);

  let hideTimeout: number | undefined;
  const hide = () => {
    if (hideTimeout) clearTimeout(hideTimeout);
    element.style.opacity = "0";
  };
  const show = (text: string) => {
    hide();
    element.textContent = text;
    element.style.opacity = "1";
    hideTimeout = setTimeout(() => {
      hide();
    }, INTERACTION_HINT_DURATION_MS);
  };
  return { element, show, hide };
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
  /** The map visualization config to render; when absent no map is built. */
  config?: MapVisualizationConfig;
  /** Feature ids currently visible (filter result); undefined means no filter (show all). */
  visibleFeatureIds?: ReadonlySet<string>;
  /** Feature ids to highlight (current selection); empty set means none. */
  highlightedFeatureIds: ReadonlySet<string>;
  /** Latest explicit zoom-to-node request, or null. Selection no longer moves the map; this does. */
  zoomRequest?: MapZoomRequest | null;
  /** Called with the feature id when a feature is clicked. */
  onSelectFeature?: (featureId: string) => void;
  /** Whether to show the metadata popup when a feature is selected. */
  showMapSelectionPopup?: boolean;
}

/**
 * Owns the OpenLayers map: it builds the map from the config (feature and WMTS base layers, selection
 * popup, event handlers), keeps it reactive to filter/selection/zoom inputs, and exposes it through
 * MapVisualizationContext.
 */
export const MapVisualizationProvider: FC<PropsWithChildren<MapVisualizationProviderProps>> = ({
  config,
  visibleFeatureIds,
  highlightedFeatureIds,
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
  const visibleIdsRef = useRef<ReadonlySet<string> | undefined>(visibleFeatureIds);
  const highlightedIdsRef = useRef<ReadonlySet<string>>(highlightedFeatureIds);
  const lastZoomTokenRef = useRef<number | undefined>(undefined);

  // Padding and max zoom used whenever the view is fit to features.
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

  useEffect(() => {
    if (!config) return;

    const isTouch: Condition = e => e.originalEvent instanceof PointerEvent && e.originalEvent.pointerType === "touch";
    const interactions = defaultInteractions({
      dragPan: false,
      mouseWheelZoom: false,
      pinchRotate: false,
    }).extend([
      new DragPan({
        condition: e => !isTouch(e) || e.activePointers?.length === 2,
      }),
      new MouseWheelZoom({ condition: platformModifierKeyOnly }),
    ]);

    const map = new Map({
      overlays: [selectionOverlay],
      controls: defaultControls({ zoom: false, attribution: false }),
      interactions,
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

    // Explain the restricted pan/zoom gestures to the user when they attempt the blocked variant
    const interactionHint = createInteractionHint(theme);
    map.getViewport().appendChild(interactionHint.element);

    const onWheel = (event: WheelEvent) => {
      const modifierKey = MAC ? event.metaKey : event.ctrlKey;
      if (!modifierKey) {
        interactionHint.show(t("mapInteractionHintScroll", { key: MAC ? "⌘" : "Ctrl" }));
      } else {
        interactionHint.hide();
      }
    };
    map.getViewport().addEventListener("wheel", onWheel, { passive: true });

    const dragHintKey = map.on("pointerdrag", event => {
      if (isTouch(event) && event.activePointers?.length === 1) {
        interactionHint.show(t("mapInteractionHintTouch"));
      } else {
        interactionHint.hide();
      }
    });

    let cancelled = false;

    const initializeLayersFromConfig = async () => {
      const layers: BaseLayer[] = config.layers
        .filter(layer => layer.features)
        .map(layer =>
          buildFeatureLayer(layer, theme.palette.map, layer.title, SWISS_PROJECTION, visibleIdsRef, highlightedIdsRef),
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
    initializeLayersFromConfig().catch(error => {
      console.error("Failed to render map visualization.", error);
    });

    return () => {
      cancelled = true;
      map.getViewport().removeEventListener("wheel", onWheel);
      unByKey(dragHintKey);
      interactionHint.hide();
      map.setTarget(undefined);
    };
  }, [config, selectionOverlay, t, theme]);

  useEffect(() => {
    if (!map) return;

    const handler = map.on("click", event => {
      const feature = map?.forEachFeatureAtPixel(event.pixel, f => f);
      const info = feature?.get("info") as string | undefined;
      const featureId = feature?.getId()?.toString();
      if (showMapSelectionPopup && feature && info) {
        setSelectionOverlayText(info);
        const geometry = feature.getGeometry();
        selectionOverlay.setPosition(geometry ? getCenter(geometry.getExtent()) : event.coordinate);
      } else {
        selectionOverlay.setPosition(undefined);
      }
      if (featureId) onSelectFeature?.(featureId);
    });

    return () => {
      unByKey(handler);
    };
  }, [map, onSelectFeature, selectionOverlay, setSelectionOverlayText, showMapSelectionPopup]);

  useEffect(() => {
    visibleIdsRef.current = visibleFeatureIds;
    highlightedIdsRef.current = highlightedFeatureIds;
    // Trigger a re-render of the style function
    featureLayers.forEach(layer => layer.changed());
  }, [visibleFeatureIds, highlightedFeatureIds, featureLayers]);

  useEffect(() => {
    if (!map || !zoomRequest || zoomRequest.token === lastZoomTokenRef.current) return;
    lastZoomTokenRef.current = zoomRequest.token;
    fitToFeatures(map, new Set(zoomRequest.featureIds), {
      padding: fitOptions.current.padding,
      maxZoom: ZOOM_TO_NODE_MAX_ZOOM,
      duration: ZOOM_TO_NODE_DURATION,
    });
  }, [map, zoomRequest]);

  const contextValue = useMemo<MapVisualizationContextInterface>(
    () => ({ map, zoomToExtent, zoomBy, setFitOptions }),
    [map, zoomToExtent, zoomBy, setFitOptions],
  );
  return <MapVisualizationContext.Provider value={contextValue}>{children}</MapVisualizationContext.Provider>;
};
