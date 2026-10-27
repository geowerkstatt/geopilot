import { asArray } from "ol/color";
import { FeatureLike } from "ol/Feature";
import Geometry from "ol/geom/Geometry";
import GeometryCollection from "ol/geom/GeometryCollection";
import { Circle, Fill, Stroke, Style, Text } from "ol/style";
import { StyleFunction } from "ol/style/Style";
import { getClusterMembers, getPartRank } from "../map/layers";
import {
  ChangeType,
  DecorationRole,
  DiffLook,
  getLook,
  getParts,
  MapState,
  PartTypeIndex,
  resolvePartType,
} from "./diffMapGeometry";

export interface DiffMapColors {
  added: string;
  deleted: string;
  changed: string;
  /** Beneath selected and hovered features, whatever their change type. */
  outline: string;
  /** Around filled points, apart from the base map. */
  rim: string;
  /** Of a cluster, which may hold several change types. */
  cluster: string;
}

export interface DiffStyleInputs {
  objectTypes: ReadonlyMap<string, ChangeType>;
  partTypes: PartTypeIndex;
  /** The filter result; undefined means no filter. */
  visibleIds?: ReadonlySet<string>;
  highlightedIds: ReadonlySet<string>;
  hoveredIds: ReadonlySet<string>;
}

type Emphasis = 0 | 1 | 2;

const LOOK_SIZES: Record<DiffLook, { radius: number; width: number }> = {
  crisp: { radius: 5, width: 2.5 },
  dotted: { radius: 5, width: 3.5 },
  ghost: { radius: 10, width: 10 },
  dashed: { radius: 7, width: 3 },
};

const withAlpha = (color: string, opacity: number): string => {
  const [red, green, blue] = asArray(color);
  return `rgba(${red}, ${green}, ${blue}, ${opacity})`;
};

const getSizes = (look: DiffLook, emphasis: Emphasis) => {
  const grow = emphasis * 1.5;
  return { radius: LOOK_SIZES[look].radius + grow, width: LOOK_SIZES[look].width + grow };
};

const buildLookStyle = (look: DiffLook, color: string, rim: string, emphasis: Emphasis, zIndex: number): Style => {
  const { radius, width } = getSizes(look, emphasis);
  const dot = new Circle({ radius, fill: new Fill({ color }), stroke: new Stroke({ color: rim, width: 1.5 }) });
  switch (look) {
    case "crisp":
      return new Style({
        image: dot,
        stroke: new Stroke({ color, width }),
        fill: new Fill({ color: withAlpha(color, 0.25) }),
        zIndex,
      });
    case "dotted":
      return new Style({
        image: dot,
        // A near-zero dash with round caps draws each dash as a dot.
        stroke: new Stroke({ color, width, lineDash: [0.1, 6], lineCap: "round" }),
        fill: new Fill({ color: withAlpha(color, 0.25) }),
        zIndex,
      });
    case "ghost":
      return new Style({
        image: new Circle({
          radius,
          fill: new Fill({ color: withAlpha(color, 0.12) }),
          stroke: new Stroke({ color: withAlpha(color, 0.75), width: 3 }),
        }),
        stroke: new Stroke({ color: withAlpha(color, 0.4), width, lineCap: "round", lineJoin: "round" }),
        fill: new Fill({ color: withAlpha(color, 0.08) }),
        zIndex,
      });
    case "dashed":
      return new Style({
        image: new Circle({
          radius,
          fill: new Fill({ color: withAlpha(color, 0.15) }),
          stroke: new Stroke({ color, width: 2.5, lineDash: [3, 3] }),
        }),
        stroke: new Stroke({ color, width, lineDash: [8, 6] }),
        fill: new Fill({ color: withAlpha(color, 0.1) }),
        zIndex,
      });
  }
};

/** A dark rim beneath a feature's own style, so emphasis never competes with the change colours. */
const buildUnderlayStyle = (look: DiffLook, outline: string, emphasis: Exclude<Emphasis, 0>, zIndex: number): Style => {
  const { radius, width } = getSizes(look, emphasis);
  const color = withAlpha(outline, emphasis === 2 ? 0.9 : 0.45);
  return new Style({
    image: new Circle({ radius: radius + 3, fill: new Fill({ color }) }),
    stroke: new Stroke({ color, width: width + 5, lineCap: "round", lineJoin: "round" }),
    zIndex,
  });
};

