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

const openDiffVisualization = (transform: Transform = visualization => visualization) => {
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
  cy.dataCy("diff-object").should("exist");
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
