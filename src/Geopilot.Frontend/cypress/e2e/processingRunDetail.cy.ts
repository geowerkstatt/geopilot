import {
  InputKind,
  InputOrigin,
  PipelineRunOutcomeKind,
  PipelineRunStepResponse,
  ProcessingState,
  ReactionEvent,
  ReactionKind,
  ReactionState,
  StepState,
} from "../../src/api/generated";
import { loginAsAdmin } from "./helpers/appHelpers";

const jobId = "6f7cdfa3-c20a-44bc-bc6c-8771bfadcc8a";

// The first attempt of the transfer of the delivery, as the protocol holds it once it went through.
const transferAttempt = {
  deliveryId: 23,
  reactionId: "ablage",
  reactionKind: ReactionKind.Transfer,
  implementation: "Geopilot.Pipeline.Channels.FileDrop.FileDropChannel",
  event: ReactionEvent.Attempt,
  attempt: 1,
  occurredAt: "2026-11-06T15:05:00Z",
  finishedAt: "2026-11-06T15:05:01Z",
  state: ReactionState.Done,
  message: { en: "Delivered to the target." },
};

// Moves the clock of the page on by the time between two loads until the check passes. The page sets its timer a
// moment after an answer arrived, so a single tick could come before it.
const tickUntil = (check: () => void) =>
  cy.clock().then(clock =>
    cy.wrap(null).should(() => {
      clock.tick(15_000);
      check();
    }),
  );

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
                reactionEvents: [transferAttempt],
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

  it("keeps loading the run again after one of the loads failed", () => {
    // The answer follows the test: the transfer waits, then loading fails, then the first attempt went through.
    let answer: "waiting" | "failing" | "attempted" = "waiting";
    const answers = { waiting: 0, failing: 0, attempted: 0 };
    // The first answer waits for the test, so that the page sets its timer on the clock the test moves.
    let requested = false;
    let answerFirst: () => void = () => undefined;
    const firstAnswerDue = new Promise<void>(resolve => {
      answerFirst = resolve;
    });
    cy.fixture("processingRun.json").then(run => {
      cy.intercept({ method: "GET", pathname: `/api/v1/pipelinerun/${jobId}` }, async request => {
        requested = true;
        await firstAnswerDue;
        answers[answer]++;
        if (answer === "failing") {
          request.reply({
            statusCode: 500,
            body: { title: "An error occurred while processing your request.", status: 500 },
          });
        } else if (answer === "attempted") {
          request.reply({
            ...run,
            forwardingState: ReactionState.Done,
            notificationState: null,
            reactionEvents: [transferAttempt],
          });
        } else {
          request.reply({
            ...run,
            forwardingState: ReactionState.Pending,
            notificationState: null,
            reactionEvents: [],
          });
        }
      });
    });

    cy.visit(`/admin/processing-runs/${jobId}#reactions`);
    cy.wrap(null).should(() => expect(requested, "run requested").to.equal(true));
    cy.clock(Date.now(), ["setTimeout", "clearTimeout"]);
    cy.then(() => answerFirst());
    cy.dataCy("reactionEventsNone").should("be.visible");

    cy.then(() => {
      answer = "failing";
    });
    tickUntil(() => expect(answers.failing, "loads that failed").to.be.greaterThan(0));
    cy.then(() => {
      answer = "attempted";
    });
    tickUntil(() => expect(answers.attempted, "loads after the failed one").to.be.greaterThan(0));

    cy.contains("td", "Delivered to the target.").should("be.visible");
  });

  it("opens another attempt of the delivery on its own page and the same tab", () => {
    const earlierJobId = "3d0e7b52-9a41-4c8e-b6f3-2f5a1c9d7e80";
    cy.fixture("processingRun.json").then(run => {
      cy.intercept({ method: "GET", pathname: `/api/v1/pipelinerun/${earlierJobId}` }, { ...run, ...run.attempts[1] });
    });
    cy.visit(`/admin/processing-runs/${jobId}#reactions`);
    cy.contains("h3", "Attempts for SMOKE-TEST-01").should("be.visible");
    cy.dataCy(`processingRunAttempt-${jobId}`).should("have.attr", "aria-current", "page");

    // The middle of the row is off the date, so the click shows that the whole row opens the attempt.
    cy.dataCy(`processingRunAttempt-${earlierJobId}`).click();

    cy.location("pathname").should("eq", `/admin/processing-runs/${earlierJobId}`);
    cy.location("hash").should("eq", "#reactions");
    cy.dataCy(`processingRunAttempt-${earlierJobId}`).should("have.attr", "aria-current", "page");
    cy.contains(`Job ID: ${earlierJobId}`).should("be.visible");
    cy.dataCy("summary-processingState")
      .find('[data-cy="processingState-failed"]')
      .should("contain", "XTF Validierung");
    cy.dataCy("processingRunTab-reactions").should("have.attr", "aria-selected", "true");
  });

  it("names the preparation where the preflight failed and says why within it", () => {
    cy.fixture("processingRun.json").then(run => {
      cy.intercept(
        { method: "GET", pathname: `/api/v1/pipelinerun/${jobId}` },
        {
          ...run,
          terminalState: ProcessingState.Failed,
          failureReason: "IncompleteUpload: File 'roads.xtf' is incomplete.",
          outcome: { kind: PipelineRunOutcomeKind.NotDeliverable, blockingSteps: [], preflightFailed: true },
          forwardingState: null,
          notificationState: null,
          steps: run.steps.map((step: PipelineRunStepResponse) => ({ ...step, state: StepState.Pending })),
        },
      );
    });
    cy.visit(`/admin/processing-runs/${jobId}`);

    cy.dataCy("summary-processingState").find('[data-cy="processingState-failed"]').should("contain", "Preparation");
    cy.dataCy("processingRunStep-preflight")
      .should("contain", "roads.xtf")
      .and("contain", "IncompleteUpload: File 'roads.xtf' is incomplete.");
    cy.dataCy("processingRunFailureReason").should("not.exist");
  });

  it("says above the steps why the run ended outside them", () => {
    cy.fixture("processingRun.json").then(run => {
      cy.intercept(
        { method: "GET", pathname: `/api/v1/pipelinerun/${jobId}` },
        {
          ...run,
          terminalState: ProcessingState.Cancelled,
          failureReason: "Job timed out after 00:30:00.",
          outcome: {
            kind: PipelineRunOutcomeKind.NotDeliverable,
            blockingSteps: [{ stepId: "validation", displayName: { de: "XTF Validierung" } }],
            preflightFailed: false,
          },
          forwardingState: null,
          notificationState: null,
          steps: run.steps.map((step: PipelineRunStepResponse) => ({
            ...step,
            state: StepState.Cancelled,
            errorMessage: "A task was canceled.",
          })),
        },
      );
    });
    cy.visit(`/admin/processing-runs/${jobId}`);

    cy.dataCy("processingRunFailureReason").should("contain", "Job timed out after 00:30:00.");
    cy.dataCy("processingRunStep-preflight").should("not.contain", "Job timed out");
    cy.dataCy("processingRunStep-validation").should("not.contain", "A task was canceled.");
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

  describe("with files an earlier step passed on", () => {
    // The upload delivered roads.xtf and a matcher passed it on; a step of the run made a roads.xtf of its own.
    beforeEach(() => {
      cy.fixture("processingRun.json").then(run => {
        cy.intercept(
          { method: "GET", pathname: `/api/v1/pipelinerun/${jobId}` },
          {
            ...run,
            steps: run.steps.map((step: PipelineRunStepResponse) => ({
              ...step,
              inputs: [
                {
                  parameter: "transferFile",
                  origin: InputOrigin.StepOutput,
                  reference: "xtf_matching.XtfFiles",
                  kind: InputKind.File,
                  fileName: "roads.xtf",
                  size: 20480,
                  fromUpload: true,
                },
                {
                  parameter: "referenceDataFiles",
                  origin: InputOrigin.StepOutput,
                  reference: "unzip.Files",
                  kind: InputKind.File,
                  fileName: "roads.xtf",
                  size: 1024,
                  fromUpload: false,
                },
              ],
            })),
          },
        );
      });
      cy.visit(`/admin/processing-runs/${jobId}`);
    });

    it("shows the hash of the preparation for a delivered file", () => {
      cy.dataCy("processingRunStep-validation")
        .contains("tr", "transferFile")
        .should("contain", "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08");
    });

    it("says that a file made during the processing has no hash, also under the name of a delivered one", () => {
      cy.dataCy("processingRunStep-validation")
        .contains("tr", "referenceDataFiles")
        .should("contain", "created during processing")
        .and("not.contain", "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08")
        .and("not.contain", "not computed");
    });
  });
});
