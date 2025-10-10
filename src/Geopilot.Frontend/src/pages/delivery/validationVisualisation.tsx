import { useEffect, useRef } from "react";
import "ol/ol.css";
import Map from "ol/Map";
import View from "ol/View";
import { Tile as TileLayer } from "ol/layer";
import VectorLayer from "ol/layer/Vector";
import TileWMS from "ol/source/TileWMS";
import VectorSource from "ol/source/Vector";
import GeoJSON from "ol/format/GeoJSON";
import Style from "ol/style/Style";
import Stroke from "ol/style/Stroke";
import Fill from "ol/style/Fill";
import CircleStyle from "ol/style/Circle";
import { ValidationErrorFeatures } from "./validationErrorData";
import Cluster from "ol/source/Cluster";
import Text from "ol/style/Text";
import { boundingExtent } from "ol/extent";
import Overlay from "ol/Overlay";

export const ValidationVisualisation = () => {
  const mapRef = useRef<HTMLDivElement | null>(null);
  const mapInstanceRef = useRef<Map | null>(null);
  const tooltipRef = useRef<HTMLDivElement | null>(null);
  const overlayRef = useRef<Overlay | null>(null);

  useEffect(() => {
    if (!mapRef.current || mapInstanceRef.current) return;

    // Tooltip element
    const tooltipEl = document.createElement("div");
    tooltipEl.style.cssText = `
      position: relative;
      background: rgba(0,0,0,0.75);
      color: #fff;
      padding: 4px 8px;
      border-radius: 4px;
      white-space: nowrap;
      font-size: 12px;
      pointer-events: none;
      transform: translateY(-12px);
    `;
    tooltipRef.current = tooltipEl;

    const overlay = new Overlay({
      element: tooltipEl,
      offset: [0, -12],
      positioning: "bottom-center",
      stopEvent: false,
    });
    overlayRef.current = overlay;

    // --- Styles ---
    const pointCircle = new CircleStyle({
      radius: 5,
      fill: new Fill({ color: "rgba(255, 255, 0, 1)" }),
      stroke: new Stroke({ color: "red", width: 1 }),
    });

    const styles: Record<string, Style> = {
      Point: new Style({ image: pointCircle }),
      MultiPoint: new Style({ image: pointCircle }),
      LineString: new Style({ stroke: new Stroke({ color: "green", width: 1 }) }),
      MultiLineString: new Style({ stroke: new Stroke({ color: "green", width: 1 }) }),
      Polygon: new Style({
        stroke: new Stroke({ color: "blue", lineDash: [4], width: 3 }),
        fill: new Fill({ color: "rgba(0, 0, 255, 0.1)" }),
      }),
      MultiPolygon: new Style({
        stroke: new Stroke({ color: "yellow", width: 1 }),
        fill: new Fill({ color: "rgba(255, 255, 0, 0.1)" }),
      }),
      GeometryCollection: new Style({
        stroke: new Stroke({ color: "magenta", width: 2 }),
        fill: new Fill({ color: "magenta" }),
        image: new CircleStyle({
          radius: 10,
          fill: undefined,
          stroke: new Stroke({ color: "magenta" }),
        }),
      }),
      Circle: new Style({
        stroke: new Stroke({ color: "red", width: 2 }),
        fill: new Fill({ color: "rgba(255,0,0,1)" }),
      }),
    };

    const styleFunction = (feature: any) => styles[feature.getGeometry().getType()];

    const vectorSource = new VectorSource({
      features: new GeoJSON().readFeatures(ValidationErrorFeatures),
    });

    const clusterSource = new Cluster({
      distance: 20,
      minDistance: 20,
      source: vectorSource,
    });

    const clusterStyleCache: Record<number, Style> = {};
    const clusterLayer = new VectorLayer({
      source: clusterSource,
      style: (feature: any) => {
        const size = feature.get("features")?.length || 1;
        if (size === 1) {
          return new Style({
            image: new CircleStyle({
              radius: 7,
              fill: new Fill({ color: "#ffcc33" }),
              stroke: new Stroke({ color: "#444", width: 1 }),
            }),
          });
        }
        let style = clusterStyleCache[size];
        if (!style) {
          style = new Style({
            image: new CircleStyle({
              radius: 12,
              stroke: new Stroke({ color: "#fff", width: 2 }),
              fill: new Fill({ color: "#3399CC" }),
            }),
            text: new Text({
              text: String(size),
              fill: new Fill({ color: "#fff" }),
            }),
          });
          clusterStyleCache[size] = style;
        }
        return style;
      },
    });

    const baseLayer = new TileLayer({
      opacity: 1,
      source: new TileWMS({
        url: "https://geodienste.ch/db/av_0/deu",
        params: { LAYERS: "Aggregierte_Amtliche_Vermessung", TILED: true },
        serverType: "geoserver",
        crossOrigin: "anonymous",
      }),
    });

    const map = new Map({
      target: mapRef.current,
      layers: [baseLayer, clusterLayer],
      view: new View({
        center: [808075.5185416606, 5971312.53417889],
        zoom: 12,
      }),
      overlays: [overlay],
    });

    if (!vectorSource.isEmpty()) {
      map.getView().fit(vectorSource.getExtent(), { padding: [40, 40, 40, 40], maxZoom: 16 });
    }

    // Click to zoom into cluster
    map.on("click", evt => {
      (clusterLayer as any).getFeatures(evt.pixel).then((clicked: any[]) => {
        if (!clicked.length) return;
        const clusterFeat = clicked[0];
        const members = clusterFeat.get("features");
        if (members && members.length > 1) {
          const extent = boundingExtent(members.map((f: any) => f.getGeometry().getCoordinates()));
          map.getView().fit(extent, { duration: 700, padding: [50, 50, 50, 50], maxZoom: 18 });
        }
      });
    });

    // Hover tooltip
    map.on("pointermove", evt => {
      if (evt.dragging) return;
      (clusterLayer as any).getFeatures(evt.pixel).then((hits: any[]) => {
        if (!hits.length) {
          tooltipEl.style.display = "none";
          map.getTargetElement().style.cursor = "";
          return;
        }
        const clusterFeature = hits[0];
        const members = clusterFeature.get("features");
        let text: string | undefined;
        if (members?.length === 1) {
          // Show the single feature's message property
          text = members[0].get("Message") ?? "(no message)";
        }
        if (!text) {
          tooltipEl.style.display = "none";
          return;
        }
        tooltipEl.textContent = text;
        overlay.setPosition(evt.coordinate);
        tooltipEl.style.display = "block";
        map.getTargetElement().style.cursor = "pointer";
      });
    });

    mapInstanceRef.current = map;

    return () => {
      map.setTarget(undefined);
      mapInstanceRef.current = null;
    };
  }, []);

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
      <div
        ref={mapRef}
        style={{
          width: "100%",
          height: 400,
          border: "1px solid #ccc",
          borderRadius: 4,
          overflow: "hidden",
        }}
        data-cy="validation-map"
      />
    </div>
  );
};
