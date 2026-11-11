import { ReactionEvent, ReactionKind, ReactionState } from "../../src/api/generated";
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

  it("marks a run that a machine client started", () => {
    cy.visit(`/admin/processing-runs/${jobId}`);

    cy.dataCy("declarer-machineClient")
      .should("contain", "Service account geopilot-api")
      .find("svg title")
      .should("have.text", "Machine client");
  });

  it("offers the reactions while they wait and shows their events once there are", () => {
    // The first attempt starts once the test says so; the page loads the run twice when it opens in development.
    let attempted = false;
    cy.fixture("processingRun.json").then(run => {
      cy.intercept({ method: "GET", pathname: `/api/v1/pipelinerun/${jobId}` }, request =>
        request.reply(
          attempted
            ? {
                ...run,
                forwardingState: ReactionState.Done,
                notificationState: null,
                reactionEvents: [
                  {
                    deliveryId: 23,
                    reactionId: "ablage",
                    reactionKind: ReactionKind.Transfer,
                    implementation: "Geopilot.Pipeline.Channels.FileDrop.FileDropChannel",
                    event: ReactionEvent.Attempt,
                    attempt: 1,
                    occurredAt: "2026-11-06T15:05:00Z",
                    finishedAt: "2026-11-06T15:05:01Z",
                    state: ReactionState.Done,
                    message: "Delivered to the target.",
                  },
                ],
              }
            : { ...run, forwardingState: ReactionState.Pending, notificationState: null, reactionEvents: [] },
        ),
      );
    });

    cy.visit(`/admin/processing-runs/${jobId}#reactions`);
    cy.dataCy("processingRunTab-reactions").should("have.attr", "aria-selected", "true");
    cy.dataCy("reactionEventsNone").should("be.visible");

    // The page loads the run again on its own while a reaction is open.
    cy.then(() => {
      attempted = true;
    });
    cy.contains("td", "Delivered to the target.", { timeout: 20000 }).should("be.visible");
  });

  it("opens another attempt of the delivery on its own page and the same tab", () => {
    const earlierJobId = "3d0e7b52-9a41-4c8e-b6f3-2f5a1c9d7e80";
    cy.fixture("processingRun.json").then(run => {
      cy.intercept({ method: "GET", pathname: `/api/v1/pipelinerun/${earlierJobId}` }, { ...run, ...run.attempts[1] });
    });
    cy.visit(`/admin/processing-runs/${jobId}#files`);
    cy.contains("h3", "Attempts for SMOKE-TEST-01").should("be.visible");
    cy.dataCy(`processingRunAttempt-${jobId}`).should("have.attr", "aria-current", "page");

    // The middle of the row is off the date, so the click shows that the whole row opens the attempt.
    cy.dataCy(`processingRunAttempt-${earlierJobId}`).click();

    cy.location("pathname").should("eq", `/admin/processing-runs/${earlierJobId}`);
    cy.location("hash").should("eq", "#files");
    cy.dataCy(`processingRunAttempt-${earlierJobId}`).should("have.attr", "aria-current", "page");
    cy.contains(`Job ID: ${earlierJobId}`).should("be.visible");
    cy.dataCy("summary-processingState")
      .find('[data-cy="processingState-failed"]')
      .should("contain", "XTF Validierung");
    cy.dataCy("processingRunTab-files").should("have.attr", "aria-selected", "true");
  });

  it("lists no attempts for a run with a reference that is the only attempt of its delivery", () => {
    cy.fixture("processingRun.json").then(run => {
      cy.intercept(
        { method: "GET", pathname: `/api/v1/pipelinerun/${jobId}` },
        { ...run, attempts: [run.attempts[0]] },
      );
    });
    cy.visit(`/admin/processing-runs/${jobId}`);

    cy.dataCy("summary-clientReference").should("contain", "SMOKE-TEST-01");
    cy.dataCy("processingRunAttempts").should("not.exist");
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
