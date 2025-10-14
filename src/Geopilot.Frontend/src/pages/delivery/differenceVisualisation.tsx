import { useEffect, useRef } from "react";
import "ol/ol.css";
import styles from "./differenceVisualisation.module.css";
import Map from "ol/Map";
import View from "ol/View";
import OSM from "ol/source/OSM";
import ol_control_Swipe from "ol-ext/control/Swipe";
import VectorLayer from "ol/layer/Vector";
import VectorSource from "ol/source/Vector";
import GeoJSON from "ol/format/GeoJSON";
import { Extent } from "ol/extent";
import { bbox as bboxStrategy } from "ol/loadingstrategy";
import { Style, Fill, Stroke } from "ol/style";
import Feature from "ol/Feature";
import { Geometry } from "ol/geom";
import TileLayer from "ol/layer/Tile";
import Overlay from "ol/Overlay";
import "ol-ext/control/Swipe.css";

const getFeatureStyle = (isNew: boolean) => (feature: Feature<Geometry>) => {
  console.log(feature);
  const operation = (feature.get("operation") as string)?.toLowerCase() || "";

  if (operation == "deleted (no close geometry)") {
    return new Style({
      fill: new Fill({ color: "rgba(255,0,0,0.9)" }),
      stroke: new Stroke({ color: "#ff0000", width: 2 }),
    });
  }
  if (operation == "added (no close geometry)") {
    return new Style({
      fill: new Fill({ color: "rgba(000,255,000,0.9)" }),
      stroke: new Stroke({ color: "#00ff00", width: 2 }),
    });
  }
  if (operation == "changed (equal geometry)") {
    return new Style({
      fill: new Fill({ color: "rgba(000,000,255,0.9)" }),
      stroke: new Stroke({ color: "#0000ff", width: 2 }),
    });
  }
  if (operation == "changed (close geometry)") {
    return new Style({
      fill: new Fill({ color: isNew ? "rgba(000, 255, 128, 0.7)" : "rgba(255, 000, 128, 0.9)" }),
      stroke: new Stroke({ color: isNew ? "#00FF80" : "#ff0080", width: 2 }),
    });
  }
  // fallback style
  return new Style({
    fill: new Fill({ color: "rgba(128,128,128,0.3)" }),
    stroke: new Stroke({ color: "#888", width: 1 }),
  });
};

export const DifferenceVisualisation = ({ sourceWFS }: { sourceWFS: string }) => {
  const mapRef = useRef<HTMLDivElement>(null);
  const mapObj = useRef<Map | null>(null);

  const OGCFilter = (extent: Extent) =>
    `
    <Filter>
      <And>
        <PropertyIsNotEqualTo>
          <PropertyName>operation</PropertyName>
          <Literal>unchanged</Literal>
        </PropertyIsNotEqualTo>
        <BBOX>
          <PropertyName>the_geom</PropertyName>
          <Box srsName="EPSG:3857">
            <coordinates>${extent.join(",")}</coordinates>
          </Box>
        </BBOX>
      </And>
    </Filter>
    `.trim();

  useEffect(() => {
    if (mapRef.current && !mapObj.current) {
      // Create tooltip element
      const tooltipEl = document.createElement("div");
      tooltipEl.style.cssText = `
        position: relative;
        background: rgba(0,0,0,0.8);
        color: #fff;
        padding: 6px 10px;
        border-radius: 4px;
        white-space: pre-line;
        font-size: 12px;
        pointer-events: none;
        transform: translateY(-12px);
      `;

      const overlay = new Overlay({
        element: tooltipEl,
        offset: [0, -12],
        positioning: "bottom-center",
        stopEvent: false,
      });

      const currentGeometrySource = new VectorSource({
        format: new GeoJSON(),
        url: extent =>
          `${sourceWFS}?service=WFS&` +
          "version=1.1.0&request=GetFeature&typename=diff_sh_ntznng_v5_0geobasisdaten_grundnutzung_zonenflaeche.c_geometrie&" +
          "outputFormat=application/json&srsname=EPSG:3857&" +
          `filter=${encodeURIComponent(OGCFilter(extent))}`,
        strategy: bboxStrategy,
      });

      const nextGeometrySource = new VectorSource({
        format: new GeoJSON(),
        url: extent =>
          `${sourceWFS}?service=WFS&` +
          "version=1.1.0&request=GetFeature&typename=diff_sh_ntznng_v5_0geobasisdaten_grundnutzung_zonenflaeche.n_geometrie&" +
          "outputFormat=application/json&srsname=EPSG:3857&" +
          `filter=${encodeURIComponent(OGCFilter(extent))}`,
        strategy: bboxStrategy,
      });

      const baseTileLayer = new TileLayer({ source: new OSM(), className: styles.ol_bw });
      const beforeLayer = new VectorLayer({
        source: nextGeometrySource,
        style: getFeatureStyle(true),
        className: styles.diff_new,
      });
      const afterLayer = new VectorLayer({
        source: currentGeometrySource,
        style: getFeatureStyle(false),
        className: styles.diff_old,
      });

      const map = new Map({
        target: mapRef.current,
        layers: [baseTileLayer, beforeLayer, afterLayer],
        view: new View({
          center: [963555.2574733495, 6058156.276204036],
          zoom: 13.65,
        }),
        overlays: [overlay],
      });

      const ctrl = new ol_control_Swipe();
      map.addControl(ctrl);
      ctrl.addLayer(beforeLayer, true);
      ctrl.addLayer(afterLayer, false);

      // Tooltip functionality
      map.on("pointermove", evt => {
        if (evt.dragging) return;

        const feature = map.forEachFeatureAtPixel(evt.pixel, feature => feature);

        if (feature) {
          const operation = feature.get("operation") || "unknown operation";
          const id = feature.get("id") || feature.getId() || "no id";
          const text = `${operation}\nTID: ${id}`;

          tooltipEl.textContent = text;
          overlay.setPosition(evt.coordinate);
          tooltipEl.style.display = "block";
          map.getTargetElement().style.cursor = "pointer";
        } else {
          tooltipEl.style.display = "none";
          map.getTargetElement().style.cursor = "";
        }
      });

      mapObj.current = map;
    }

    return () => {
      mapObj.current?.setTarget(undefined);
      mapObj.current = null;
    };
  }, [sourceWFS]);

  return (
    <div
      ref={mapRef}
      style={{ width: "100%", height: 450, borderRadius: 8, border: "1px solid #bdbdbd", backgroundColor: "#fff" }}
    />
  );
};
