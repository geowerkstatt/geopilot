import { MutableRefObject, useCallback, useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import CenterFocusStrongIcon from "@mui/icons-material/CenterFocusStrong";
import { Box, IconButton, Tooltip } from "@mui/material";
import { useTheme } from "@mui/material/styles";
import i18next from "i18next";
import { containsExtent, createEmpty, extend as extendExtent, getCenter, isEmpty as isExtentEmpty } from "ol/extent";
import Feature from "ol/Feature";
import WKT from "ol/format/WKT";
import WMTSCapabilities from "ol/format/WMTSCapabilities";
import BaseLayer from "ol/layer/Base";
import LayerGroup from "ol/layer/Group";
import TileLayer from "ol/layer/Tile";
import VectorLayer from "ol/layer/Vector";
import Map from "ol/Map";
import Overlay from "ol/Overlay";
import { get as getProjection } from "ol/proj";
import { register } from "ol/proj/proj4";
import VectorSource from "ol/source/Vector";
import WMTS, { optionsFromCapabilities } from "ol/source/WMTS";
import { Circle, Fill, Stroke, Style } from "ol/style";
import View from "ol/View";
import proj4 from "proj4";
import { MapLayer, MapVisualizationConfig } from "../../../../api/apiInterfaces";
import { LayerSwitcher, LayerSwitcherProperties } from "./layerSwitcher";
import "ol/ol.css";

const localized = (entries?: Record<string, string>) =>
  entries?.[i18next.resolvedLanguage ?? "en"] ?? entries?.["en"] ?? "";

// The map visualization config uses Swiss LV95 (EPSG:2056) coordinates, matching the swisstopo base
// map and the coordinate reference system of INTERLIS error coordinates. OpenLayers only ships EPSG:4326
// and EPSG:3857 out of the box, so EPSG:2056 has to be registered with proj4 once.
const SWISS_PROJECTION = "EPSG:2056";
// Bounding extent of Switzerland in LV95, used as the WMTS tile grid extent and default map view extent.
const SWISS_EXTENT: [number, number, number, number] = [2420000, 1030000, 2900000, 1350000];

let projectionRegistered = false;
const registerSwissProjection = () => {
  if (projectionRegistered) return;
  proj4.defs(
    SWISS_PROJECTION,
    "+proj=somerc +lat_0=46.95240555555556 +lon_0=7.439583333333333 +k_0=1 " +
      "+x_0=2600000 +y_0=1200000 +ellps=bessel " +
      "+towgs84=674.374,15.056,405.346,0,0,0,0 +units=m +no_defs",
  );
  register(proj4);
  getProjection(SWISS_PROJECTION)?.setExtent(SWISS_EXTENT);
  projectionRegistered = true;
};

const wktFormat = new WKT();

// Loads the capabilities as an XML Document via XMLHttpRequest. We intentionally do not fetch the text
// and hand it to WMTSCapabilities.read(string): that path uses DOMParser.parseFromString internally,
// which is a Trusted Types sink and is blocked by the app's "require-trusted-types-for 'script'" CSP.
// XMLHttpRequest's responseType "document" lets the browser parse the XML response (not a Trusted Types
// sink), and WMTSCapabilities.read() accepts a Document directly.
const fetchCapabilitiesDocument = (url: string): Promise<Document> =>
  new Promise((resolve, reject) => {
    const request = new XMLHttpRequest();
    request.open("GET", url);
    request.responseType = "document";
    request.overrideMimeType("text/xml");
    request.onload = () => {
      if (request.status >= 200 && request.status < 300 && request.responseXML) {
        resolve(request.responseXML);
      } else {
        reject(new Error(`Failed to load WMTS capabilities (HTTP ${request.status}).`));
      }
    };
    request.onerror = () => reject(new Error("Network error while loading WMTS capabilities."));
    request.send();
  });

// Builds the base map from a WMTS service. The layers to show are taken from layerIds (in the given
// order); when layerIds is omitted/empty, all layers the service advertises are used. A single resulting
// layer is returned directly and titled with the config's localized title (falling back to the layer
// identifier); multiple layers are wrapped in a group layer titled with the config's localized title
// (falling back to the service's advertised title, then the capabilities host) so the layer switcher can
// label it. Returns null when the capabilities cannot be fetched/parsed (e.g. the map service is
// unreachable) so the map still renders the feature layer.
const buildWmtsLayer = async (
  capabilitiesUrl: string,
  layerIds?: string[],
  title?: string,
): Promise<BaseLayer | null> => {
  try {
    const capabilitiesDocument = await fetchCapabilitiesDocument(capabilitiesUrl);
    const capabilities = new WMTSCapabilities().read(capabilitiesDocument);

    const availableLayers: { Identifier?: string }[] = capabilities?.Contents?.Layer ?? [];
    const availableIds = availableLayers.map(layer => layer.Identifier).filter((id): id is string => id !== undefined);

    let targetIds: string[];
    if (layerIds && layerIds.length > 0) {
      targetIds = layerIds.filter(id => availableIds.includes(id));
      const missing = layerIds.filter(id => !availableIds.includes(id));
      if (missing.length > 0) {
        console.warn(`WMTS service '${capabilitiesUrl}' does not advertise layer(s): ${missing.join(", ")}.`);
      }
    } else {
      // No explicit selection: show every layer the service advertises.
      targetIds = availableIds;
    }
    if (targetIds.length === 0) return null;

    const tileLayers = targetIds
      .map(id => {
        const options = optionsFromCapabilities(capabilities, { layer: id, projection: SWISS_PROJECTION });
        if (!options) return null;
        // crossOrigin "anonymous" is required for the cross-origin base map host: without it the tile
        // images taint OpenLayers' tile canvas, and the renderer's read-back during compositing then
        // throws a SecurityError, so the base map silently fails to render (while the feature layer still
        // shows). Requesting the tiles with CORS keeps the canvas clean; the host sends
        // Access-Control-Allow-Origin and is allow-listed by the Content-Security-Policy (img-src / connect-src).
        // The layer identifier is used as the title so the layer switcher can label it.
        return new TileLayer({ source: new WMTS({ ...options, crossOrigin: "anonymous" }), properties: { title: id } });
      })
      .filter((layer): layer is TileLayer<WMTS> => layer !== null);
    if (tileLayers.length === 0) return null;

    if (tileLayers.length === 1) {
      if (title) tileLayers[0].set(LayerSwitcherProperties.TITLE, title);
      return tileLayers[0];
    }

    const serviceTitle = capabilities?.ServiceIdentification?.Title;
    const groupTitle =
      title || (typeof serviceTitle === "string" && serviceTitle.trim()) || new URL(capabilitiesUrl).host;
    return new LayerGroup({ layers: tileLayers, properties: { title: groupTitle } });
  } catch (error) {
    // Don't fail the whole map if the base map is unavailable (e.g. the map service is unreachable or
    // its host is not allow-listed by the Content-Security-Policy). The feature layer still renders.
    console.warn("Failed to load WMTS base map; rendering features only.", error);
    return null;
  }
};

// Returns a transparent variant of a hex color (#rrggbb) by appending a 20% alpha channel, used as the
// polygon fill so the underlying map stays visible. Other color formats are returned unchanged.
const toTransparent = (color: string): string => (/^#[0-9a-f]{6}$/i.test(color) ? `${color}33` : color);

// Builds a vector layer for one feature layer of the config. Points are drawn as circle markers filled
// with the layer color; lines and polygon outlines are stroked with it, polygons filled with a
// transparent variant of it. Each feature keeps its info text so it can be shown in a popup on click.
const buildFeatureLayer = (
  layer: MapLayer,
  color: string,
  title: string,
  highlightColor: string,
  visibleIdsRef: MutableRefObject<ReadonlySet<string> | undefined>,
  highlightedIdsRef: MutableRefObject<ReadonlySet<string>>,
): VectorLayer<VectorSource> => {
  const source = new VectorSource();
  for (const mapFeature of layer.features ?? []) {
    try {
      const geometry = wktFormat.readGeometry(mapFeature.geom, { dataProjection: SWISS_PROJECTION });
      const feature = new Feature({ geometry });
      feature.set("info", mapFeature.info);
      feature.set("errorId", mapFeature.errorId);
      source.addFeature(feature);
    } catch {
      // Skip features with unparseable geometry rather than failing the whole map.
    }
  }

  const defaultStyle = new Style({
    image: new Circle({
      radius: 6,
      fill: new Fill({ color }),
      stroke: new Stroke({ color: "#ffffff", width: 2 }),
    }),
    stroke: new Stroke({ color, width: 2 }),
    fill: new Fill({ color: toTransparent(color) }),
  });
  const highlightStyle = new Style({
    image: new Circle({
      radius: 9,
      fill: new Fill({ color }),
      stroke: new Stroke({ color: highlightColor, width: 3 }),
    }),
    stroke: new Stroke({ color: highlightColor, width: 3 }),
    fill: new Fill({ color: toTransparent(color) }),
  });

  return new VectorLayer({
    source,
    properties: { [LayerSwitcherProperties.TITLE]: title },
    // A style function (not a static style): filtering hides non-matching features and selection emphasizes
    // highlighted ones. It reads the current sets from refs, which the map's update effect keeps in sync,
    // so changing filter/selection only restyles the existing layer instead of rebuilding the map.
    style: feature => {
      const errorId = feature.get("errorId") as string | undefined;
      const visible = visibleIdsRef.current;
      if (errorId !== undefined && visible !== undefined && !visible.has(errorId)) return undefined;
      return errorId !== undefined && highlightedIdsRef.current.has(errorId) ? highlightStyle : defaultStyle;
    },
  });
};

// Padding and max zoom used whenever the view is fit to the features, both on initial render and via the
// reset-viewport button, so the two stay in sync.
const FIT_OPTIONS = { padding: [40, 40, 40, 40], maxZoom: 12 };

// Returns the extent the view should fit: the combined bounding box of all feature layers, falling back to
// the whole country when there are no features.
const getFitExtent = (featureLayers: VectorLayer<VectorSource>[]): number[] => {
  const featureExtent = createEmpty();
  for (const featureLayer of featureLayers) {
    const extent = featureLayer.getSource()?.getExtent();
    if (extent) extendExtent(featureExtent, extent);
  }
  return isExtentEmpty(featureExtent) ? SWISS_EXTENT : featureExtent;
};

interface MapVisualizationProps {
  /** The map visualization config to render. */
  config: MapVisualizationConfig;
  /** Error ids currently visible (filter result); undefined means no filter (show all). */
  visibleErrorIds?: ReadonlySet<string>;
  /** Error ids to highlight (current selection); empty set means none. */
  highlightedErrorIds: ReadonlySet<string>;
  /** Called with the error id when a feature is clicked. */
  onSelectFeature: (errorId: string) => void;
  /** Whether to show the info popup on feature click. Suppressed when the tree below already shows the details. */
  showPopup: boolean;
}

export const MapVisualization = ({
  config,
  visibleErrorIds,
  highlightedErrorIds,
  onSelectFeature,
  showPopup,
}: MapVisualizationProps) => {
  const { t } = useTranslation();
  const theme = useTheme();
  const mapContainerRef = useRef<HTMLDivElement>(null);
  // The map instance and feature layers are kept in refs so the reset-viewport button can re-fit the view
  // after the initializing effect has finished.
  const mapRef = useRef<Map | undefined>(undefined);
  const featureLayersRef = useRef<VectorLayer<VectorSource>[]>([]);
  const [map, setMap] = useState<Map | null>(null);

  // The feature style function reads the current filter/selection from refs, so changing them only restyles
  // the existing layers (cheap) instead of rebuilding the map (which would re-fetch the WMTS base map).
  const visibleIdsRef = useRef<ReadonlySet<string> | undefined>(visibleErrorIds);
  const highlightedIdsRef = useRef<ReadonlySet<string>>(highlightedErrorIds);
  const onSelectRef = useRef(onSelectFeature);
  onSelectRef.current = onSelectFeature;
  const showPopupRef = useRef(showPopup);
  showPopupRef.current = showPopup;

  const resetViewport = useCallback(() => {
    const map = mapRef.current;
    if (!map) return;
    map.getView().fit(getFitExtent(featureLayersRef.current), FIT_OPTIONS);
  }, []);

  useEffect(() => {
    let cancelled = false;
    let map: Map | undefined;
    setMap(null);

    const initialize = async () => {
      try {
        registerSwissProjection();
        if (cancelled || !mapContainerRef.current) return;

        const featureLayers = config.layers
          .filter(layer => layer.features)
          .map(layer =>
            buildFeatureLayer(
              layer,
              layer.color ?? theme.palette.error.main,
              localized(layer.title) || t("mapVisualizationFeatureLayer"),
              theme.palette.primary.main,
              visibleIdsRef,
              highlightedIdsRef,
            ),
          );
        const layers: BaseLayer[] = [...featureLayers];

        const wmtsLayerConfig = config.layers.find(layer => layer.wmts);
        if (wmtsLayerConfig?.wmts) {
          const wmtsLayer = await buildWmtsLayer(
            wmtsLayerConfig.wmts,
            wmtsLayerConfig.layerIds,
            localized(wmtsLayerConfig.title),
          );
          if (cancelled) return;
          // Base map is drawn below the features.
          if (wmtsLayer) layers.unshift(wmtsLayer);
        }
        if (cancelled || !mapContainerRef.current) return;

        // The popup element is created imperatively rather than rendered by React, because OpenLayers'
        // Overlay moves the element out of its original parent into the map's overlay container. A
        // React-rendered node handed to OL would no longer be where React expects it, and the next sibling
        // insert/remove in this component would throw "insertBefore … not a child of this node".
        const popupElement = document.createElement("div");
        Object.assign(popupElement.style, {
          display: "none",
          backgroundColor: theme.palette.background.paper,
          border: `1px solid ${theme.palette.primary.light}`,
          borderRadius: "4px",
          padding: "8px 12px",
          maxWidth: "300px",
          boxShadow: theme.shadows[2],
          fontSize: "0.875rem",
          pointerEvents: "none",
        });
        const overlay = new Overlay({
          element: popupElement,
          positioning: "bottom-center",
          offset: [0, -12],
          stopEvent: false,
        });

        map = new Map({
          target: mapContainerRef.current,
          layers,
          overlays: [overlay],
          view: new View({ projection: SWISS_PROJECTION, extent: SWISS_EXTENT }),
        });
        mapRef.current = map;
        featureLayersRef.current = featureLayers;

        // Fit the view to the combined extent of all feature layers, falling back to the whole country
        // when there are no features.
        map.getView().fit(getFitExtent(featureLayers), FIT_OPTIONS);

        map.on("click", event => {
          const feature = map?.forEachFeatureAtPixel(event.pixel, f => f);
          const info = feature?.get("info") as string | undefined;
          const errorId = feature?.get("errorId") as string | undefined;
          if (showPopupRef.current && feature && info) {
            popupElement.textContent = info;
            popupElement.style.display = "block";
            overlay.setPosition(event.coordinate);
          } else {
            popupElement.style.display = "none";
            overlay.setPosition(undefined);
          }
          if (errorId) onSelectRef.current(errorId);
        });

        map.on("pointermove", event => {
          const hit = map?.hasFeatureAtPixel(event.pixel) ?? false;
          map!.getTargetElement().style.cursor = hit ? "pointer" : "";
        });

        if (!cancelled) {
          setMap(map);
        }
      } catch (error) {
        console.error("Failed to render map visualization.", error);
      }
    };

    initialize();

    return () => {
      cancelled = true;
      setMap(null);
      map?.setTarget(undefined);
      mapRef.current = undefined;
      featureLayersRef.current = [];
    };
  }, [config, theme, t]);

  // Apply filter (hide non-matching) and selection (highlight + center) without rebuilding the map: update
  // the refs the style function reads, restyle the existing layers, and center on the current selection.
  useEffect(() => {
    visibleIdsRef.current = visibleErrorIds;
    highlightedIdsRef.current = highlightedErrorIds;
    if (!map) return;
    featureLayersRef.current.forEach(layer => layer.changed());

    if (highlightedErrorIds.size === 0) return;
    const extent = createEmpty();
    let count = 0;
    for (const layer of featureLayersRef.current) {
      for (const feature of layer.getSource()?.getFeatures() ?? []) {
        const errorId = feature.get("errorId") as string | undefined;
        if (errorId !== undefined && highlightedErrorIds.has(errorId)) {
          const geometry = feature.getGeometry();
          if (geometry) {
            extendExtent(extent, geometry.getExtent());
            count += 1;
          }
        }
      }
    }
    if (isExtentEmpty(extent)) return;
    // Only move the map when the selection is not already visible: clicking a feature on the map must not
    // yank it out from under the cursor, and an already-visible selection should stay put. An off-screen
    // single error gently centers (keeps zoom); an off-screen group fits to show all its points.
    const size = map.getSize();
    const viewExtent = size ? map.getView().calculateExtent(size) : undefined;
    if (viewExtent && containsExtent(viewExtent, extent)) return;
    if (count === 1) {
      map.getView().setCenter(getCenter(extent));
    } else {
      map.getView().fit(extent, FIT_OPTIONS);
    }
  }, [map, visibleErrorIds, highlightedErrorIds]);

  return (
    <Box sx={{ position: "relative", width: "100%", height: "400px" }}>
      <Box
        ref={mapContainerRef}
        data-cy="map-visualization"
        sx={{
          width: "100%",
          height: "100%",
          borderRadius: "4px",
          overflow: "hidden",
          border: theme => `1px solid ${theme.palette.primary.light}`,
          // Move the default zoom (+/-) control to the top-right; the layer switcher and reset-viewport
          // buttons occupy the top-left corner.
          "& .ol-zoom": { top: "8px", left: "auto", right: "8px" },
        }}
      />
      <LayerSwitcher map={map} />
      {map && (
        <Tooltip title={t("mapResetViewport")}>
          <IconButton
            data-cy="map-reset-viewport"
            onClick={resetViewport}
            sx={{
              position: "absolute",
              // Below the layer switcher button (40px tall at top 8px) with an 8px gap.
              top: "56px",
              left: "8px",
              backgroundColor: "background.paper",
              color: "text.secondary",
              boxShadow: 1,
              borderRadius: "4px",
              "&:hover": { backgroundColor: "background.paper", color: "text.primary" },
            }}>
            <CenterFocusStrongIcon />
          </IconButton>
        </Tooltip>
      )}
    </Box>
  );
};
