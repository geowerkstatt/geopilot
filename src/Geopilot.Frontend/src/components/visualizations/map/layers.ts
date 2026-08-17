import { MutableRefObject } from "react";
import { alpha } from "@mui/material";
import { createEmpty, extend as extendExtent, Extent, isEmpty as isExtentEmpty } from "ol/extent";
import Feature, { FeatureLike } from "ol/Feature";
import WKT from "ol/format/WKT";
import WMTSCapabilities from "ol/format/WMTSCapabilities";
import Point from "ol/geom/Point";
import BaseLayer from "ol/layer/Base";
import LayerGroup from "ol/layer/Group";
import TileLayer from "ol/layer/Tile";
import VectorLayer from "ol/layer/Vector";
import Map from "ol/Map";
import Cluster from "ol/source/Cluster";
import VectorSource from "ol/source/Vector";
import WMTS, { optionsFromCapabilities } from "ol/source/WMTS";
import { Circle, Fill, Stroke, Style, Text } from "ol/style";
import { FitOptions } from "ol/View";
import { LocalizedText, MapLayer } from "../../../api/apiInterfaces";

const LOCALIZABLE_TITLE_PROPERTY = "localizableTitle";

const wktFormat = new WKT();

// Loads the capabilities as an XML Document via XMLHttpRequest instead of fetch to conform to the
// "require-trusted-types-for 'script'" CSP.
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
        // images taint OpenLayers' tile canvas and the base map silently fails to render.
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
    console.warn("Failed to load WMTS base map; rendering features only.", error);
    return null;
  }
};

/** Colours used to render error features: the marker fill, its outline, and the fill of a selected marker. */
interface MapFeatureColors {
  fill: string;
  stroke: string;
  highlight: string;
}

/** Pixel distance within which point features are merged into one cluster marker. */
const CLUSTER_DISTANCE = 40;

/** Pixel distance kept between two cluster markers so their counts stay readable. */
const CLUSTER_MIN_DISTANCE = 24;

/** The property under which a cluster feature carries the features it stands for. */
const CLUSTER_MEMBERS_PROPERTY = "features";

/** Returns the features a cluster contains, or the feature itself, if its not a cluster. */
export const getClusterMembers = (feature: FeatureLike): Feature[] =>
  (feature.get(CLUSTER_MEMBERS_PROPERTY) as Feature[] | undefined) ?? [feature as Feature];

