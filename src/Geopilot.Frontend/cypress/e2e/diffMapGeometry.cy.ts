import {
  buildDecorations,
  buildPartTypeIndex,
  getChangeExtent,
  getLook,
  getParts,
  inferObjectTypes,
  LocatedChange,
  readLocation,
  resolvePartType,
} from "../../src/components/visualizations/diff/diffMapGeometry";

const geometryChange = (change: Partial<LocatedChange>): LocatedChange => ({
  objectId: "o",
  changeType: "changed",
  valueType: "geometry",
  ...change,
});

const partsOf = (wkt: string) => getParts(readLocation(wkt)!);

describe("diff map geometry", () => {
  it("gives each part the type of its own geometry change", () => {
    const index = buildPartTypeIndex([geometryChange({ changeType: "deleted", oldLocation: "POINT (2 2)" })]);
    const parts = partsOf("GEOMETRYCOLLECTION (POINT (1 1), POINT (2 2))");

    expect(parts).to.have.length(2);
    expect(resolvePartType(index, "o", "old", parts[0], "changed")).to.equal("changed");
    expect(resolvePartType(index, "o", "old", parts[1], "changed")).to.equal("deleted");
    expect(resolvePartType(index, "o", "new", parts[1], "changed")).to.equal("changed");
  });

  it("ignores locations it cannot read", () => {
    const index = buildPartTypeIndex([
      geometryChange({ changeType: "deleted", oldLocation: "NOT WKT" }),
      geometryChange({ changeType: "added" }),
    ]);
    const [part] = partsOf("POINT (1 1)");

    expect(readLocation("NOT WKT")).to.equal(undefined);
    expect(readLocation(undefined)).to.equal(undefined);
    expect(resolvePartType(index, "o", "old", part, "changed")).to.equal("changed");
  });

  it("lets the first change decide a shared location", () => {
    const index = buildPartTypeIndex([
      geometryChange({ changeType: "deleted", oldLocation: "POINT (1 1)" }),
      geometryChange({ changeType: "changed", oldLocation: "POINT (1 1)" }),
    ]);
    const [part] = partsOf("POINT (1 1)");

    expect(resolvePartType(index, "o", "old", part, "changed")).to.equal("deleted");
  });

  it("matches regardless of WKT spacing", () => {
    const index = buildPartTypeIndex([geometryChange({ changeType: "added", newLocation: "POINT(3 4)" })]);
    const [part] = partsOf("POINT (3 4)");

    expect(resolvePartType(index, "o", "new", part, "changed")).to.equal("added");
  });

  it("leaves other value types and objects to the object's type", () => {
    const index = buildPartTypeIndex([
      geometryChange({ valueType: "attribute", changeType: "added", newLocation: "POINT (1 1)" }),
      geometryChange({ objectId: "other", changeType: "added", newLocation: "POINT (1 1)" }),
    ]);
    const [part] = partsOf("POINT (1 1)");

    expect(resolvePartType(index, "o", "new", part, "changed")).to.equal("changed");
  });

  it("infers the type from the layers", () => {
    const types = inferObjectTypes(["a", "c"], ["b", "c"]);

    expect(types.get("a")).to.equal("deleted");
    expect(types.get("b")).to.equal("added");
    expect(types.get("c")).to.equal("changed");
  });

  it("connects moved points only", () => {
    const moved = geometryChange({ oldLocation: "POINT (0 0)", newLocation: "POINT (3 4)" });
    const unmoved = geometryChange({ oldLocation: "POINT (1 1)", newLocation: "POINT (1 1)" });
    const movedLine = geometryChange({ oldLocation: "LINESTRING (0 0, 1 1)", newLocation: "LINESTRING (0 1, 1 2)" });

    const decorations = buildDecorations([moved, unmoved, movedLine], null);

    expect(decorations).to.have.length(1);
    expect(decorations[0].get("role")).to.equal("connector");
    expect(decorations[0].getGeometry()?.getExtent()).to.deep.equal([0, 0, 3, 4]);
  });

  it("drops the decorations of filtered objects", () => {
    const change = geometryChange({ oldLocation: "POINT (0 0)", newLocation: "POINT (0 0)" });
    const focus = { change, side: "old" as const };

    expect(buildDecorations([change], focus, new Set(["other"]))).to.have.length(0);
    expect(buildDecorations([change], focus).map(feature => feature.get("role"))).to.deep.equal([
      "focus",
      "counterpart",
    ]);
  });

  it("zooms to the location of one side", () => {
    const change = geometryChange({ oldLocation: "POINT (1 2)", newLocation: "LINESTRING (0 0, 4 3)" });

    expect(getChangeExtent(change, "old")).to.deep.equal([1, 2, 1, 2]);
    expect(getChangeExtent(change, "new")).to.deep.equal([0, 0, 4, 3]);
    expect(getChangeExtent(geometryChange({ oldLocation: "NOT WKT" }), "old")).to.equal(undefined);
    expect(getChangeExtent(geometryChange({}), "new")).to.equal(undefined);
  });

  it("draws each change type in its own look", () => {
    expect(getLook("added", "new")).to.equal("dotted");
    expect(getLook("deleted", "old")).to.equal("dashed");
    expect(getLook("changed", "old")).to.equal("ghost");
    expect(getLook("changed", "new")).to.equal("crisp");
  });
});
