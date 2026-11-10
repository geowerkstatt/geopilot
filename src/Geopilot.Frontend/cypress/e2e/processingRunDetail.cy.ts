import { loginAsAdmin } from "./helpers/appHelpers";

const jobId = "6f7cdfa3-c20a-44bc-bc6c-8771bfadcc8a";

// The seeded database holds no runs, so the run is answered from a fixture.
describe("Processing run", () => {
  beforeEach(() => {
    cy.intercept({ method: "GET", pathname: `/api/v1/pipelinerun/${jobId}` }, { fixture: "processingRun.json" });
    loginAsAdmin();
  });

  it("shows the pipeline the run was configured with", () => {
    cy.intercept(
      { method: "GET", pathname: `/api/v1/pipelinerun/${jobId}/definition` },
      {
        pipelines: [
          {
            id: "ili_validation",
            display_name: { de: "INTERLIS Validierung" },
            steps: [
              {
                id: "validation",
                display_name: { de: "XTF Validierung" },
                process_id: "xtf_validator",
                input: { xtfFiles: "${upload()}" },
                output_actions: [{ property: "XtfLog", actions: ["Download"] }],
              },
            ],
          },
        ],
        processes: [
          {
            id: "xtf_validator",
            implementation: "Geopilot.Pipeline.Processes.XtfValidation.XtfValidatorProcess",
            default_config: { validationProfile: "DEFAULT" },
          },
        ],
      },
    );

    cy.visit(`/admin/processing-runs/${jobId}#pipeline`);

    cy.dataCy("pipelineSnapshotStep-validation").should(
      "contain",
      "Geopilot.Pipeline.Processes.XtfValidation.XtfValidatorProcess",
    );
  });

  it("says that the pipeline could not be loaded instead of waiting on", () => {
    cy.intercept(
      { method: "GET", pathname: `/api/v1/pipelinerun/${jobId}/definition` },
      { statusCode: 500, body: { title: "An error occurred while processing your request.", status: 500 } },
    ).as("definition");

    cy.visit(`/admin/processing-runs/${jobId}#pipeline`);
    cy.wait("@definition");

    cy.dataCy("pipelineSnapshotNotLoaded").should("be.visible");
    cy.get('[role="tabpanel"]').find('[role="progressbar"]').should("not.exist");
  });
});
