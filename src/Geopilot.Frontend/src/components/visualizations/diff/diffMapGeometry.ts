import { Extent } from "ol/extent";
import Feature from "ol/Feature";
import WKT from "ol/format/WKT";
import Geometry from "ol/geom/Geometry";
import GeometryCollection from "ol/geom/GeometryCollection";
import LineString from "ol/geom/LineString";
import Point from "ol/geom/Point";
import SimpleGeometry from "ol/geom/SimpleGeometry";
import { isFittableExtent } from "../map/picking";

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

/** How a geometry is drawn: the shape carries the change type, so the map stays readable without colour. */
export type DiffLook = "crisp" | "dotted" | "ghost" | "dashed";

export const getLook = (changeType: ChangeType, state: MapState): DiffLook => {
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

const locationOf = (change: LocatedChange, state: MapState) =>
  readLocation(state === "old" ? change.oldLocation : change.newLocation);

export const getParts = (geometry: Geometry): Geometry[] =>
  geometry instanceof GeometryCollection ? geometry.getGeometriesArray() : [geometry];

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

export const getChangeExtent = (change: LocatedChange, side: MapState): Extent | undefined => {
  const extent = locationOf(change, side)?.getExtent();
  return extent && isFittableExtent(extent) ? extent : undefined;
};

/** A jump to the change in that state, or none without a location there to jump to. */
export const getFocus = (change: LocatedChange, side: MapState): DiffMapFocus | null =>
  getChangeExtent(change, side) ? { change, side } : null;

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

/** Lines from the old to the new position of each moved point; features carry `role` and `objectId`. */
export const buildConnectors = (changes: LocatedChange[]): Feature[] =>
  changes.flatMap(change => buildConnector(change) ?? []);

/** The connectors and, when given, the focused pair of the objects the filter shows; features carry `role`, `objectId` and `side`. */
export const buildDecorations = (
  connectors: Feature[],
  focus: DiffMapFocus | null,
  visibleIds?: ReadonlySet<string>,
): Feature[] => {
  const isVisible = (objectId: string) => !visibleIds || visibleIds.has(objectId);
  const shownConnectors = connectors.filter(connector => isVisible(connector.get("objectId")));
  const pair = focus && isVisible(focus.change.objectId) ? buildFocusFeatures(focus) : [];
  return [...shownConnectors, ...pair];
};
