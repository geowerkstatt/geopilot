import { asArray } from "ol/color";
import Feature, { FeatureLike } from "ol/Feature";
import Geometry from "ol/geom/Geometry";
import GeometryCollection from "ol/geom/GeometryCollection";
import { DEVICE_PIXEL_RATIO } from "ol/has";
import { Circle, Fill, RegularShape, Stroke, Style } from "ol/style";
import { StyleFunction } from "ol/style/Style";
import { DiffChangeType, DiffState } from "../../../api/apiInterfaces";
import { getRank } from "../map/picking";
import {
  DecorationRole,
  DiffLook,
  getLook,
  getParts,
  hasOwnGeometryChange,
  liesOnSurfaceEdge,
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
}

export interface DiffStyleInputs {
  objectTypes: ReadonlyMap<string, DiffChangeType>;
  partTypes: PartTypeIndex;
  /** The filter result; undefined means no filter. */
  visibleIds?: ReadonlySet<string>;
  highlightedIds: ReadonlySet<string>;
  hoveredIds: ReadonlySet<string>;
}

type Emphasis = 0 | 1 | 2;

const LOOK_SIZES: Record<DiffLook, { radius: number; width: number }> = {
  dotted: { radius: 6, width: 3.5 },
  crisp: { radius: 5, width: 2.5 },
  ghost: { radius: 10, width: 10 },
  crossed: { radius: 7, width: 2 },
};

/**
 * An added line or outline as dots: with round caps, each near-zero dash draws as a dot. The gap grows with the width,
 * so the dots of a hovered or selected line stay apart.
 */
const getAddedDots = (width: number) => [0.1, width + 2.5];

/** The dots on the rim of an added point lie closer than on a line, so the small circle still gets enough of them. */
const ADDED_POINT_DOTS = [0.1, 4];

/** Dash and gap in pixels of a deleted line or outline; dashes stay long enough that a short line does not read as a point. */
const DELETED_DASH = [8, 4];

/** Pixels between the lines of the cross-hatching of a deleted area. */
const HATCH_SPACING = 10;

const withAlpha = (color: string, opacity: number): string => {
  const [red, green, blue] = asArray(color);
  return `rgba(${red}, ${green}, ${blue}, ${opacity})`;
};

/**
 * Pale crossed diagonals on a faint tint, so the base map beneath stays legible. Scaled to the device pixels the map
 * renders in, so they keep their size on any screen.
 */
const createCrossHatching = (color: string): CanvasPattern | string => {
  const tint = withAlpha(color, 0.05);
  const canvas = document.createElement("canvas");
  const size = Math.round(HATCH_SPACING * DEVICE_PIXEL_RATIO);
  canvas.width = canvas.height = size;
  const context = canvas.getContext("2d");
  if (!context) return tint;
  context.fillStyle = tint;
  context.fillRect(0, 0, size, size);
  context.strokeStyle = withAlpha(color, 0.5);
  context.lineWidth = DEVICE_PIXEL_RATIO;
  context.beginPath();
  // Each diagonal is repeated one tile to either side, so its ends cut off at the corners join the neighboring tiles.
  for (const offset of [-size, 0, size]) {
    context.moveTo(offset, 0);
    context.lineTo(offset + size, size);
    context.moveTo(offset, size);
    context.lineTo(offset + size, 0);
  }
  context.stroke();
  return context.createPattern(canvas, "repeat") ?? tint;
};

const blendOverWhite = (color: string, opacity: number): string => {
  const [red, green, blue] = asArray(color).map(channel => Math.round(channel * opacity + 255 * (1 - opacity)));
  return `rgb(${red}, ${green}, ${blue})`;
};

const getSizes = (look: DiffLook, emphasis: Emphasis) => {
  const grow = emphasis * 1.5;
  return { radius: LOOK_SIZES[look].radius + grow, width: LOOK_SIZES[look].width + grow };
};

/**
 * The style of one look. `filled` is false for an area of a changed object whose geometry stayed the same: it keeps an
 * invisible fill, so it is still picked from inside.
 */
const buildLookStyle = (
  look: DiffLook,
  color: string,
  rim: string,
  emphasis: Emphasis,
  zIndex: number,
  filled: boolean,
): Style => {
  const { radius, width } = getSizes(look, emphasis);
  switch (look) {
    case "dotted":
      return new Style({
        image: new Circle({
          radius,
          fill: new Fill({ color: withAlpha(color, 0.25) }),
          stroke: new Stroke({ color, width: 3, lineDash: ADDED_POINT_DOTS, lineCap: "round" }),
        }),
        stroke: new Stroke({ color, width, lineDash: getAddedDots(width), lineCap: "round" }),
        fill: new Fill({ color: withAlpha(color, 0.25) }),
        zIndex,
      });
    case "crisp":
      return new Style({
        image: new Circle({ radius, fill: new Fill({ color }), stroke: new Stroke({ color: rim, width: 1.5 }) }),
        stroke: new Stroke({ color, width }),
        fill: new Fill({ color: withAlpha(color, filled ? 0.25 : 0) }),
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
        fill: new Fill({ color: withAlpha(color, filled ? 0.08 : 0) }),
        zIndex,
      });
    case "crossed":
      return new Style({
        image: new RegularShape({
          points: 4,
          radius,
          radius2: 0,
          angle: Math.PI / 4,
          stroke: new Stroke({ color, width: 3, lineCap: "round" }),
        }),
        // Round caps would grow a dash into its gap.
        stroke: new Stroke({ color, width, lineDash: DELETED_DASH, lineCap: "butt" }),
        fill: new Fill({ color: createCrossHatching(color) }),
        zIndex,
      });
  }
};

