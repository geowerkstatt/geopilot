import { createEmpty } from "ol/extent";
import Feature from "ol/Feature";
import Point from "ol/geom/Point";
import Polygon from "ol/geom/Polygon";
import VectorLayer from "ol/layer/Vector";
import VectorSource from "ol/source/Vector";
import {
  DECORATION_LAYER_PROPERTY,
  getPopupFeatures,
  isFittableExtent,
  isPickable,
} from "../../src/components/visualizations/map/picking";

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

  it("skips decorations and layers drawn invisible", () => {
    expect(isPickable(new VectorLayer({ source: new VectorSource() }))).to.equal(true);
    const decoration = new VectorLayer({
      source: new VectorSource(),
      properties: { [DECORATION_LAYER_PROPERTY]: true },
    });
    expect(isPickable(decoration)).to.equal(false);
    expect(isPickable(new VectorLayer({ source: new VectorSource(), opacity: 0 }))).to.equal(false);
  });

  it("fits only an extent with a position", () => {
    expect(isFittableExtent([0, 0, 1, 1])).to.equal(true);
    expect(isFittableExtent([1, 2, 1, 2])).to.equal(true);
    expect(isFittableExtent(createEmpty())).to.equal(false);
    expect(isFittableExtent([NaN, NaN, NaN, NaN])).to.equal(false);
  });

  it("offers nothing without a feature", () => {
    expect(getPopupFeatures([], 1)).to.deep.equal([]);
  });
});
