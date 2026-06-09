import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Box, CircularProgress, Typography } from "@mui/material";
import proj4 from "proj4";
import Map from "ol/Map";
import View from "ol/View";
import Overlay from "ol/Overlay";
import TileLayer from "ol/layer/Tile";
import VectorLayer from "ol/layer/Vector";
import VectorSource from "ol/source/Vector";
import WMTS, { optionsFromCapabilities } from "ol/source/WMTS";
import WMTSCapabilities from "ol/format/WMTSCapabilities";
import WKT from "ol/format/WKT";
import Feature from "ol/Feature";
import { Circle, Fill, Stroke, Style } from "ol/style";
import { register } from "ol/proj/proj4";
import { get as getProjection } from "ol/proj";
import BaseLayer from "ol/layer/Base";
import { MapVisualizationConfig, StepDownload } from "../../../api/apiInterfaces";
import useFetch from "../../../hooks/useFetch";
import { useTheme } from "@mui/material/styles";
import "ol/ol.css";

// The map visualization config uses Swiss LV95 (EPSG:2056) coordinates, matching the swisstopo base
// map and the coordinate reference system of INTERLIS error coordinates. OpenLayers only ships EPSG:4326
// and EPSG:3857 out of the box, so EPSG:2056 has to be registered with proj4 once.
const SWISS_PROJECTION = "EPSG:2056";
// Bounding extent of Switzerland in LV95, used as the WMTS tile grid extent and default map view extent.
const SWISS_EXTENT: [number, number, number, number] = [2420000, 1030000, 2900000, 1350000];
// Preferred swisstopo base map layers, in order; falls back to the first layer the capabilities expose.
const PREFERRED_WMTS_LAYERS = ["ch.swisstopo.pixelkarte-farbe", "ch.swisstopo.pixelkarte-grau"];

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

// Builds a WMTS tile layer from a capabilities document. Returns null when the capabilities cannot be
// fetched/parsed (e.g. the map service is unreachable) so the map still renders the feature layer.

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

const buildWmtsLayer = async (capabilitiesUrl: string): Promise<TileLayer<WMTS> | null> => {
  try {
    const capabilitiesDocument = await fetchCapabilitiesDocument(capabilitiesUrl);
    const capabilities = new WMTSCapabilities().read(capabilitiesDocument);

    const availableLayers: { Identifier?: string }[] = capabilities?.Contents?.Layer ?? [];
    const layerId =
      PREFERRED_WMTS_LAYERS.find(preferred => availableLayers.some(layer => layer.Identifier === preferred)) ??
      availableLayers[0]?.Identifier;
    if (!layerId) return null;

    const options = optionsFromCapabilities(capabilities, { layer: layerId, projection: SWISS_PROJECTION });
    if (!options) return null;

    // crossOrigin "anonymous" is required for the cross-origin base map host: without it the tile
    // images taint OpenLayers' tile canvas, and the renderer's read-back during compositing then throws
    // a SecurityError, so the base map silently fails to render (while the feature layer still shows).
    // Requesting the tiles with CORS keeps the canvas clean; the host sends Access-Control-Allow-Origin
    // and is allow-listed by the Content-Security-Policy (img-src / connect-src).
    return new TileLayer({ source: new WMTS({ ...options, crossOrigin: "anonymous" }) });
  } catch (error) {
    // Don't fail the whole map if the base map is unavailable (e.g. the map service is unreachable or
    // its host is not allow-listed by the Content-Security-Policy). The feature layer still renders.
    console.warn("Failed to load WMTS base map; rendering features only.", error);
    return null;
  }
};

// Builds a vector layer with a point marker per feature. Each feature keeps its info text so it can be
// shown in a popup on click.
const buildFeatureLayer = (config: MapVisualizationConfig, color: string): VectorLayer<VectorSource> => {
  const source = new VectorSource();
  for (const layer of config.layers) {
    if (!layer.features) continue;
    for (const mapFeature of layer.features) {
      try {
        const geometry = wktFormat.readGeometry(mapFeature.geom, { dataProjection: SWISS_PROJECTION });
        const feature = new Feature({ geometry });
        feature.set("info", mapFeature.info);
        source.addFeature(feature);
      } catch {
        // Skip features with unparseable geometry rather than failing the whole map.
      }
    }
  }

  return new VectorLayer({
    source,
    style: new Style({
      image: new Circle({
        radius: 6,
        fill: new Fill({ color }),
        stroke: new Stroke({ color: "#ffffff", width: 2 }),
      }),
    }),
  });
};

