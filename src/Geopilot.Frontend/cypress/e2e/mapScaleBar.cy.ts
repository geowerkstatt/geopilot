import { getScaleBar } from "../../src/components/visualizations/map/scaleBar";

describe("Map scale bar", () => {
  it("takes the shortest length of 1, 2 or 5 times a power of ten that is at least 64 pixels wide", () => {
    expect(getScaleBar(1)).to.deep.equal({ label: "100 m", width: 100 });
    expect(getScaleBar(2)).to.deep.equal({ label: "200 m", width: 100 });
    expect(getScaleBar(3)).to.deep.equal({ label: "200 m", width: 67 });
  });

  it("measures in kilometres from 1000 m and in millimetres below 1 m", () => {
    expect(getScaleBar(20)).to.deep.equal({ label: "2 km", width: 100 });
    expect(getScaleBar(0.005)).to.deep.equal({ label: "500 mm", width: 100 });
  });

  it("has no bar without a usable resolution", () => {
    expect(getScaleBar(Number.NaN)).to.equal(undefined);
    expect(getScaleBar(0)).to.equal(undefined);
    expect(getScaleBar(Number.POSITIVE_INFINITY)).to.equal(undefined);
  });
});
