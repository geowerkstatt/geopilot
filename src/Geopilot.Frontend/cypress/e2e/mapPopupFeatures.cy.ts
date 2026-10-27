import Feature from "ol/Feature";
import Point from "ol/geom/Point";
import Polygon from "ol/geom/Polygon";
import { getPopupFeatures } from "../../src/components/visualizations/map/featureRank";

const point = (id: string) => {
  const feature = new Feature(new Point([5, 5]));
  feature.setId(id);
  return feature;
};

const square = (id: string, size: number) => {
  const feature = new Feature(
    new Polygon([
      [
        [0, 0],
        [size, 0],
        [size, size],
        [0, size],
        [0, 0],
      ],
    ]),
  );
  feature.setId(id);
  return feature;
};

const ids = (features: Feature[]) => features.map(feature => feature.getId());

describe("map popup features", () => {
  it("offers only the features of the clicked kind", () => {
    expect(ids(getPopupFeatures([point("p"), square("area", 100)], 1))).to.deep.equal(["p"]);
  });

  it("offers overlapping features of the clicked kind", () => {
    expect(ids(getPopupFeatures([point("p1"), point("p2")], 1))).to.deep.equal(["p1", "p2"]);
    expect(ids(getPopupFeatures([square("small", 10), square("large", 100)], 1))).to.deep.equal(["small", "large"]);
  });

  it("offers a feature that disappears under the marker", () => {
    expect(ids(getPopupFeatures([point("p"), square("area", 10)], 1))).to.deep.equal(["p", "area"]);
    expect(ids(getPopupFeatures([point("p"), square("area", 10)], 0.1))).to.deep.equal(["p"]);
  });

  it("offers nothing without a feature", () => {
    expect(getPopupFeatures([], 1)).to.deep.equal([]);
  });
});
