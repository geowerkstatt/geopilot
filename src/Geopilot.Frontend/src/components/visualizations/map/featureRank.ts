import { getHeight, getWidth } from "ol/extent";
import Feature, { FeatureLike } from "ol/Feature";
import Geometry from "ol/geom/Geometry";
import GeometryCollection from "ol/geom/GeometryCollection";
import RenderFeature from "ol/render/Feature";

// Free of React, MUI and the app's types, so a Cypress spec can test it on its own.

/** Pixels a feature may span and still disappear under the marker of a point picked above it. */
const HIDDEN_UNDER_MARKER = 24;

/**
 * Where a geometry stands when features overlap: areas 0, lines 1, points 2. A higher rank is drawn above a lower one
 * and picked before it, so a large area neither covers nor swallows the points in it. A collection, the locations of
 * an object in a diff for example, stands where its highest part stands.
 */
export const getRank = (geometry: Geometry | RenderFeature | undefined): number => {
  if (geometry instanceof GeometryCollection) return Math.max(0, ...geometry.getGeometries().map(getRank));
  switch (geometry?.getType()) {
    case "Point":
    case "MultiPoint":
      return 2;
    case "LineString":
    case "MultiLineString":
    case "LinearRing":
      return 1;
    default:
      return 0;
  }
};

export const getGeometryRank = (feature: FeatureLike): number => getRank(feature.getGeometry());

const isHiddenUnderMarker = (feature: Feature, resolution: number): boolean => {
  const extent = feature.getGeometry()?.getExtent();
  return (
    extent !== undefined &&
    getWidth(extent) / resolution <= HIDDEN_UNDER_MARKER &&
    getHeight(extent) / resolution <= HIDDEN_UNDER_MARKER
  );
};

/**
 * The features the popup offers of those under the pointer, in pick order: the ones of the picked kind, and of the
 * kinds beneath it only those too small at this resolution to be clicked beside the picked one.
 */
export const getPopupFeatures = (candidates: Feature[], resolution: number): Feature[] => {
  const [picked] = candidates;
  if (!picked) return [];
  const rank = getGeometryRank(picked);
  return candidates.filter(
    candidate => getGeometryRank(candidate) === rank || isHiddenUnderMarker(candidate, resolution),
  );
};