/**
 * A dark rim beneath a feature's own style, so emphasis never competes with the change colors. Opaque, so the rims of
 * two parts on the same edge, a surface and a line on its boundary, look like one.
 */
const buildUnderlayStyle = (look: DiffLook, outline: string, emphasis: Exclude<Emphasis, 0>, zIndex: number): Style => {
  const { radius, width } = getSizes(look, emphasis);
  const color = blendOverWhite(outline, emphasis === 2 ? 0.9 : 0.45);
  return new Style({
    image: new Circle({ radius: radius + 3, fill: new Fill({ color }) }),
    stroke: new Stroke({ color, width: width + 5, lineCap: "round", lineJoin: "round" }),
    zIndex,
  });
};

/** Builds styles once per look, color, rank and emphasis; a style function runs for every feature on every frame. */
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

const getEmphasis = (id: string, inputs: DiffStyleInputs): Emphasis => {
  if (inputs.highlightedIds.has(id)) return 2;
  return inputs.hoveredIds.has(id) ? 1 : 0;
};

/** The style of an old or a new layer of the diff: each part of a feature in the look and color of its change type. */
export const buildDiffStyleFunction = (
  state: DiffState,
  inputs: { readonly current: DiffStyleInputs },
  colors: DiffMapColors,
): StyleFunction => {
  const cached = createStyleCache();
  // Splitting a feature into parts and matching them to changes is costly; it reruns only when its inputs change.
  const featureStyles = new WeakMap<
    Feature,
    Pick<DiffStyleInputs, "objectTypes" | "partTypes"> & { emphasis: Emphasis; styles: Style[] }
  >();
  return (feature: FeatureLike) => {
    const current = inputs.current;
    if (!(feature instanceof Feature)) return undefined;
    const id = feature.getId()?.toString() ?? "";
    if (current.visibleIds && !current.visibleIds.has(id)) return undefined;
    const emphasis = getEmphasis(id, current);

    const known = featureStyles.get(feature);
    if (
      known?.emphasis === emphasis &&
      known.objectTypes === current.objectTypes &&
      known.partTypes === current.partTypes
    ) {
      return known.styles;
    }
    const geometry = feature.getGeometry();
    if (!geometry) return undefined;
    const objectType = current.objectTypes.get(id) ?? "changed";
    const parts = getParts(geometry).map(part => ({
      part,
      changeType: resolvePartType(current.partTypes, id, state, part, objectType),
    }));
    // A line on the outline of a surface of the same change type is drawn by that outline already. Drawn twice, the
    // dashes of the two strokes fall out of step with the zoom and close into a solid line.
    const drawnParts = parts.filter(
      ({ part, changeType }) =>
        !parts.some(other => other.changeType === changeType && liesOnSurfaceEdge(part, other.part)),
    );
    const styles = drawnParts.flatMap(({ part, changeType }) => {
      const look = getLook(changeType, state);
      const rank = getRank(part);
      const zIndex = rank * 3 + emphasis;
      // Only a changed geometry fills its area. A perimeter covering a whole municipality changes along with the
      // references to it and would otherwise cover the map.
      const filled = changeType !== "changed" || hasOwnGeometryChange(current.partTypes, id, state, part);
      const styles = cached(`${look}|${changeType}|${zIndex}|${emphasis}|${filled}`, () => [
        ...(emphasis === 0 ? [] : [buildUnderlayStyle(look, colors.outline, emphasis, zIndex)]),
        buildLookStyle(look, colors[changeType], colors.rim, emphasis, zIndex, filled),
      ]);
      if (!(geometry instanceof GeometryCollection)) return styles;
      return styles.map(style => {
        const partStyle = style.clone();
        partStyle.setGeometry(part);
        return partStyle;
      });
    });
    featureStyles.set(feature, { objectTypes: current.objectTypes, partTypes: current.partTypes, emphasis, styles });
    return styles;
  };
};

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
    const state: DiffState = feature.get("state");
    const current = inputs.current;
    const objectType = current.objectTypes.get(objectId) ?? "changed";
    const look = getLook(resolvePartType(current.partTypes, objectId, state, geometry, objectType), state);
    const emphasis = role === "focus" ? 2 : 1;
    const zIndex = getRank(geometry) * 3 + emphasis;
    return cached(`${look}|${zIndex}|${emphasis}`, () => [buildUnderlayStyle(look, colors.outline, emphasis, zIndex)]);
  };
};
