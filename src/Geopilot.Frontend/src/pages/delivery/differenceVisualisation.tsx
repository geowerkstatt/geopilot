import { useEffect, useRef } from "react";
import Map from "ol/Map";
import View from "ol/View";
import TileLayer from "ol/layer/Tile";
import OSM from "ol/source/OSM";
import { fromLonLat } from "ol/proj";
import VectorLayer from "ol/layer/Vector";
import VectorSource from "ol/source/Vector";
import GeoJSON from "ol/format/GeoJSON";
import { bbox as bboxStrategy } from "ol/loadingstrategy";

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

      mapObj.current = new Map({
        target: mapRef.current,
        layers: [
          new TileLayer({ source: new OSM() }),
          new VectorLayer({ source: currentGeometrySource }),
          new VectorLayer({ source: nextGeometrySource }),
        ],
        view: new View({
          center: fromLonLat([8.6, 47.7]),
          zoom: 12,
        }),
      });
    }


    return () => {
      mapObj.current?.setTarget(undefined);
      mapObj.current = null;
    };
  }, [sourceWFS]);

  return <div ref={mapRef} style={{ width: "100%", height: 450, borderRadius: 8, border: "1px solid #bdbdbd" }} />;
};
