import { MutableRefObject } from "react";
import { alpha } from "@mui/material";
import { createEmpty, extend as extendExtent, Extent, getArea, isEmpty as isExtentEmpty } from "ol/extent";
import Feature, { FeatureLike } from "ol/Feature";
import WKT from "ol/format/WKT";
import WMTSCapabilities from "ol/format/WMTSCapabilities";
import MultiPolygon from "ol/geom/MultiPolygon";
import Point from "ol/geom/Point";
import Polygon from "ol/geom/Polygon";
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
import { getClusterMembers, getGeometryRank } from "./picking";

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

/** Colors used to render features: the marker fill, its outline, and the fill of a selected marker. */
interface MapFeatureColors {
  fill: string;
  stroke: string;
  highlight: string;
}

/** Pixel distance within which point features are merged into one cluster marker. */
const CLUSTER_DISTANCE = 40;

/** Pixel distance kept between two cluster markers so their counts stay readable. */
const CLUSTER_MIN_DISTANCE = 24;

/** The layer property holding the `style` of the map layer a feature layer was built from. */
export const MAP_LAYER_STYLE_PROPERTY = "mapLayerStyle";

/**
 * The area a feature covers, in m² as the map is in EPSG:2056. Other geometries, a collection for example, fall back
 * to their bounding box.
 */
const getCoveredArea = (feature: Feature): number => {
  const geometry = feature.getGeometry();
  if (geometry instanceof Polygon || geometry instanceof MultiPolygon) return geometry.getArea();
  return geometry ? getArea(geometry.getExtent()) : 0;
};

/**
 * The distinct features under the pointer in the order they are offered for picking: points before lines before
 * areas, and a smaller area before a larger one. A feature in both the old and the new state counts once.
 */
export const getPickOrder = (hits: FeatureLike[]): Feature[] => {
  const pickedIds = new Set<string>();
  const features: Feature[] = [];
  for (const member of hits.flatMap(getClusterMembers)) {
    const id = member.getId()?.toString() ?? "";
    if (pickedIds.has(id)) continue;
    pickedIds.add(id);
    features.push(member);
  }
  // The sort is stable, so among equals the topmost hit stays first.
  return features.sort((a, b) => getGeometryRank(b) - getGeometryRank(a) || getCoveredArea(a) - getCoveredArea(b));
};

export const buildFeatureLayer = (
  layer: MapLayer,
  colors: MapFeatureColors,
  title: LocalizedText | undefined,
  projection: string,
  visibleIdsRef: MutableRefObject<ReadonlySet<string> | undefined>,
  highlightedIdsRef: MutableRefObject<ReadonlySet<string>>,
  hoveredIdsRef: MutableRefObject<ReadonlySet<string>>,
): VectorLayer<VectorSource> => {
  const source = new VectorSource();
  for (const mapFeature of layer.features ?? []) {
    try {
      const geometry = wktFormat.readGeometry(mapFeature.geom, { dataProjection: projection });
      const feature = new Feature({
        geometry,
        info: mapFeature.info,
      });
      feature.setId(mapFeature.id);
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
  // point, so a layer holding any other geometry is left unclustered instead of losing those shapes. A diff layer is
  // never clustered: it holds one state only, so a cluster could not join an object's before and after.
  const clusterable = !layer.style && source.getFeatures().every(feature => feature.getGeometry() instanceof Point);
  const layerSource = clusterable
    ? new Cluster({
        source,
        distance: CLUSTER_DISTANCE,
        minDistance: CLUSTER_MIN_DISTANCE,
        // Filtered out features contribute no point, which keeps them out of the clusters and their counts.
        geometryFunction: feature => (isVisible(feature) ? (feature.getGeometry() as Point) : null),
      })
    : source;

  // One style per emphasis and geometry rank. The rank keeps areas beneath lines and points, the emphasis lifts the
  // hovered and above it the selected feature over the others of its rank, so nearby markers cannot cover them while
  // an area still never covers a point. Hovered is a lighter version of selected: the usual color, drawn larger.
  const geometryRanks = [0, 1, 2];
  const buildStyles = (
    color: string,
    pointRadius: number,
    pointRingWidth: number,
    lineWidth: number,
    emphasis: number,
  ) =>
    geometryRanks.map(
      rank =>
        new Style({
          image: new Circle({
            radius: pointRadius,
            fill: new Fill({ color }),
            stroke: new Stroke({ color: colors.stroke, width: pointRingWidth }),
          }),
          stroke: new Stroke({ color, width: lineWidth }),
          fill: new Fill({ color: alpha(color, 0.2) }),
          zIndex: rank * 3 + emphasis,
        }),
    );
  const defaultStyles = buildStyles(colors.fill, 6, 2, 2, 0);
  const hoveredStyles = buildStyles(colors.fill, 8, 3, 4, 1);
  const highlightStyles = buildStyles(colors.highlight, 9, 3, 4, 2);
  const buildClusterStyle = (color: string, width: number, zIndex: number) =>
    new Style({
      image: new Circle({
        radius: 12,
        fill: new Fill({ color }),
        stroke: new Stroke({ color: colors.stroke, width }),
      }),
      text: new Text({ font: "bold 12px sans-serif", fill: new Fill({ color: colors.stroke }) }),
      zIndex,
    });
  const defaultClusterStyle = buildClusterStyle(colors.fill, 2, 0);
  const hoveredClusterStyle = buildClusterStyle(colors.fill, 4, 1);
  const highlightClusterStyle = buildClusterStyle(colors.highlight, 2, 2);

  return new VectorLayer({
    source: layerSource,
    properties: { [LOCALIZABLE_TITLE_PROPERTY]: title, [MAP_LAYER_STYLE_PROPERTY]: layer.style },
    // A style function (not a static style): filtering hides non-matching features and selection emphasizes
    // highlighted ones. It reads the current sets from refs, which the map's update effect keeps in sync,
    // so changing filter/selection only restyles the existing layer instead of rebuilding the map.
    style: feature => {
      const members = getClusterMembers(feature).filter(isVisible);
      if (members.length === 0) return undefined;
      const containsAny = (ids: ReadonlySet<string>) =>
        members.some(member => {
          const id = member.getId()?.toString();
          return id !== undefined && ids.has(id);
        });
      const highlighted = containsAny(highlightedIdsRef.current);
      const hovered = !highlighted && containsAny(hoveredIdsRef.current);
      if (members.length === 1) {
        const rank = getGeometryRank(members[0]);
        if (highlighted) return highlightStyles[rank];
        return hovered ? hoveredStyles[rank] : defaultStyles[rank];
      }

      let clusterStyle = defaultClusterStyle;
      if (highlighted) clusterStyle = highlightClusterStyle;
      else if (hovered) clusterStyle = hoveredClusterStyle;
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
