import { MutableRefObject } from "react";
import { createEmpty, extend as extendExtent, Extent, isEmpty as isExtentEmpty } from "ol/extent";
import Feature from "ol/Feature";
import WKT from "ol/format/WKT";
import WMTSCapabilities from "ol/format/WMTSCapabilities";
import BaseLayer from "ol/layer/Base";
import LayerGroup from "ol/layer/Group";
import TileLayer from "ol/layer/Tile";
import VectorLayer from "ol/layer/Vector";
import Map from "ol/Map";
import VectorSource from "ol/source/Vector";
import WMTS, { optionsFromCapabilities } from "ol/source/WMTS";
import { Circle, Fill, Stroke, Style } from "ol/style";
import { FitOptions } from "ol/View";
import { LocalizedText, MapLayer } from "../../../api/apiInterfaces";

const LOCALIZABLE_TITLE_PROPERTY = "localizableTitle";

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
export const buildWmtsLayer = async (
  capabilitiesUrl: string,
  projection: string,
  layerIds?: string[],
  title?: LocalizedText,
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
        const options = optionsFromCapabilities(capabilities, { layer: id, projection });
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
      if (title) tileLayers[0].set(LOCALIZABLE_TITLE_PROPERTY, title);
      return tileLayers[0];
    }

    const serviceTitle = capabilities?.ServiceIdentification?.Title;
    const groupProperties = title
      ? { [LOCALIZABLE_TITLE_PROPERTY]: title }
      : { title: (typeof serviceTitle === "string" && serviceTitle.trim()) || new URL(capabilitiesUrl).host };
    return new LayerGroup({ layers: tileLayers, properties: groupProperties });
  } catch (error) {
    // Don't fail the whole map if the base map is unavailable (e.g. the map service is unreachable or
    // its host is not allow-listed by the Content-Security-Policy). The feature layer still renders.
    console.warn("Failed to load WMTS base map; rendering features only.", error);
    return null;
  }
};

// Builds a vector layer for one feature layer of the config. Points are drawn as circle markers filled
// with the layer color; lines and polygon outlines are stroked with it, polygons filled with a
// transparent variant of it. Each feature keeps its info text so it can be shown in a popup on click.
export const buildFeatureLayer = (
  layer: MapLayer,
  color: string,
  title: LocalizedText | undefined,
  highlightColor: string,
  projection: string,
  visibleIdsRef: MutableRefObject<ReadonlySet<string> | undefined>,
  highlightedIdsRef: MutableRefObject<ReadonlySet<string>>,
): VectorLayer<VectorSource> => {
  const source = new VectorSource();
  for (const mapFeature of layer.features ?? []) {
    try {
      const geometry = wktFormat.readGeometry(mapFeature.geom, { dataProjection: projection });
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
      stroke: new Stroke({ color: highlightColor, width: 2 }),
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
    properties: { [LOCALIZABLE_TITLE_PROPERTY]: title },
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

// Returns a transparent variant of a hex color (#rrggbb) by appending a 20% alpha channel, used as the
// polygon fill so the underlying map stays visible. Other color formats are returned unchanged.
const toTransparent = (color: string): string => (/^#[0-9a-f]{6}$/i.test(color) ? `${color}33` : color);

const getFitExtent = (featureLayers: BaseLayer[], fallback: Extent): Extent => {
  const featureExtent = createEmpty();
  for (const featureLayer of featureLayers) {
    if (featureLayer instanceof VectorLayer) {
      const extent = featureLayer.getSource()?.getExtent();
      if (extent) extendExtent(featureExtent, extent);
    }
  }
  return isExtentEmpty(featureExtent) ? fallback : featureExtent;
};

/** Fits the map view to the combined extent of all feature layers, falling back to the specified extent if there are no features. */
export const fitToLayers = (map: Map, options: FitOptions, fallback: Extent) => {
  if (map.getSize()) {
    map.getView().fit(getFitExtent(map.getLayers().getArray(), fallback), options);
  }
};

const getExtentForFeatureIds = (featureLayers: BaseLayer[], featureIds: ReadonlySet<string>): Extent => {
  const extent = createEmpty();
  for (const featureLayer of featureLayers) {
    if (!(featureLayer instanceof VectorLayer)) continue;

    for (const feature of featureLayer.getSource()?.getFeatures() ?? []) {
      const featureId = feature.get("errorId") as string | undefined;
      if (featureId !== undefined && featureIds.has(featureId)) {
        const geometry = feature.getGeometry();
        if (geometry) {
          extendExtent(extent, geometry.getExtent());
        }
      }
    }
  }
  return extent;
};

/** Fits the map view to the combined extent of the specified feature IDs, doing nothing if there are no matching features. */
export const fitToFeatures = (map: Map, featureIds: ReadonlySet<string>, options: FitOptions) => {
  const extent = getExtentForFeatureIds(map.getLayers().getArray(), featureIds);
  if (!isExtentEmpty(extent) && map.getSize()) {
    map.getView().fit(extent, options);
  }
};
