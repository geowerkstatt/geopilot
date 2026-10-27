import { deliverableMandate, processingJob, processingStep, runMockedProcessingJob } from "./helpers/deliveryHelpers";

const VISUALIZATION_URL = "/api/v2/processing/e2e-diff-job/visualizations/diff.json";
const BLOB_URL = "/e2e-diff-upload";
const ENTRANCE = "3f6c1a2e-8b4d-4e7a-9c1f-2d5e6a7b8c90";
const ADDED_LOCALISATION = "a1b2c3d4-1111-4a2b-8c3d-000000000032";
const PARCEL = "d4e5f6a7-7777-4081-c293-000000000215";
const NOTED_LOCALISATION = "2c3d4e5f-cccc-45d6-b7e8-000000000060";

// Local types: the app's types would pull its theme into the Cypress type check.
interface FixtureChange {
  id: string;
  objectId: string;
  changeType: "added" | "deleted" | "changed";
  valueType: "object" | "reference" | "attribute" | "geometry";
  interlisName: string;
  attributePath?: string;
  oldValue?: string;
  newValue?: string;
}

interface Visualization {
  type: "xtfDiff";
  data: { map?: unknown; table?: { changes: FixtureChange[] } };
}

type Transform = (visualization: Visualization) => Visualization;

const openDiffVisualization = (transform: Transform = visualization => visualization, ready = "diff-object") => {
  cy.fixture<Visualization>("xtfDiffVisualization.json").then(fixture => {
    cy.intercept("GET", VISUALIZATION_URL, { statusCode: 200, body: transform(fixture) });
  });
  // The API allows 10 uploads a minute, fewer than this spec runs tests; the upload is mocked like the job.
  cy.intercept("POST", "/api/v2/upload", {
    statusCode: 201,
    body: {
      uploadId: "e2e-diff-upload",
      files: [{ fileName: "ilimodels_valid.xtf", uploadUrl: BLOB_URL }],
      expiresAt: "2099-01-01T00:00:00Z",
    },
  });
  cy.intercept("PUT", BLOB_URL, { statusCode: 201 });
  const visualizationStep = {
    ...processingStep("diff_visualization", "Change visualization", "success"),
    visualizations: [{ originalFileName: "diff.json", url: VISUALIZATION_URL }],
  };
  runMockedProcessingJob(
    processingJob("e2e-diff-job", "success", [
      processingStep("xtf_matching", "XTF matching", "success"),
      visualizationStep,
    ]),
    [deliverableMandate(1, "Test Mandate")],
  );
  cy.dataCy("processing-step-diff_visualization")
    .find(".MuiAccordionSummary-root")
    .then(summary => {
      if (summary.attr("aria-expanded") !== "true") cy.wrap(summary).click();
    });
  cy.dataCy(ready).should("exist");
};

/** Adds thirty attribute changes to the entrance, so it is taller than a small viewport. */
const withManyChanges: Transform = visualization => {
  const table = visualization.data.table!;
  const template = table.changes.find(change => change.objectId === ENTRANCE)!;
  const extra = Array.from({ length: 30 }, (_, index) => ({
    ...template,
    id: `c${100 + index}`,
    changeType: "changed" as const,
    valueType: "attribute" as const,
    attributePath: `Attr${index}`,
    oldValue: "a",
    newValue: "b",
  }));
  return { ...visualization, data: { ...visualization.data, table: { changes: [...table.changes, ...extra] } } };
};

/** Whether the element, not something laid over it such as the page header, is what shows at that height. */
const isShownAt = (element: HTMLElement, y: number) => {
  const { left, right } = element.getBoundingClientRect();
  const shown = element.ownerDocument.elementFromPoint((left + right) / 2, y);
  return shown !== null && element.contains(shown);
};

const object = (objectId: string) => cy.get(`[data-object-id="${objectId}"]`);
const expand = (objectId: string) => object(objectId).dataCy("diff-object-toggle").click();
const change = (changeId: string) => cy.get(`[data-change-id="${changeId}"]`);
const markedTexts = (marks: JQuery) => [...marks].map(mark => mark.textContent);

