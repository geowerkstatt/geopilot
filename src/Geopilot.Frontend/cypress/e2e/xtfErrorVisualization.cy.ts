import { deliverableMandate, processingJob, processingStep, runMockedProcessingJob } from "./helpers/deliveryHelpers";
import { shouldBeFullyShown } from "./helpers/visibilityHelpers";

const VISUALIZATION_URL = "/api/v2/processing/e2e-error-job/visualizations/errors.json";
const BLOB_URL = "/e2e-error-upload";

/** Forty located errors in four classes, so the expanded tree is far taller than the map. */
const visualization = () => {
  const errors = Array.from({ length: 40 }, (_, index) => ({ id: `e${index}`, className: `Class${index % 4}` }));
  return {
    type: "xtfError",
    data: {
      map: {
        layers: [
          {
            features: errors.map((error, index) => ({
              id: error.id,
              geom: `POINT(${2645100 + index} ${1249800 + index})`,
              info: `Model.Topic.${error.className}`,
            })),
          },
        ],
      },
      tree: {
        items: errors.map((error, index) => ({
          id: error.id,
          severity: "error",
          tid: `tid-${index}`,
          model: "Model",
          topic: "Topic",
          class: error.className,
          message: `Error ${index}`,
        })),
        groupBy: ["model", "topic", "class"],
      },
    },
  };
};

const openErrorVisualization = () => {
  cy.intercept("GET", VISUALIZATION_URL, { statusCode: 200, body: visualization() });
  // The API allows 10 uploads a minute; the upload is mocked like the job.
  cy.intercept("POST", "/api/v2/upload", {
    statusCode: 201,
    body: {
      uploadId: "e2e-error-upload",
      files: [{ fileName: "ilimodels_valid.xtf", uploadUrl: BLOB_URL }],
      expiresAt: "2099-01-01T00:00:00Z",
    },
  });
  cy.intercept("PUT", BLOB_URL, { statusCode: 201 });
  runMockedProcessingJob(
    processingJob("e2e-error-job", "success", [
      {
        ...processingStep("error_visualization", "Error visualization", "success"),
        visualizations: [{ originalFileName: "errors.json", url: VISUALIZATION_URL }],
      },
    ]),
    [deliverableMandate(1, "Test Mandate")],
  );
  cy.dataCy("processing-step-error_visualization")
    .find(".MuiAccordionSummary-root")
    .then(summary => {
      if (summary.attr("aria-expanded") !== "true") cy.wrap(summary).click();
    });
  // Once unfolded, the page centres the step; scrolling of a test must not race with that.
  cy.dataCy("processing-step-error_visualization").find(".MuiCollapse-entered").should("exist");
  cy.get(".ol-viewport").should("exist");
};

describe("XTF error visualization", () => {
  it("scrolls the whole map into view from a map button in the tree", () => {
    cy.viewport(1000, 1000);
    openErrorVisualization();
    cy.dataCy("expandAll-button").click();
    // Scrolls again on each retry, as the tree is still unfolding at first.
    cy.dataCy("zoomToError-button")
      .last()
      .should(button => {
        button[0].scrollIntoView();
        expect(Cypress.$(".ol-viewport")[0].getBoundingClientRect().bottom).to.be.below(0);
      });
    cy.dataCy("zoomToError-button").last().click({ force: true });
    shouldBeFullyShown(cy.get(".ol-viewport"));
  });
});
