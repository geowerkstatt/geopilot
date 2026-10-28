import { FC, useEffect, useMemo, useRef } from "react";
import { useTheme } from "@mui/material";
import BaseLayer from "ol/layer/Base";
import VectorLayer from "ol/layer/Vector";
import { unByKey } from "ol/Observable";
import VectorSource from "ol/source/Vector";
import { DiffChangeType, DiffSide } from "../../../api/apiInterfaces";
import { MAP_LAYER_STYLE_PROPERTY } from "../map/layers";
import { LayerSwitcherProperties } from "../map/layerSwitcherProps";
import { useMapVisualization } from "../map/mapVisualizationContext";
import { DECORATION_LAYER_PROPERTY } from "../map/picking";
import { buildConnectors, buildDecorations, buildPartTypeIndex, DiffMapFocus, LocatedChange } from "./diffMapGeometry";
import { buildDecorationStyleFunction, buildDiffStyleFunction, DiffMapColors, DiffStyleInputs } from "./diffMapStyles";

const NO_IDS: ReadonlySet<string> = new Set();

const isStateLayer = (layer: BaseLayer, state: DiffSide): layer is VectorLayer<VectorSource> =>
  layer instanceof VectorLayer && layer.get(MAP_LAYER_STYLE_PROPERTY) === state;

/**
 * Connectors and pairs join both states, so they show only while both state layers do, and never while the slider
 * fades between the states.
 */
const showDecorations = (decorations: BaseLayer, stateLayers: BaseLayer[], sliderOn: boolean) =>
  decorations.setVisible(!sliderOn && stateLayers.every(layer => layer.getVisible()));

const getStateLayers = (layers: BaseLayer[]) =>
  layers.filter(layer => isStateLayer(layer, "old") || isStateLayer(layer, "new"));

interface DiffMapLayersProps {
  changes: LocatedChange[];
  objectTypes: ReadonlyMap<string, DiffChangeType>;
  /** The filter result; undefined means no filter. */
  visibleIds?: ReadonlySet<string>;
  /** The selected object, highlighted as a whole unless a pair is focused. */
  highlightedIds: ReadonlySet<string>;
  hoveredIds: ReadonlySet<string>;
  focus: DiffMapFocus | null;
  /** 0 shows only the old state, 1 only the new one; null means the slider is off. */
  slider: number | null;
}

/**
 * Draws the old and the new layer of the map it is rendered in by change type, and adds a decoration layer beneath
 * them with the connectors of moved points and the outlines of a focused pair.
 */
export const DiffMapLayers: FC<DiffMapLayersProps> = ({
  changes,
  objectTypes,
  visibleIds,
  highlightedIds,
  hoveredIds,
  focus,
  slider,
}) => {
  const { map } = useMapVisualization();
  const theme = useTheme();
  const colors = useMemo<DiffMapColors>(
    () => ({
      ...theme.palette.map.diff,
      outline: theme.palette.primary.dark,
      rim: theme.palette.map.stroke,
      cluster: theme.palette.text.secondary,
    }),
    [theme],
  );
  const partTypes = useMemo(() => buildPartTypeIndex(changes), [changes]);
  const connectors = useMemo(() => buildConnectors(changes), [changes]);
  const opacitiesBeforeSlider = useRef<Map<BaseLayer, number> | null>(null);
  const sliderOn = useRef(slider !== null);
  const inputs = useRef<DiffStyleInputs>({ objectTypes, partTypes, visibleIds, highlightedIds, hoveredIds });
  const decorations = useMemo(
    () =>
      new VectorLayer({
        source: new VectorSource(),
        properties: { [DECORATION_LAYER_PROPERTY]: true, [LayerSwitcherProperties.HIDDEN]: true },
      }),
    [],
  );

  useEffect(() => {
    if (!map) return;
    const layers = map.getLayers().getArray();
    for (const state of ["old", "new"] as const) {
      layers
        .filter(layer => isStateLayer(layer, state))
        .forEach(layer => layer.setStyle(buildDiffStyleFunction(state, inputs, colors)));
    }
    decorations.setStyle(buildDecorationStyleFunction(inputs, colors));
    const stateLayers = getStateLayers(layers);
    map.getLayers().insertAt(Math.max(layers.indexOf(stateLayers[0]), 0), decorations);

    const followStates = () => showDecorations(decorations, stateLayers, sliderOn.current);
    followStates();
    const keys = stateLayers.map(layer => layer.on("change:visible", followStates));
    return () => {
      unByKey(keys);
      map.removeLayer(decorations);
    };
  }, [map, decorations, colors]);

  useEffect(() => {
    const shownHighlights = focus ? NO_IDS : highlightedIds;
    inputs.current = { objectTypes, partTypes, visibleIds, highlightedIds: shownHighlights, hoveredIds };
    if (!map) return;
    getStateLayers(map.getLayers().getArray()).forEach(layer => layer.changed());
    decorations.changed();
  }, [map, decorations, objectTypes, partTypes, visibleIds, highlightedIds, hoveredIds, focus]);

  useEffect(() => {
    const source = decorations.getSource();
    source?.clear();
    source?.addFeatures(buildDecorations(connectors, focus, visibleIds));
  }, [decorations, connectors, focus, visibleIds]);

  useEffect(() => {
    sliderOn.current = slider !== null;
    if (!map) return;
    const stateLayers = getStateLayers(map.getLayers().getArray());
    showDecorations(decorations, stateLayers, sliderOn.current);
    if (slider === null) {
      opacitiesBeforeSlider.current?.forEach((opacity, layer) => layer.setOpacity(opacity));
      opacitiesBeforeSlider.current = null;
      return;
    }
    opacitiesBeforeSlider.current ??= new Map(stateLayers.map(layer => [layer, layer.getOpacity()]));
    stateLayers.forEach(layer => layer.setOpacity(isStateLayer(layer, "old") ? 1 - slider : slider));
  }, [map, decorations, slider]);

  return null;
};