export const buildFeatureLayer = (
  layer: MapLayer,
  colors: MapFeatureColors,
  title: LocalizedText | undefined,
  projection: string,
  visibleIdsRef: MutableRefObject<ReadonlySet<string> | undefined>,
  highlightedIdsRef: MutableRefObject<ReadonlySet<string>>,
): VectorLayer<VectorSource> => {
  const source = new VectorSource();
  for (const mapFeature of layer.features ?? []) {
    try {
      const geometry = wktFormat.readGeometry(mapFeature.geom, { dataProjection: projection });
      const feature = new Feature({
        geometry,
        info: mapFeature.info,
      });
      feature.setId(mapFeature.errorId);
      source.addFeature(feature);
    } catch {
      // Skip features with unparseable geometry rather than failing the whole map.
    }
  }

  const isVisible = (feature: Feature): boolean => {
    const id = feature.getId()?.toString();
    const visible = visibleIdsRef.current;
    return id === undefined || visible === undefined || visible.has(id);
  };

  // Clustering keeps markers of nearby errors from covering each other. It reduces every feature to a
  // point, so a layer holding any other geometry is left unclustered instead of losing those shapes.
  const clusterable = source.getFeatures().every(feature => feature.getGeometry() instanceof Point);
  const layerSource = clusterable
    ? new Cluster({
        source,
        distance: CLUSTER_DISTANCE,
        minDistance: CLUSTER_MIN_DISTANCE,
        // Filtered out features contribute no point, which keeps them out of the clusters and their counts.
        geometryFunction: feature => (isVisible(feature) ? (feature.getGeometry() as Point) : null),
      })
    : source;

  const defaultStyle = new Style({
    image: new Circle({
      radius: 6,
      fill: new Fill({ color: colors.fill }),
      stroke: new Stroke({ color: colors.stroke, width: 2 }),
    }),
    stroke: new Stroke({ color: colors.fill, width: 2 }),
    fill: new Fill({ color: alpha(colors.fill, 0.2) }),
  });
  const highlightStyle = new Style({
    image: new Circle({
      radius: 9,
      fill: new Fill({ color: colors.highlight }),
      stroke: new Stroke({ color: colors.stroke, width: 3 }),
    }),
    stroke: new Stroke({ color: colors.highlight, width: 3 }),
    fill: new Fill({ color: alpha(colors.highlight, 0.2) }),
    // Highlighted features are drawn on top so nearby unselected markers cannot cover them.
    zIndex: 1,
  });
  const buildClusterStyle = (color: string, zIndex: number) =>
    new Style({
      image: new Circle({
        radius: 12,
        fill: new Fill({ color }),
        stroke: new Stroke({ color: colors.stroke, width: 2 }),
      }),
      text: new Text({ font: "bold 12px sans-serif", fill: new Fill({ color: colors.stroke }) }),
      zIndex,
    });
  const defaultClusterStyle = buildClusterStyle(colors.fill, 0);
  const highlightClusterStyle = buildClusterStyle(colors.highlight, 1);

  return new VectorLayer({
    source: layerSource,
    properties: { [LOCALIZABLE_TITLE_PROPERTY]: title },
    // A style function (not a static style): filtering hides non-matching features and selection emphasizes
    // highlighted ones. It reads the current sets from refs, which the map's update effect keeps in sync,
    // so changing filter/selection only restyles the existing layer instead of rebuilding the map.
    style: feature => {
      const members = getClusterMembers(feature).filter(isVisible);
      if (members.length === 0) return undefined;
      const highlighted = members.some(member => {
        const id = member.getId()?.toString();
        return id !== undefined && highlightedIdsRef.current.has(id);
      });
      if (members.length === 1) return highlighted ? highlightStyle : defaultStyle;

      const clusterStyle = highlighted ? highlightClusterStyle : defaultClusterStyle;
      clusterStyle.getText()?.setText(members.length.toString());
      return clusterStyle;
    },
  });
};

/** The source holding the individual features: the wrapped source of a clustering layer, its own otherwise. */
const getFeatureSource = (layer: BaseLayer): VectorSource | null => {
  if (!(layer instanceof VectorLayer)) return null;
  const source = layer.getSource();
  return source instanceof Cluster ? source.getSource() : source;
};

/**
 * Re-applies the current filter and selection to a feature layer. A clustering layer has to re-cluster,
 * because the filter decides which features take part in a cluster and its count.
 */
export const refreshFeatureLayer = (layer: BaseLayer) => {
  const source = layer instanceof VectorLayer ? layer.getSource() : null;
  if (source instanceof Cluster) {
    source.refresh();
  } else {
    layer.changed();
  }
};

/** The combined extent of the specified features; empty when none of them has a geometry. */
export const getFeaturesExtent = (features: Feature[]): Extent => {
  const extent = createEmpty();
  for (const feature of features) {
    const geometry = feature.getGeometry();
    if (geometry) extendExtent(extent, geometry.getExtent());
  }
  return extent;
};

const getFitExtent = (featureLayers: BaseLayer[], fallback: Extent): Extent => {
  const featureExtent = createEmpty();
  for (const featureLayer of featureLayers) {
    const extent = getFeatureSource(featureLayer)?.getExtent();
    if (extent) extendExtent(featureExtent, extent);
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
  const matching: Feature[] = [];
  for (const featureLayer of featureLayers) {
    for (const feature of getFeatureSource(featureLayer)?.getFeatures() ?? []) {
      const id = feature.getId()?.toString();
      if (id !== undefined && featureIds.has(id)) matching.push(feature);
    }
  }
  return getFeaturesExtent(matching);
};

/** Fits the map view to the combined extent of the specified feature IDs, doing nothing if there are no matching features. */
export const fitToFeatures = (map: Map, featureIds: ReadonlySet<string>, options: FitOptions) => {
  const extent = getExtentForFeatureIds(map.getLayers().getArray(), featureIds);
  if (!isExtentEmpty(extent) && map.getSize()) {
    map.getView().fit(extent, options);
  }
};