describe("XTF diff visualization", () => {
  it("filters the changes by class", () => {
    openDiffVisualization();
    cy.dataCy("treeFilterToggle-button").click();
    cy.dataCy("metadata-filter-class").find("input").type("Grundstueck{downArrow}{enter}");
    cy.dataCy("diff-object").should("have.length", 1).and("contain", "Grundstueck");
  });

  it("shows each object once with class, id and summary", () => {
    openDiffVisualization();
    cy.dataCy("diff-object").should("have.length", 6);
    object(ENTRANCE).within(() => {
      cy.contains("Gebaeudeeingang");
      cy.contains(ENTRANCE);
      cy.dataCy("diff-object-summary").should("have.text", "Hausnummer, Lage, −Hausnummerpos/Pos, Lokalisation");
    });
    object(PARCEL)
      .dataCy("diff-object-summary")
      .should("have.text", "Nummer, −Gesamteflaechenmass, +Entstehung, −Untergang");
    object(ADDED_LOCALISATION).within(() => {
      cy.dataCy("diff-object-summary").should("have.text", "Object added");
      cy.dataCy("diff-object-toggle").should("not.exist");
    });
  });

  it("keeps the summary on one line", () => {
    openDiffVisualization();
    cy.dataCy("diff-object-summary")
      .first()
      .should("have.css", "text-overflow", "ellipsis")
      .and("have.css", "white-space", "nowrap");
  });

  it("fits a phone, without the summary", () => {
    cy.viewport(390, 844);
    openDiffVisualization();
    object(ENTRANCE).should("contain", ENTRANCE).dataCy("diff-object-summary").should("not.exist");
    cy.document().should(document =>
      expect(document.documentElement.scrollWidth).to.be.at.most(document.documentElement.clientWidth),
    );
  });

  it("toggles an object by clicking its header", () => {
    openDiffVisualization();
    object(ENTRANCE).dataCy("diff-object-summary").click();
    change("c0").should("exist");
    object(ENTRANCE).dataCy("diff-object-summary").click();
    change("c0").should("not.exist");
  });

  it("highlights only the changed part of a value", () => {
    openDiffVisualization();
    expand(ENTRANCE);
    change("c0").within(() => {
      cy.dataCy("diff-value-old").find("mark").should("not.exist");
      cy.dataCy("diff-value-new").find("mark").should("have.text", "a");
    });
    change("c1").within(() => {
      cy.dataCy("diff-value-old")
        .find("mark")
        .should(marks => expect(markedTexts(marks)).to.deep.equal(["3.4", "6.2"]));
      cy.dataCy("diff-value-new")
        .find("mark")
        .should(marks => expect(markedTexts(marks)).to.deep.equal(["4.1", "7.0"]));
    });
    change("c2").dataCy("diff-value-new").should("have.text", "empty");
  });

  it("clamps a very long value and shows it on demand", () => {
    openDiffVisualization();
    expand(NOTED_LOCALISATION);
    change("c12").dataCy("diff-show-more").first().click();
    change("c12").dataCy("diff-show-more").first().should("contain", "Show less");
    change("c13").dataCy("diff-value-old").should("have.text", "not-json");
  });

  it("jumps to a referenced object and focuses it", () => {
    openDiffVisualization();
    expand(ENTRANCE);
    change("c3").dataCy("diff-value-new").dataCy("diff-reference").click();
    object(ADDED_LOCALISATION).should("have.focus");
  });

  it("marks a reference whose target the filter hides", () => {
    openDiffVisualization();
    cy.dataCy("treeFilterToggle-button").click();
    cy.dataCy("metadata-filter-class").find("input").type("Gebaeudeeingang{downArrow}{enter}");
    expand(ENTRANCE);
    change("c3").dataCy("diff-value-new").dataCy("diff-reference").should("not.have.attr", "role", "button");
  });

  it("offers map buttons only for objects on the map and on geometry values", () => {
    openDiffVisualization();
    object(ENTRANCE).within(() => {
      cy.dataCy("diff-object-map").should("exist");
      cy.dataCy("diff-object-toggle").click();
    });
    change("c1").dataCy("diff-geometry-map-old").should("exist");
    change("c1").dataCy("diff-geometry-map-new").should("exist");
    change("c2").dataCy("diff-geometry-map-new").should("not.exist");
    change("c0").find('[data-cy^="diff-geometry-map"]').should("not.exist");
    object(ADDED_LOCALISATION).dataCy("diff-object-map").should("not.exist");
  });

  it("shows no map buttons without a map", () => {
    openDiffVisualization(visualization => ({ ...visualization, data: { table: visualization.data.table } }));
    cy.dataCy("diff-object-map").should("not.exist");
  });

  it("scrolls the whole map into view from a map button", () => {
    cy.viewport(1000, 1000);
    openDiffVisualization(withManyChanges);
    cy.dataCy("expandAll-button").click();
    cy.dataCy("diff-object-map").last().scrollIntoView();
    cy.get(".ol-viewport").should(viewport => expect(viewport[0].getBoundingClientRect().bottom).to.be.below(0));
    cy.dataCy("diff-object-map").last().click();
    cy.get(".ol-viewport").should(viewport => {
      const { top, bottom } = viewport[0].getBoundingClientRect();
      expect(isShownAt(viewport[0], top + 2), "top edge not covered").to.equal(true);
      expect(isShownAt(viewport[0], bottom - 2), "bottom edge not covered").to.equal(true);
    });
  });

  it("shows a revealed object from its start when it is taller than the view", () => {
    cy.viewport(1000, 400);
    openDiffVisualization(visualization => {
      const withChanges = withManyChanges(visualization);
      const template = withChanges.data.table!.changes.find(change => change.objectId === NOTED_LOCALISATION)!;
      const reference = {
        ...template,
        id: "c200",
        changeType: "changed" as const,
        valueType: "reference" as const,
        attributePath: "Gebaeudeeingang",
        oldValue: "[]",
        newValue: JSON.stringify([ENTRANCE]),
      };
      return {
        ...withChanges,
        data: { ...withChanges.data, table: { changes: [...withChanges.data.table!.changes, reference] } },
      };
    });
    expand(ENTRANCE);
    expand(NOTED_LOCALISATION);
    change("c200").dataCy("diff-reference").scrollIntoView().click();
    object(ENTRANCE).should("have.focus");
    object(ENTRANCE).should(entrance => {
      expect(isShownAt(entrance[0], entrance[0].getBoundingClientRect().top + 2), "start not covered").to.equal(true);
    });
  });

  it("labels the before and the after map button apart", () => {
    openDiffVisualization();
    expand(ENTRANCE);
    change("c1")
      .dataCy("diff-geometry-map-old")
      .invoke("attr", "aria-label")
      .then(before => change("c1").dataCy("diff-geometry-map-new").should("not.have.attr", "aria-label", before));
  });

  it("explains the map styles in a legend", () => {
    openDiffVisualization();
    cy.dataCy("diffMapLegend-button").click();
    cy.dataCy("diffMapLegend-button").should("have.attr", "aria-pressed", "true");
    cy.dataCy("diff-map-legend").should("be.visible").find("svg").should("have.length", 9);
    cy.get(".ol-viewport").click("topLeft");
    cy.dataCy("diff-map-legend").should("not.exist");
  });

  it("fades from before to after with the slider", () => {
    openDiffVisualization();
    cy.dataCy("diffMapSlider-button").click();
    const slider = () => cy.dataCy("diff-map-slider").find('input[type="range"]');
    slider().should("have.attr", "aria-valuenow", "0");
    slider().should("have.attr", "aria-valuetext", "0 %");
    slider().focus().type("{rightArrow}");
    slider().should("not.have.attr", "aria-valuenow", "0");
    cy.dataCy("diffMapSlider-button").click();
    cy.dataCy("diff-map-slider").should("not.exist");
  });

  it("offers legend and slider on a map without the list", () => {
    openDiffVisualization(
      visualization => ({ ...visualization, data: { map: visualization.data.map } }),
      "diffMapLegend-button",
    );
    cy.dataCy("diffMapSlider-button").should("exist");
  });

  it("stacks before and after on a phone", () => {
    cy.viewport(390, 844);
    openDiffVisualization();
    expand(ENTRANCE);
    change("c0").within(() => {
      cy.dataCy("diff-value-old").then(old =>
        cy.dataCy("diff-value-new").should(next => {
          expect(next[0].getBoundingClientRect().top).to.be.greaterThan(old[0].getBoundingClientRect().bottom - 1);
        }),
      );
    });
  });
});
