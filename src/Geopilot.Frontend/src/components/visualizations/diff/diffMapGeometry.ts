import { Coordinate, squaredDistanceToSegment } from "ol/coordinate";
import { Extent } from "ol/extent";
import Feature from "ol/Feature";
import WKT from "ol/format/WKT";
import Geometry from "ol/geom/Geometry";
import GeometryCollection from "ol/geom/GeometryCollection";
import LineString from "ol/geom/LineString";
import MultiLineString from "ol/geom/MultiLineString";
import MultiPolygon from "ol/geom/MultiPolygon";
import Point from "ol/geom/Point";
import Polygon from "ol/geom/Polygon";
import SimpleGeometry from "ol/geom/SimpleGeometry";
import type { DiffChange, DiffChangeType, DiffState } from "../../../api/apiInterfaces";
import { isFittableExtent } from "../map/picking";

// Free of React and MUI, so a Cypress spec can test it on its own.

/** The part of a diff change the map needs. */
export type LocatedChange = Pick<DiffChange, "objectId" | "changeType" | "valueType" | "oldLocation" | "newLocation">;

/** A jump to one change in one state. */
export interface DiffMapFocus {
  change: LocatedChange;
  state: DiffState;
}

/** Per object and state, the change type of each part that has a geometry change of its own, by part key. */
export type PartTypeIndex = ReadonlyMap<string, Record<DiffState, ReadonlyMap<string, DiffChangeType>>>;

export type DecorationRole = "connector" | "focus" | "counterpart";

/** How a geometry is drawn: the shape carries the change type, so the map stays readable without color. */
export type DiffLook = "crisp" | "dotted" | "ghost" | "dashed";

export const getLook = (changeType: DiffChangeType, state: DiffState): DiffLook => {
  if (changeType === "added") return "dotted";
  if (changeType === "deleted") return "dashed";
  return state === "old" ? "ghost" : "crisp";
};

const wkt = new WKT();

export const readLocation = (location: string | undefined): Geometry | undefined => {
  if (!location) return undefined;
  try {
    return wkt.readGeometry(location);
  } catch {
    return undefined;
  }
};

const locationOf = (change: LocatedChange, state: DiffState) =>
  readLocation(state === "old" ? change.oldLocation : change.newLocation);

export const getParts = (geometry: Geometry): Geometry[] =>
  geometry instanceof GeometryCollection ? geometry.getGeometriesArray() : [geometry];

// Parts and locations come from the same WKT of the diff tool, so equal coordinates mean the same location.
const getPartKey = (geometry: Geometry): string =>
  geometry instanceof SimpleGeometry ? `${geometry.getType()}:${geometry.getFlatCoordinates().join(",")}` : "";

export const buildPartTypeIndex = (changes: LocatedChange[]): PartTypeIndex => {
  const index = new Map<string, Record<DiffState, Map<string, DiffChangeType>>>();
  for (const change of changes) {
    if (change.valueType !== "geometry") continue;
    for (const state of ["old", "new"] as const) {
      const location = locationOf(change, state);
      if (!location) continue;
      let states = index.get(change.objectId);
      if (!states) {
        states = { old: new Map(), new: new Map() };
        index.set(change.objectId, states);
      }
      const key = getPartKey(location);
      if (!states[state].has(key)) states[state].set(key, change.changeType);
    }
  }
  return index;
};

export const resolvePartType = (
  index: PartTypeIndex,
  objectId: string,
  state: DiffState,
  part: Geometry,
  objectType: DiffChangeType,
): DiffChangeType => index.get(objectId)?.[state].get(getPartKey(part)) ?? objectType;

// The diff tool writes coordinates to the millimeter, so a line on the edge of a surface lies within a millimeter of it.
const EDGE_TOLERANCE = 0.001;

const getRings = (surface: Geometry): Coordinate[][] => {
  if (surface instanceof Polygon) return surface.getCoordinates();
  if (surface instanceof MultiPolygon) return surface.getCoordinates().flat();
  return [];
};

const getLines = (line: Geometry): Coordinate[][] => {
  if (line instanceof LineString) return [line.getCoordinates()];
  if (line instanceof MultiLineString) return line.getCoordinates();
  return [];
};

const isOnRing = (point: Coordinate, ring: Coordinate[]) =>
  ring.some(
    (end, i) => i > 0 && squaredDistanceToSegment(point, [ring[i - 1], end]) <= EDGE_TOLERANCE * EDGE_TOLERANCE,
  );

/**
 * Whether every segment of a line lies on the outline of a surface, so the outline already draws it. A segment counts
 * when its ends and its middle do, which keeps a chord between two corners off the outline.
 */
export const liesOnSurfaceEdge = (line: Geometry, surface: Geometry): boolean => {
  const rings = getRings(surface);
  const lines = getLines(line);
  const isOnEdge = (point: Coordinate) => rings.some(ring => isOnRing(point, ring));
  return (
    lines.length > 0 &&
    lines.every(
      coordinates =>
        coordinates.length > 1 &&
        coordinates.every(
          (point, i) =>
            isOnEdge(point) &&
            (i === 0 || isOnEdge([(coordinates[i - 1][0] + point[0]) / 2, (coordinates[i - 1][1] + point[1]) / 2])),
        ),
    )
  );
};

export const getChangeExtent = (change: LocatedChange, state: DiffState): Extent | undefined => {
  const extent = locationOf(change, state)?.getExtent();
  return extent && isFittableExtent(extent) ? extent : undefined;
};

/** A jump to the change in that state, or none without a location there to jump to. */
export const getFocus = (change: LocatedChange, state: DiffState): DiffMapFocus | null =>
  getChangeExtent(change, state) ? { change, state } : null;

const buildConnector = (change: LocatedChange): Feature | undefined => {
  const before = locationOf(change, "old");
  const after = locationOf(change, "new");
  if (change.valueType !== "geometry" || !(before instanceof Point) || !(after instanceof Point)) return undefined;
  const from = before.getCoordinates();
  const to = after.getCoordinates();
  if (from[0] === to[0] && from[1] === to[1]) return undefined;
  const role: DecorationRole = "connector";
  return new Feature({ geometry: new LineString([from, to]), role, objectId: change.objectId });
};

const buildFocusFeatures = (focus: DiffMapFocus): Feature[] =>
  (["old", "new"] as const).flatMap(state => {
    const geometry = locationOf(focus.change, state);
    const role: DecorationRole = state === focus.state ? "focus" : "counterpart";
    return geometry ? [new Feature({ geometry, role, state, objectId: focus.change.objectId })] : [];
  });

/** Lines from the old to the new position of each moved point; features carry `role` and `objectId`. */
export const buildConnectors = (changes: LocatedChange[]): Feature[] =>
  changes.flatMap(change => buildConnector(change) ?? []);

/** The connectors and, when given, the focused pair of the objects the filter shows; features carry `role`, `objectId` and `state`. */
export const buildDecorations = (
  connectors: Feature[],
  focus: DiffMapFocus | null,
  visibleIds?: ReadonlySet<string>,
): Feature[] => {
  const isVisible = (objectId: unknown) => !visibleIds || (typeof objectId === "string" && visibleIds.has(objectId));
  const shownConnectors = connectors.filter(connector => isVisible(connector.get("objectId")));
  const pair = focus && isVisible(focus.change.objectId) ? buildFocusFeatures(focus) : [];
  return [...shownConnectors, ...pair];
};
