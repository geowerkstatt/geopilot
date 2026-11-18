import {
  ConfigOrigin,
  InputKind,
  InputOrigin,
  ProcessingState,
  ReactionEvent,
  ReactionKind,
  ReactionState,
  RecordingOutcomeKind,
  RecordingStepResponse,
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
describe("Recording", () => {
  beforeEach(() => {
    cy.intercept({ method: "GET", pathname: `/api/v1/recording/${jobId}` }, { fixture: "recording.json" });
    loginAsAdmin();
  });

  it("shows the pipeline the run was configured with", () => {
    cy.intercept(
      { method: "GET", pathname: `/api/v1/recording/${jobId}/definition` },
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

    cy.visit(`/admin/recordings/${jobId}#pipeline`);

    cy.dataCy("pipelineSnapshotStep-validation").should(
      "contain",
      "Geopilot.Pipeline.Processes.XtfValidation.XtfValidatorProcess",
    );
  });

  it("marks a run that a machine client started", () => {
    cy.visit(`/admin/recordings/${jobId}`);

    cy.dataCy("userOrClient-machineClient")
      .should("contain", "Service account geopilot-api")
      .find("svg title")
      .should("have.text", "Machine client");
  });

  it("offers the reactions while they wait and shows their events once there are", () => {
    // The first attempt starts once the test says so; the page loads the run twice when it opens in development.
    let attempted = false;
    cy.fixture("recording.json").then(run => {
      cy.intercept({ method: "GET", pathname: `/api/v1/recording/${jobId}` }, request =>
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

    cy.visit(`/admin/recordings/${jobId}#reactions`);
    cy.dataCy("recordingTab-reactions").should("have.attr", "aria-selected", "true");
    cy.dataCy("reactionEventsNone").should("be.visible");

    // The page loads the run again on its own while a reaction is open.
    cy.then(() => {
      attempted = true;
    });
    cy.get('[data-cy="reactionEvent-0"]', { timeout: 20000 }).should("be.visible");
  });

  it("opens an attempt to show what its channel ran with", () => {
    const mailAttempt = {
      ...transferAttempt,
      reactionId: "info",
      reactionKind: ReactionKind.Notification,
      implementation: "Geopilot.Pipeline.Channels.Mail.MailChannel",
      occurredAt: "2026-11-06T15:06:00Z",
      finishedAt: "2026-11-06T15:06:01Z",
      reactionDisplayName: { en: "Mail to the recipients" },
      appVersion: "3.2.17",
      // In the order PostgreSQL returns a jsonb object, by the length of its keys.
      config: {
        to: { value: ["empfang@example.com", "kopie@example.com"], origin: ConfigOrigin.Mandate },
        host: { value: "smtp.example.com", origin: ConfigOrigin.Base },
        password: { value: "**********", origin: ConfigOrigin.Base },
        template: {
          value: "mail/info.html",
          origin: ConfigOrigin.Definition,
          sha256: "3a5f8dba468cf12de017fec68004a5020ec28c25ad671f026beba8b5fb900581",
        },
      },
    };
    // A reset is no attempt: nothing ran, so there is nothing to open.
    const resetEvent = {
      ...transferAttempt,
      event: ReactionEvent.Reset,
      attempt: null,
      occurredAt: "2026-11-06T15:07:00Z",
      finishedAt: null,
      state: ReactionState.Pending,
      message: null,
      userId: 7,
    };
    // A failed attempt with another one to follow leaves its reaction pending.
    const failedAttempt = {
      ...transferAttempt,
      reactionId: "archiv",
      occurredAt: "2026-11-06T15:08:00Z",
      finishedAt: "2026-11-06T15:08:30Z",
      state: ReactionState.Pending,
      message: { en: "The target is not reachable." },
    };
    // After the reset the attempts count from one again.
    const retriedAttempt = {
      ...transferAttempt,
      occurredAt: "2026-11-06T15:07:30Z",
      finishedAt: "2026-11-06T15:07:31Z",
    };
    // An attempt that no longer held its reaction when it reported has no state.
    const lostAttempt = {
      ...transferAttempt,
      reactionId: "kopie",
      occurredAt: "2026-11-06T15:09:00Z",
      finishedAt: "2026-11-06T15:09:07Z",
      state: null,
    };
    cy.fixture("recording.json").then(run => {
      cy.intercept(
        { method: "GET", pathname: `/api/v1/recording/${jobId}` },
        {
          ...run,
          forwardingState: ReactionState.Done,
          reactionEvents: [transferAttempt, mailAttempt, resetEvent, retriedAttempt, failedAttempt, lostAttempt],
        },
      );
    });

    cy.visit(`/admin/recordings/${jobId}#reactions`);

    // The events stand under the tabs like the steps of the run, without a heading of their own, each with its own entry,
    // also an attempt counted from one again after a reset.
    cy.dataCy("summary-reactionEvents").should("not.exist");
    cy.get('[data-cy^="reactionEvent-"]')
      .then(entries => entries.toArray().map(entry => entry.dataset.cy))
      .should(ids => expect(new Set(ids).size, "distinct entries").to.equal(6));

    // The header tells how the event ended, what happened to which reaction, by its name with its id beside, and of what
    // kind, without opening it, also to a screen reader and in copied text, which need the words apart. A reaction the
    // definition no longer has shows its id.
    cy.dataCy("reactionEvent-1").should("contain", "Done after 1 s");
    cy.dataCy("reactionEvent-1")
      .find("[aria-expanded]")
      .invoke("text")
      .should("contain", "attempt 1 Mail to the recipients info notification");
    cy.dataCy("reactionEvent-0")
      .find("[aria-expanded]")
      .invoke("text")
      .should("contain", "attempt 1 ablage forwarding");
    cy.dataCy("reactionEvent-4").should("contain", "Failed after 30 s").and("not.contain", "Pending");
    cy.dataCy("reactionEvent-5").should("contain", "7 s");
    cy.dataCy("reactionEvent-2").should("contain", "retry requested").find("[aria-expanded]").should("not.exist");

    // An attempt recorded before the protocol kept version and configuration opens to its channel and message.
    cy.dataCy("reactionEvent-0").find('[aria-expanded="false"]').click();
    cy.dataCy("reactionEvent-0").within(() => {
      cy.dataCy("summary-channel").should("contain", "FileDropChannel").and("not.contain", "(");
      cy.dataCy("reactionEventConfig").should("not.exist");
      cy.dataCy("summary-message").should("contain", "Delivered to the target.");
    });

    cy.dataCy("reactionEvent-1").find('[aria-expanded="false"]').click();
    cy.dataCy("reactionEvent-1").find('[aria-expanded="true"]').should("exist");
    cy.dataCy("reactionEvent-1")
      .find("[aria-expanded]")
      .invoke("attr", "aria-controls")
      .then(id => cy.get(`[id="${id}"]`).should("have.attr", "role", "region"));
    cy.dataCy("reactionEvent-1").within(() => {
      cy.dataCy("summary-channel")
        .should("contain", "Geopilot.Pipeline.Channels.Mail.MailChannel")
        .and("contain", "geopilot v3.2.17");
      cy.get('[data-cy^="reactionEventConfig-"]')
        .then(rows => rows.toArray().map(row => row.dataset.cy))
        .should("deep.equal", [
          "reactionEventConfig-host",
          "reactionEventConfig-password",
          "reactionEventConfig-template",
          "reactionEventConfig-to",
        ]);
      cy.dataCy("reactionEventConfig-password").should("contain", "**********").and("contain", "installation");
      cy.dataCy("reactionEventConfig-template")
        .should("contain", "mail/info.html")
        .and("contain", "pipeline definition")
        .and("contain", "3a5f8dba468cf12de017fec68004a5020ec28c25ad671f026beba8b5fb900581");
      cy.dataCy("reactionEventConfig-to")
        .should("contain", "empfang@example.com, kopie@example.com")
        .and("contain", "mandate");
      cy.dataCy("summary-message").should("contain", "Delivered to the target.");
    });
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
    cy.fixture("recording.json").then(run => {
      cy.intercept({ method: "GET", pathname: `/api/v1/recording/${jobId}` }, async request => {
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

    cy.visit(`/admin/recordings/${jobId}#reactions`);
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

    cy.dataCy("reactionEvent-0").should("be.visible");
  });

  it("opens another attempt of the delivery on its own page and the same tab", () => {
    const earlierJobId = "3d0e7b52-9a41-4c8e-b6f3-2f5a1c9d7e80";
    cy.fixture("recording.json").then(run => {
      cy.intercept({ method: "GET", pathname: `/api/v1/recording/${earlierJobId}` }, { ...run, ...run.attempts[1] });
    });
    cy.visit(`/admin/recordings/${jobId}#reactions`);
    cy.contains("h3", "Attempts for SMOKE-TEST-01").should("be.visible");
    cy.dataCy(`recordingAttempt-${jobId}`).should("have.attr", "aria-current", "page");

    // The middle of the row is off the date, so the click shows that the whole row opens the attempt.
    cy.dataCy(`recordingAttempt-${earlierJobId}`).click();

    cy.location("pathname").should("eq", `/admin/recordings/${earlierJobId}`);
    cy.location("hash").should("eq", "#reactions");
    cy.dataCy(`recordingAttempt-${earlierJobId}`).should("have.attr", "aria-current", "page");
    cy.contains(`Job ID: ${earlierJobId}`).should("be.visible");
    cy.dataCy("summary-processingState")
      .find('[data-cy="processingState-failed"]')
      .should("contain", "XTF Validierung");
    cy.dataCy("recordingTab-reactions").should("have.attr", "aria-selected", "true");
  });

  it("names the preparation where the preflight failed and says why within it", () => {
    cy.fixture("recording.json").then(run => {
      cy.intercept(
        { method: "GET", pathname: `/api/v1/recording/${jobId}` },
        {
          ...run,
          terminalState: ProcessingState.Failed,
          failureReason: "IncompleteUpload: File 'roads.xtf' is incomplete.",
          outcome: { kind: RecordingOutcomeKind.NotDeliverable, blockingSteps: [], preflightFailed: true },
          forwardingState: null,
          notificationState: null,
          steps: run.steps.map((step: RecordingStepResponse) => ({ ...step, state: StepState.Pending })),
        },
      );
    });
    cy.visit(`/admin/recordings/${jobId}`);

    cy.dataCy("summary-processingState").find('[data-cy="processingState-failed"]').should("contain", "Preparation");
    cy.dataCy("recordingStep-preflight")
      .should("contain", "roads.xtf")
      .and("contain", "IncompleteUpload: File 'roads.xtf' is incomplete.");
    cy.dataCy("recordingFailureReason").should("not.exist");
  });

  it("says above the steps why the run ended outside them", () => {
    cy.fixture("recording.json").then(run => {
      cy.intercept(
        { method: "GET", pathname: `/api/v1/recording/${jobId}` },
        {
          ...run,
          terminalState: ProcessingState.Cancelled,
          failureReason: "Job timed out after 00:30:00.",
          outcome: {
            kind: RecordingOutcomeKind.NotDeliverable,
            blockingSteps: [{ stepId: "validation", displayName: { de: "XTF Validierung" } }],
            preflightFailed: false,
          },
          forwardingState: null,
          notificationState: null,
          steps: run.steps.map((step: RecordingStepResponse) => ({
            ...step,
            state: StepState.Cancelled,
            errorMessage: "A task was canceled.",
          })),
        },
      );
    });
    cy.visit(`/admin/recordings/${jobId}`);

    cy.dataCy("recordingFailureReason").should("contain", "Job timed out after 00:30:00.");
    cy.dataCy("recordingStep-preflight").should("not.contain", "Job timed out");
    cy.dataCy("recordingStep-validation").should("not.contain", "A task was canceled.");
  });

  it("lists no attempts for a run with a reference that is the only attempt of its delivery", () => {
    cy.fixture("recording.json").then(run => {
      cy.intercept({ method: "GET", pathname: `/api/v1/recording/${jobId}` }, { ...run, attempts: [run.attempts[0]] });
    });
    cy.visit(`/admin/recordings/${jobId}`);

    cy.dataCy("summary-clientReference").should("contain", "SMOKE-TEST-01");
    cy.dataCy("recordingAttempts").should("not.exist");
  });

  it("says that the pipeline could not be loaded instead of waiting on", () => {
    cy.intercept(
      { method: "GET", pathname: `/api/v1/recording/${jobId}/definition` },
      { statusCode: 500, body: { title: "An error occurred while processing your request.", status: 500 } },
    ).as("definition");

    cy.visit(`/admin/recordings/${jobId}#pipeline`);
    cy.wait("@definition");

    cy.dataCy("pipelineSnapshotNotLoaded").should("be.visible");
    cy.get('[role="tabpanel"]').find('[role="progressbar"]').should("not.exist");
  });

  describe("with files an earlier step passed on", () => {
    // The upload delivered roads.xtf and a matcher passed it on; a step of the run made a roads.xtf of its own.
    beforeEach(() => {
      cy.fixture("recording.json").then(run => {
        cy.intercept(
          { method: "GET", pathname: `/api/v1/recording/${jobId}` },
          {
            ...run,
            steps: run.steps.map((step: RecordingStepResponse) => ({
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
      cy.visit(`/admin/recordings/${jobId}`);
    });

    it("shows the hash of the preparation for a delivered file", () => {
      cy.dataCy("recordingStep-validation")
        .contains("tr", "transferFile")
        .should("contain", "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08");
    });

    it("says that a file made during the processing has no hash, also under the name of a delivered one", () => {
      cy.dataCy("recordingStep-validation")
        .contains("tr", "referenceDataFiles")
        .should("contain", "created during processing")
        .and("not.contain", "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08")
        .and("not.contain", "not computed");
    });
  });
});
