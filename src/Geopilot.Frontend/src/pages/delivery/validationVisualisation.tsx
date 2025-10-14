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
      padding: 6px 10px;
      border-radius: 4px;
      white-space: pre-line;
      font-size: 12px;
      pointer-events: none;
      transform: translateY(-12px);
      max-width: fit-content;
    `;
    tooltipRef.current = tooltipEl;

    const overlay = new Overlay({
      element: tooltipEl,
      offset: [0, -12],
      positioning: "bottom-center",
      stopEvent: false,
    });
    overlayRef.current = overlay;

    const vectorSource = new VectorSource({
      features: new GeoJSON().readFeatures(ValidationErrorFeatures),
    });

    const clusterSource = new Cluster({
      distance: 20,
      minDistance: 20,
      source: vectorSource,
    });

    const errorStyle = new Style({
      image: new CircleStyle({
        radius: 7,
        fill: new Fill({ color: "#ff3939ff" }),
        stroke: new Stroke({ color: "#444", width: 1 }),
      }),
    });

    const warningStyle = new Style({
      image: new CircleStyle({
        radius: 7,
        fill: new Fill({ color: "#ffcc33" }),
        stroke: new Stroke({ color: "#444", width: 1 }),
      }),
    });

    const clusterStyleCache: Record<number, Style> = {};
    const clusterLayer = new VectorLayer({
      source: clusterSource,
      style: (feature: any) => {
        const features = feature.get("features");
        const size = features?.length || 1;
        if (size === 1) {
          const featureType = features[0].get("Type");
          if (featureType === "Error") return errorStyle;
          if (featureType === "Warning") return warningStyle;
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
        center: [810681.9625898949, 5972942.988941241],
        zoom: 14.58,
      }),
      overlays: [overlay],
    });

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
          // Show the single feature's message, tid, and objtag properties
          const feature = members[0];
          const message = feature.get("Message") ?? "(no message)";
          const tid = feature.get("Tid") ?? "(no TID)";
          const objTag = feature.get("ObjTag") ?? "(no obj tag)";
          text = `${message}\nTID: ${tid}\nType: ${objTag}`;
        } else if (members?.length > 1) {
          text = `${members.length} erreurs`;
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
        }}
        data-cy="validation-map"
      />
    </div>
  );
};
