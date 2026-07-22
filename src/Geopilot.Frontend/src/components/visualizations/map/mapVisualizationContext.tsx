import { createContext, useContext } from "react";
import OlMap from "ol/Map";
import { FitOptions } from "ol/View";

/**
 * Access to the OpenLayers map owned by the MapVisualizationProvider. The provider builds and holds the
 * map so it survives the view component unmounting and remounting (e.g. toggling fullscreen).
 */
export interface MapVisualizationContextInterface {
  /** The OpenLayers map, or null while it is still being built or when there is no map to show. */
  map: OlMap | null;
  /** Fit the view to the combined extent of all feature layers, falling back to the whole country. */
  zoomToExtent: () => void;
  /** Animate the zoom level by the given delta (positive zooms in). */
  zoomBy: (delta: number) => void;
  /** Set options for fitting the view. */
  setFitOptions: (options: FitOptions) => void;
}

export const MapVisualizationContext = createContext<MapVisualizationContextInterface>({
  map: null,
  zoomToExtent: () => {},
  zoomBy: () => {},
  setFitOptions: () => {},
});

export const useMapVisualization = (): MapVisualizationContextInterface => useContext(MapVisualizationContext);
