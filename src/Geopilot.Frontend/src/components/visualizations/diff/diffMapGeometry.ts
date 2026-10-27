import { Extent } from "ol/extent";
import Feature from "ol/Feature";
import WKT from "ol/format/WKT";
import Geometry from "ol/geom/Geometry";
import GeometryCollection from "ol/geom/GeometryCollection";
import LineString from "ol/geom/LineString";
import Point from "ol/geom/Point";
import SimpleGeometry from "ol/geom/SimpleGeometry";

// Free of React, MUI and the app's types, so a Cypress spec can test it on its own.

export type MapState = "old" | "new";
export type ChangeType = "added" | "deleted" | "changed";

/** The part of a diff change the map needs; a `DiffChange` satisfies it. */
export interface LocatedChange {
  objectId: string;
  changeType: ChangeType;
  valueType: string;
  oldLocation?: string;
  newLocation?: string;
}

/** A jump to one change in one state. */
export interface DiffMapFocus {
  change: LocatedChange;
  side: MapState;
}

/** Per object and state, the change type of each part that has a geometry change of its own, by part key. */
export type PartTypeIndex = ReadonlyMap<string, Record<MapState, ReadonlyMap<string, ChangeType>>>;

export type DecorationRole = "connector" | "focus" | "counterpart";

const wkt = new WKT();

export const readLocation = (location: string | undefined): Geometry | undefined => {
  if (!location) return undefined;
  try {
    return wkt.readGeometry(location);
  } catch {
    return undefined;
  }
};

const locationOf = (change: LocatedChange, state: MapState) =>
  readLocation(state === "old" ? change.oldLocation : change.newLocation);

export const getParts = (geometry: Geometry): Geometry[] =>
  geometry instanceof GeometryCollection ? geometry.getGeometries() : [geometry];

// Parts and locations come from the same WKT of the diff tool, so equal coordinates mean the same location.
const getPartKey = (geometry: Geometry): string =>
  geometry instanceof SimpleGeometry ? `${geometry.getType()}:${geometry.getFlatCoordinates().join(",")}` : "";

export const buildPartTypeIndex = (changes: LocatedChange[]): PartTypeIndex => {
  const index = new Map<string, Record<MapState, Map<string, ChangeType>>>();
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
  state: MapState,
  part: Geometry,
  objectType: ChangeType,
): ChangeType => index.get(objectId)?.[state].get(getPartKey(part)) ?? objectType;

/** Without the list the map knows no change types: an object only before was deleted, only after added. */
export const inferObjectTypes = (oldIds: Iterable<string>, newIds: Iterable<string>): Map<string, ChangeType> => {
  const oldSet = new Set(oldIds);
  const newSet = new Set(newIds);
  const types = new Map<string, ChangeType>();
  for (const id of oldSet) types.set(id, newSet.has(id) ? "changed" : "deleted");
  for (const id of newSet) if (!oldSet.has(id)) types.set(id, "added");
  return types;
};

export const getChangeExtent = (change: LocatedChange, side: MapState): Extent | undefined =>
  locationOf(change, side)?.getExtent();

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
  (["old", "new"] as const).flatMap(side => {
    const geometry = locationOf(focus.change, side);
    const role: DecorationRole = side === focus.side ? "focus" : "counterpart";
    return geometry ? [new Feature({ geometry, role, side, objectId: focus.change.objectId })] : [];
  });

/** Connectors of moved points and, when given, the focused pair; features carry `role`, `objectId` and `side`. */
export const buildDecorations = (
  changes: LocatedChange[],
  focus: DiffMapFocus | null,
  visibleIds?: ReadonlySet<string>,
): Feature[] => {
  const isVisible = (objectId: string) => !visibleIds || visibleIds.has(objectId);
  const connectors = changes.flatMap(change => (isVisible(change.objectId) ? (buildConnector(change) ?? []) : []));
  const pair = focus && isVisible(focus.change.objectId) ? buildFocusFeatures(focus) : [];
  return [...connectors, ...pair];
};