/** Builds styles once per look, colour, rank and emphasis; a style function runs for every feature on every frame. */
const createStyleCache = () => {
  const cache = new Map<string, Style[]>();
  return (key: string, build: () => Style[]): Style[] => {
    let styles = cache.get(key);
    if (!styles) {
      styles = build();
      cache.set(key, styles);
    }
    return styles;
  };
};

const getEmphasis = (ids: string[], inputs: DiffStyleInputs): Emphasis => {
  if (ids.some(id => inputs.highlightedIds.has(id))) return 2;
  return ids.some(id => inputs.hoveredIds.has(id)) ? 1 : 0;
};

/** The style of an old or a new layer of the diff: each part of a feature in the look and colour of its change type. */
export const buildDiffStyleFunction = (
  state: MapState,
  inputs: { readonly current: DiffStyleInputs },
  colors: DiffMapColors,
): StyleFunction => {
  const cached = createStyleCache();
  return (feature: FeatureLike) => {
    const current = inputs.current;
    const members = getClusterMembers(feature).filter(member => {
      const id = member.getId()?.toString();
      return !current.visibleIds || (id !== undefined && current.visibleIds.has(id));
    });
    if (members.length === 0) return undefined;
    const ids = members.map(member => member.getId()?.toString() ?? "");
    const emphasis = getEmphasis(ids, current);
    if (members.length > 1)
      return cached(`cluster|${members.length}|${emphasis}`, () => [
        buildClusterStyle(members.length, emphasis, colors),
      ]);

    const [member] = members;
    const geometry = member.getGeometry();
    if (!geometry) return undefined;
    const objectType = current.objectTypes.get(ids[0]) ?? "changed";
    return getParts(geometry).flatMap(part => {
      const changeType = resolvePartType(current.partTypes, ids[0], state, part, objectType);
      const look = getLook(changeType, state);
      const zIndex = getPartRank(part) * 3 + emphasis;
      const styles = cached(`${look}|${changeType}|${zIndex}|${emphasis}`, () => [
        ...(emphasis === 0 ? [] : [buildUnderlayStyle(look, colors.outline, emphasis, zIndex)]),
        buildLookStyle(look, colors[changeType], colors.rim, emphasis, zIndex),
      ]);
      if (!(geometry instanceof GeometryCollection)) return styles;
      return styles.map(style => {
        const partStyle = style.clone();
        partStyle.setGeometry(part);
        return partStyle;
      });
    });
  };
};

const buildClusterStyle = (count: number, emphasis: Emphasis, colors: DiffMapColors) =>
  new Style({
    image: new Circle({
      radius: 12,
      fill: new Fill({ color: colors.cluster }),
      stroke: new Stroke({ color: emphasis > 0 ? colors.outline : colors.rim, width: emphasis > 0 ? 4 : 2 }),
    }),
    text: new Text({ text: count.toString(), font: "bold 12px sans-serif", fill: new Fill({ color: colors.rim }) }),
    zIndex: 6 + emphasis,
  });

/** The style of the decoration layer: connectors of moved points and the outlines of a focused pair. */
export const buildDecorationStyleFunction = (
  inputs: { readonly current: DiffStyleInputs },
  colors: DiffMapColors,
): StyleFunction => {
  const connector = new Style({ stroke: new Stroke({ color: colors.changed, width: 2, lineDash: [2, 4] }) });
  const cached = createStyleCache();
  return (feature: FeatureLike) => {
    const role: DecorationRole = feature.get("role");
    if (role === "connector") return connector;
    const geometry = feature.getGeometry();
    if (!(geometry instanceof Geometry)) return undefined;
    const objectId: string = feature.get("objectId");
    const side: MapState = feature.get("side");
    const current = inputs.current;
    const objectType = current.objectTypes.get(objectId) ?? "changed";
    const look = getLook(resolvePartType(current.partTypes, objectId, side, geometry, objectType), side);
    const emphasis = role === "focus" ? 2 : 1;
    const zIndex = getPartRank(geometry) * 3 + emphasis;
    return cached(`${look}|${zIndex}|${emphasis}`, () => [buildUnderlayStyle(look, colors.outline, emphasis, zIndex)]);
  };
};
