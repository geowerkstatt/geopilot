import { FC, PropsWithChildren, useCallback, useRef } from "react";
import BaseLayer from "ol/layer/Base";
import LayerGroup from "ol/layer/Group";
import OlMap from "ol/Map";
import { LayerSwitcherProperties } from "./layerSwitcher";
import { MapLayerState, MapViewState, MapVisualizationContext } from "./mapVisualizationContext";

const visitLayers = (layers: BaseLayer[], parentPath: string, visit: (layer: BaseLayer, key: string) => void) => {
  for (const layer of layers) {
    const title = (layer.get(LayerSwitcherProperties.TITLE) as string | undefined) ?? "";
    const key = `${parentPath}/${title}`;
    visit(layer, key);
    if (layer instanceof LayerGroup) {
      visitLayers(layer.getLayers().getArray(), key, visit);
    }
  }
};

export const MapVisualizationProvider: FC<PropsWithChildren> = ({ children }) => {
  const viewStateRef = useRef<MapViewState | undefined>(undefined);
  const layerStateRef = useRef(new Map<string, MapLayerState>());
  const lastZoomTokenRef = useRef<number | undefined>(undefined);

  const captureLayerState = useCallback((map: OlMap) => {
    const layerState = layerStateRef.current;
    visitLayers(map.getLayers().getArray(), "", (layer, key) => {
      layerState.set(key, { visible: layer.getVisible(), opacity: layer.getOpacity() });
    });
  }, []);

  const restoreLayerState = useCallback((map: OlMap) => {
    const layerState = layerStateRef.current;
    if (layerState.size > 0) {
      visitLayers(map.getLayers().getArray(), "", (layer, key) => {
        const saved = layerState.get(key);
        if (saved) {
          layer.setVisible(saved.visible);
          layer.setOpacity(saved.opacity);
        }
      });
    }
  }, []);

  const captureViewState = useCallback((map: OlMap) => {
    const view = map.getView();
    viewStateRef.current = {
      center: view.getCenter(),
      resolution: view.getResolution(),
    };
  }, []);

  const restoreViewState = useCallback((map: OlMap) => {
    const saved = viewStateRef.current;
    if (saved) {
      const view = map.getView();
      if (saved.center) view.setCenter(saved.center);
      if (saved.resolution) view.setResolution(saved.resolution);
    }
  }, []);

  return (
    <MapVisualizationContext.Provider
      value={{
        viewStateRef,
        layerStateRef,
        lastZoomTokenRef,
        captureLayerState,
        restoreLayerState,
        captureViewState,
        restoreViewState,
      }}>
      {children}
    </MapVisualizationContext.Provider>
  );
};
