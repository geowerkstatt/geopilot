import { createContext, MutableRefObject } from "react";
import { Coordinate } from "ol/coordinate";
import OlMap from "ol/Map";

export interface MapViewState {
  center?: Coordinate;
  resolution?: number;
}

export interface MapLayerState {
  visible: boolean;
  opacity: number;
}

export interface MapVisualizationContextInterface {
  viewStateRef: MutableRefObject<MapViewState | undefined>;
  layerStateRef: MutableRefObject<Map<string, MapLayerState>>;
  captureLayerState: (map: OlMap) => void;
  restoreLayerState: (map: OlMap) => void;
  captureViewState: (map: OlMap) => void;
  restoreViewState: (map: OlMap) => void;
}

/**
 * The MapVisualizationContext can be used to persist the view and layer state of the map visualization between
 * unmounts and remounts such as when switching into and out of fullscreen mode.
 */
export const MapVisualizationContext = createContext<MapVisualizationContextInterface>({
  viewStateRef: { current: undefined },
  layerStateRef: { current: new Map() },
  captureLayerState: () => {},
  restoreLayerState: () => {},
  captureViewState: () => {},
  restoreViewState: () => {},
});