interface MapVisualizationProps {
  /** The map visualization config file to render (carries the URL to fetch the JSON config). */
  file: StepDownload;
}

export const MapVisualization = ({ file }: MapVisualizationProps) => {
  const { t } = useTranslation();
  const theme = useTheme();
  const { fetchApi } = useFetch();
  const mapContainerRef = useRef<HTMLDivElement>(null);
  const popupRef = useRef<HTMLDivElement>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [hasError, setHasError] = useState(false);
  const [popupContent, setPopupContent] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    let map: Map | undefined;
    setIsLoading(true);
    setHasError(false);

    const initialize = async () => {
      try {
        registerSwissProjection();
        const config = await fetchApi<MapVisualizationConfig>(file.url, { method: "GET" });
        if (cancelled || !mapContainerRef.current) return;

        const featureLayer = buildFeatureLayer(config, theme.palette.error.main);
        const layers: BaseLayer[] = [featureLayer];

        const wmtsUrl = config.layers.find(layer => layer.wmts)?.wmts;
        if (wmtsUrl) {
          const wmtsLayer = await buildWmtsLayer(wmtsUrl);
          if (cancelled) return;
          // Base map is drawn below the features.
          if (wmtsLayer) layers.unshift(wmtsLayer);
        }
        if (cancelled || !mapContainerRef.current) return;

        const overlay = popupRef.current
          ? new Overlay({ element: popupRef.current, positioning: "bottom-center", offset: [0, -12], stopEvent: false })
          : undefined;

        map = new Map({
          target: mapContainerRef.current,
          layers,
          overlays: overlay ? [overlay] : [],
          view: new View({ projection: SWISS_PROJECTION, extent: SWISS_EXTENT }),
        });

        // Fit the view to the error features, falling back to the whole country when there are none.
        // An empty source reports an "inverted" extent ([Infinity, Infinity, -Infinity, -Infinity]).
        const featureExtent = featureLayer.getSource()?.getExtent();
        const hasFeatures =
          featureExtent && featureExtent[0] <= featureExtent[2] && featureExtent[1] <= featureExtent[3];
        map.getView().fit(hasFeatures ? featureExtent : SWISS_EXTENT, {
          padding: [40, 40, 40, 40],
          maxZoom: 12,
        });

        map.on("click", event => {
          const feature = map?.forEachFeatureAtPixel(event.pixel, f => f);
          const info = feature?.get("info") as string | undefined;
          if (feature && info && overlay) {
            overlay.setPosition(event.coordinate);
            setPopupContent(info);
          } else {
            overlay?.setPosition(undefined);
            setPopupContent(null);
          }
        });

        map.on("pointermove", event => {
          const hit = map?.hasFeatureAtPixel(event.pixel) ?? false;
          map!.getTargetElement().style.cursor = hit ? "pointer" : "";
        });

        if (!cancelled) setIsLoading(false);
      } catch (error) {
        if (!cancelled) {
          console.error("Failed to render map visualization.", error);
          setHasError(true);
          setIsLoading(false);
        }
      }
    };

    initialize();

    return () => {
      cancelled = true;
      map?.setTarget(undefined);
    };
  }, [file.url, fetchApi, theme.palette.error.main]);

  if (hasError) {
    return (
      <Typography variant="body1" color="error">
        {t("mapVisualizationError")}
      </Typography>
    );
  }

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
        }}
      />
      {isLoading && (
        <Box
          sx={{
            position: "absolute",
            inset: 0,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            backgroundColor: "rgba(255, 255, 255, 0.6)",
          }}>
          <CircularProgress />
        </Box>
      )}
      <Box
        ref={popupRef}
        sx={{
          display: popupContent ? "block" : "none",
          backgroundColor: "white",
          border: theme => `1px solid ${theme.palette.primary.light}`,
          borderRadius: "4px",
          padding: "8px 12px",
          maxWidth: "300px",
          boxShadow: 2,
          fontSize: "0.875rem",
          pointerEvents: "none",
        }}>
        {popupContent}
      </Box>
    </Box>
  );
};
