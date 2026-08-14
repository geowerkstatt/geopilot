import { FC, PropsWithChildren, useCallback, useEffect, useMemo, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { useTranslation } from "react-i18next";
import { Theme, useTheme } from "@mui/material/styles";
import { Coordinate } from "ol/coordinate";
import { Condition, platformModifierKeyOnly } from "ol/events/condition";
import { Extent, getCenter, getHeight, getWidth } from "ol/extent";
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
import { px2rem } from "../../../appTheme.ts";
import { useLocalized } from "../../../hooks/useLocalized";
import {
  buildFeatureLayer,
  buildWmtsLayer,
  fitToFeatures,
  fitToLayers,
  getClusterMembers,
  getFeaturesExtent,
  refreshFeatureLayer,
} from "./layers";
import { LayerSwitcherProperties } from "./layerSwitcherProps";
import { MapSelectionPopup, SelectionEntry } from "./mapSelectionPopup";
import { MapVisualizationContext, MapVisualizationContextInterface } from "./mapVisualizationContext";

const ZOOM_TO_NODE_MAX_ZOOM = 13;
const ZOOM_TO_CLUSTER_MAX_ZOOM = 18;
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

/**
 * Creates the overlay that anchors the selection popup to a marker. It only owns the positioning and the
 * host element, its content is rendered into that element with React (see MapSelectionPopup).
 */
const createSelectionOverlay = (): [Overlay, HTMLDivElement] => {
  const element = document.createElement("div");
  // The popup itself decides whether it takes clicks, so the host stays out of the way.
  element.style.pointerEvents = "none";
  // Scrolling a long list must not reach the viewport, which would answer with the zoom gesture hint.
  element.addEventListener("wheel", event => event.stopPropagation());
  const overlay = new Overlay({
    element,
    positioning: "bottom-center",
    offset: [0, -12],
    // The rows of a list are clickable, so the map must not act on events coming from the popup: it derives
    // its own click from pointerup and would close the popup before the row is ever selected.
    stopEvent: true,
  });
  return [overlay, element];
};

/** What the selection popup currently shows, and the coordinate it is anchored to. */
interface MapSelection {
  entries: SelectionEntry[];
  position: Coordinate;
}

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
    fontSize: px2rem(20),
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
  /** Whether the map is shown fullscreen, where it may claim single-touch drag and plain scroll-zoom. */
  fullscreen?: boolean;
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
  fullscreen = false,
  children,
}) => {
  const { t } = useTranslation();
  const { localized } = useLocalized();
  const theme = useTheme();

  const [map, setMap] = useState<Map | null>(null);
  const [featureLayers, setFeatureLayers] = useState<BaseLayer[]>([]);

  // The feature style function and the map's event handlers read the current filter/selection/callbacks
  // from refs, so changing them only restyles the existing layers (cheap) instead of rebuilding the map
  // (which would re-fetch the WMTS base map).
  const visibleIdsRef = useRef<ReadonlySet<string> | undefined>(visibleFeatureIds);
  const highlightedIdsRef = useRef<ReadonlySet<string>>(highlightedFeatureIds);
  const lastZoomTokenRef = useRef<number | undefined>(undefined);
  const fullscreenRef = useRef(fullscreen);

  // Padding and max zoom used whenever the view is fit to features.
  const fitOptions = useRef<FitOptions>({ padding: [40, 40, 40, 40], maxZoom: 12 });
  const setFitOptions = useCallback((options: FitOptions) => {
    fitOptions.current = options;
  }, []);

  const [selection, setSelection] = useState<MapSelection | null>(null);
  const [selectionOverlay, selectionOverlayElement] = useMemo(() => createSelectionOverlay(), []);

  // A changed filter re-clusters the markers, so the popup drops the errors the filter removed and closes
  // once none is left. Derived rather than reset, so picking a row (which changes the selection, not the
  // filter) leaves the popup open. Entries without an id are kept, as the feature style function does.
  const shownSelection = useMemo<MapSelection | null>(() => {
    if (!selection) return null;
    const entries = selection.entries.filter(
      entry => !visibleFeatureIds || !entry.id || visibleFeatureIds.has(entry.id),
    );
    return entries.length > 0 ? { ...selection, entries } : null;
  }, [selection, visibleFeatureIds]);

  useEffect(() => {
    selectionOverlay.setPosition(shownSelection?.position);
  }, [shownSelection, selectionOverlay]);

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
    // Inline the map shares the page with the surrounding scroll, so panning takes two fingers and zooming
    // the platform modifier key. Fullscreen there is nothing behind the map, so both gestures are handed to
    // the map unrestricted (issue #848).
    const interactions = defaultInteractions({
      dragPan: false,
      mouseWheelZoom: false,
      pinchRotate: false,
    }).extend([
      new DragPan({
        condition: e => fullscreenRef.current || !isTouch(e) || e.activePointers?.length === 2,
      }),
      new MouseWheelZoom({ condition: e => fullscreenRef.current || platformModifierKeyOnly(e) }),
    ]);

    const map = new Map({
      overlays: [selectionOverlay],
      controls: [],
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
      if (fullscreenRef.current) return;
      const modifierKey = MAC ? event.metaKey : event.ctrlKey;
      if (!modifierKey) {
        interactionHint.show(t("mapInteractionHintScroll", { key: MAC ? "⌘" : "Ctrl" }));
      } else {
        interactionHint.hide();
      }
    };
    map.getViewport().addEventListener("wheel", onWheel, { passive: true });

    const dragHintKey = map.on("pointerdrag", event => {
      if (fullscreenRef.current) return;
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

  // The interaction conditions and the gesture hints read the fullscreen state from a ref, so toggling
  // fullscreen does not rebuild the map (which would re-fetch the base map and reset the view). The viewport
  // additionally has to give up the touch-action ol.css reserves for page scrolling, otherwise the browser
  // keeps the single-finger gesture and the map never sees it.
  useEffect(() => {
    fullscreenRef.current = fullscreen;
    if (map) map.getViewport().style.touchAction = fullscreen ? "none" : "";
  }, [fullscreen, map]);

  useEffect(() => {
    if (!map) return;

    const handler = map.on("click", event => {
      const clicked = map?.forEachFeatureAtPixel(event.pixel, f => f);
      const members = clicked ? getClusterMembers(clicked) : [];

      // If the clicked feature is a cluster, zoom into it
      if (members.length > 1) {
        const membersExtent = getFeaturesExtent(members);
        const zoom = map.getView().getZoom() ?? 0;
        const separable = getWidth(membersExtent) > 0 || getHeight(membersExtent) > 0;
        if (separable && zoom < ZOOM_TO_CLUSTER_MAX_ZOOM) {
          setSelection(null);
          fitToFeatures(map, new Set(members.map(member => member.getId()?.toString() ?? "")), {
            padding: fitOptions.current.padding,
            maxZoom: ZOOM_TO_CLUSTER_MAX_ZOOM,
            duration: ZOOM_TO_NODE_DURATION,
          });
          return;
        }

        setSelection({
          entries: members.map(member => ({
            id: member.getId()?.toString() ?? "",
            text: (member.get("info") as string | undefined) ?? "",
          })),
          position: getCenter(membersExtent),
        });
        return;
      }

      const feature = members[0];
      const info = feature?.get("info") as string | undefined;
      const featureId = feature?.getId()?.toString();
      if (showMapSelectionPopup && feature && info) {
        const geometry = feature.getGeometry();
        setSelection({
          entries: [{ id: featureId ?? "", text: info }],
          position: geometry ? getCenter(geometry.getExtent()) : event.coordinate,
        });
      } else {
        setSelection(null);
      }
      if (featureId) onSelectFeature?.(featureId);
    });

    return () => {
      unByKey(handler);
    };
  }, [map, onSelectFeature, showMapSelectionPopup]);

  useEffect(() => {
    visibleIdsRef.current = visibleFeatureIds;
    highlightedIdsRef.current = highlightedFeatureIds;
    featureLayers.forEach(refreshFeatureLayer);
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
  return (
    <MapVisualizationContext.Provider value={contextValue}>
      {children}
      {shownSelection &&
        createPortal(
          <MapSelectionPopup
            entries={shownSelection.entries}
            onSelect={featureId => {
              if (featureId) onSelectFeature?.(featureId);
            }}
          />,
          selectionOverlayElement,
        )}
    </MapVisualizationContext.Provider>
  );
};
