import { loadWithoutAuth, loginAsNewUser, loginAsUploader } from "./helpers/appHelpers.js";
import {
  addFile,
  processingJob,
  processingStep,
  resultStepHasIcon,
  resultStepShowsMessage,
  runMockedProcessingJob,
  selectMandate,
  selectStep,
  startProcessing,
  stepHasError,
  stepIsActive,
  stepIsCompleted,
  stepIsLoading,
  stepIsSkipped,
  stepperStepHasIcon,
  stepperStepMissingIcon,
  stepperStepMissingMessage,
  stepperStepShowsMessage,
  uploadFile,
} from "./helpers/deliveryHelpers.js";
import { hasError, setSelect } from "./helpers/formHelpers.js";

describe("Delivery tests", () => {
  it("can only upload supported file types", () => {
    // Limit the file types to a few extensions
    cy.intercept("GET", "/api/v2/processing", {
      statusCode: 200,
      body: { allowedFileExtensions: [".csv", ".gpkg", ".itf", ".xml", ".xtf", ".zip"] },
    }).as("fileExtensions");

    loadWithoutAuth();
    cy.dataCy("files-step").should("exist");
    cy.dataCy("mandate-step").should("exist");
    cy.dataCy("processing-step").should("exist");
    cy.dataCy("delivery-step").should("not.exist");
    stepIsActive("files", true);

    cy.wait("@fileExtensions");

    addFile("deliveryFiles/picture-type.png", false);
    stepHasError("files", true, "The file type is not supported");

    addFile("deliveryFiles/ilimodels_valid.xtf", true);
    stepHasError("files", false);

    addFile(["deliveryFiles/ilimodels_invalid.xml", "deliveryFiles/ilimodels_not_conform.xml"], true);
    stepHasError("files", false);

    cy.dataCy("file-list-item").should("have.length", 3);

    uploadFile();

    stepIsActive("mandate");
  });

  // Skip test as starting the processing currently results in a 500 when running in the github action
  it.skip("shows processing error without log files", () => {
    loginAsUploader();
    addFile("deliveryFiles/ilimodels_not_conform.xml", true);
    uploadFile();
    selectMandate(1);
    startProcessing();
    stepIsLoading("processing", true);
    stepHasError("processing", true, "Failed");
    cy.dataCy("processing-step-validation").dataCy("stepIcon-error").should("exist");
    cy.dataCy("errorLog.log-button").should("not.exist");
    cy.dataCy("xtfLog.xtf-button").should("not.exist");
    stepIsActive("processing");
    stepIsActive("delivery", false); // Should not be active if processing has errors
    cy.dataCy("continue-button").should("be.disabled");
  });

  // Skip test as starting the processing currently results in a 500 when running in the github action
  it.skip("can submit delivery", () => {
    cy.intercept("/api/v1/delivery?mandateId=*").as("precursors");

    loginAsUploader();
    addFile("deliveryFiles/ilimodels_valid.xtf", true);
    stepIsActive("files");
    uploadFile();

    stepIsActive("mandate");
    selectMandate(1);
    startProcessing();

    stepIsActive("processing");

    // XTF log files should be available
    cy.dataCy("errorLog.log-button").should("exist");
    cy.dataCy("xtfLog.xtf-button").should("exist");

    cy.dataCy("continue-button").click();
    stepIsActive("delivery");

    //Wait for select values to be present on DOM
    cy.wait("@precursors");
    cy.wait(200);

    // Declare delivery metadata
    setSelect("precursor", 0);
    hasError("precursor", false);
    cy.dataCy("createDelivery-button").should("be.enabled");

    // Complete delivery
    cy.dataCy("createDelivery-button").should("be.enabled").click();
    stepIsActive("delivery");
    stepIsCompleted("delivery");
  });

  it("marks the step and blocks delivery when a delivery restriction applies", () => {
    // Processing is fully mocked so this runs in CI, where real pipeline execution is unavailable (that is
    // why the specs above are skipped). Two steps restrict delivery, so the job aggregates to a restricted
    // state. A later step still runs to success, so the pipeline completed rather than stopping at the
    // restriction. The processing node carries the merged reasons of all restricting steps, and the delivery
    // node is skipped with a generic "blocked by processing" note.
    const restrictedJob = processingJob("e2e-restricted-job", "deliveryRestriction", [
      processingStep("xtf_matching", "XTF Matching", "success"),
      processingStep(
        "validation",
        "XTF Validation",
        "deliveryRestriction",
        "Validation was not successful. Delivery is not possible.",
      ),
      processingStep("topology_check", "Topology Check", "deliveryRestriction", "Topology check failed."),
      processingStep("cleanup", "Cleanup", "success"),
    ]);

    runMockedProcessingJob(restrictedJob);

    // Right results pane: both restricting steps show the delivery-restriction icon and a blocked alert; the
    // trailing step still ran to success, proving the pipeline completed.
    resultStepHasIcon("validation", "deliveryRestriction");
    resultStepShowsMessage("validation", "Delivery is not possible");
    resultStepHasIcon("topology_check", "deliveryRestriction");
    resultStepHasIcon("cleanup", "success");

    // Left stepper: the processing node shows the delivery-restriction state and carries the merged reasons
    // of all restricting steps, while the delivery node is skipped with a generic blocked note.
    stepperStepHasIcon("processing", "deliveryRestriction");
    stepperStepShowsMessage("processing", "Validation was not successful");
    stepperStepShowsMessage("processing", "Topology check failed");
    stepIsSkipped("delivery", true, "Blocked by processing");
  });

  it("enables the delivery step as ready when processing succeeds", () => {
    // A deliverable (successful) job unlocks delivery: every executed step is green, a step that only runs
    // after errors is skipped and explains itself, and the delivery node becomes enabled ("ready"), not
    // skipped, and can be opened directly from the stepper (it no longer looks disabled).
    const successJob = processingJob("e2e-success-job", "success", [
      processingStep("xtf_matching", "XTF Matching", "success"),
      processingStep("validation", "XTF Validation", "success"),
      processingStep(
        "error_visualization",
        "Error Visualization",
        "skipped",
        "Skipped because no errors were reported.",
      ),
    ]);

    runMockedProcessingJob(successJob);

    // Right results pane: executed steps are green; the skipped step shows its explanation as an info alert.
    resultStepHasIcon("validation", "success");
    resultStepHasIcon("error_visualization", "skipped");
    resultStepShowsMessage("error_visualization", "Skipped because no errors were reported.");

    // Left stepper: the processing node completes and the delivery node is enabled ("ready"), not skipped.
    stepperStepHasIcon("processing", "success");
    stepperStepHasIcon("delivery", "enabled");
    stepperStepMissingIcon("delivery", "skipped");

    // Delivery is deliverable: the user can continue, and the ready step can also be opened directly.
    cy.intercept("GET", "/api/v1/delivery?mandateId=*", { statusCode: 200, body: [] }).as("precursors");
    cy.dataCy("continue-button").should("be.enabled");
    selectStep("delivery");
    stepIsActive("delivery");
  });

  it("shows warnings on the steps without blocking delivery", () => {
    // A warning is a non-blocking outcome: the run stays deliverable (the Warning capability is kept for
    // pipelines that opt into it, even though the shipped pipelines do not use it). Every warning is listed
    // on the processing node in the stepper and shown as an alert in the step's accordion.
    const warningJob = processingJob("e2e-warning-job", "warning", [
      processingStep("xtf_matching", "XTF Matching", "success"),
      processingStep("validation", "XTF Validation", "warning", "Validation completed with warnings."),
      processingStep("topology_check", "Topology Check", "warning", "Minor topology issues detected."),
    ]);

    runMockedProcessingJob(warningJob);

    // Right results pane: each warning step shows the warning icon and its message in the accordion.
    resultStepHasIcon("validation", "warning");
    resultStepShowsMessage("validation", "Validation completed with warnings.");
    resultStepHasIcon("topology_check", "warning");
    resultStepShowsMessage("topology_check", "Minor topology issues detected.");

    // Left stepper: the processing node stays in the (deliverable) warning state and lists all warnings; no
    // delivery-restriction or error state appears.
    stepperStepHasIcon("processing", "warning");
    stepperStepShowsMessage("processing", "Validation completed with warnings.");
    stepperStepShowsMessage("processing", "Minor topology issues detected.");
    cy.dataCy("stepIcon-deliveryRestriction").should("not.exist");
    cy.dataCy("stepIcon-error").should("not.exist");

    // Delivery stays possible: continuing leads to the delivery step.
    cy.dataCy("continue-button").should("be.enabled").click();
    stepIsActive("delivery");
  });

  it("keeps warnings out of the stepper but visible in the accordion when delivery is restricted", () => {
    // A single step cannot be both warning and delivery-restriction (post-conditions resolve to one state),
    // so a warning lives on its own step. The job aggregates to the more severe delivery-restriction, and
    // the stepper surfaces only the restriction reasons while the warning stays in its own accordion.
    const mixedJob = processingJob("e2e-mixed-job", "deliveryRestriction", [
      processingStep("xtf_matching", "XTF Matching", "success"),
      processingStep("validation", "XTF Validation", "warning", "Validation completed with warnings."),
      processingStep(
        "topology_check",
        "Topology Check",
        "deliveryRestriction",
        "Topology check failed. Delivery is not possible.",
      ),
    ]);

    runMockedProcessingJob(mixedJob);

    // Right results pane: the warning step keeps its warning icon and message; the restricting step is blocked.
    resultStepHasIcon("validation", "warning");
    resultStepShowsMessage("validation", "Validation completed with warnings.");
    resultStepHasIcon("topology_check", "deliveryRestriction");

    // Left stepper: only the restriction reason is surfaced; the warning message is not shown there.
    stepperStepHasIcon("processing", "deliveryRestriction");
    stepperStepShowsMessage("processing", "Topology check failed");
    stepperStepMissingMessage("processing", "Validation completed with warnings.");

    // Delivery is blocked.
    stepIsSkipped("delivery", true, "Blocked by processing");
  });

  it("stops the pipeline and blocks delivery when a step fails", () => {
    // A failing step stops the pipeline: steps after it are never run and stay pending, and the job fails
    // (reported as "failed", normalized to the error state). Delivery is never reached and stays pending.
    const failedJob = processingJob("e2e-failed-job", "failed", [
      processingStep("xtf_matching", "XTF Matching", "success"),
      processingStep("validation", "XTF Validation", "error", "Validation failed unexpectedly."),
      processingStep("topology_check", "Topology Check", "pending"),
    ]);

    runMockedProcessingJob(failedJob);

    // Right results pane: the failing step shows the error icon and message; the step after it never ran and
    // stays pending.
    resultStepHasIcon("validation", "error");
    resultStepShowsMessage("validation", "Validation failed unexpectedly.");
    resultStepHasIcon("topology_check", "pending");

    // Left stepper: the processing node shows the error and its message; the delivery node was never reached
    // and stays pending (not skipped), and delivery cannot proceed.
    stepperStepHasIcon("processing", "error");
    stepperStepShowsMessage("processing", "Validation failed unexpectedly.");
    stepperStepHasIcon("delivery", "pending");
    stepperStepMissingIcon("delivery", "skipped");
    cy.dataCy("continue-button").should("be.disabled");
  });

  it("displays error if no mandates were found", () => {
    loginAsNewUser();
    addFile("deliveryFiles/ilimodels_invalid.xml", true);
    uploadFile();
    stepIsActive("mandate");
    stepHasError("mandate", true, "No suitable mandate was found for your delivery");
  });

  it("displays custom error messages when they don't match predefined errors", () => {
    cy.intercept(
      { url: "/api/v2/upload", method: "POST" },
      {
        statusCode: 418, // I'm a teapot
        body: {
          detail: "I'm a teapot",
        },
        delay: 500, // Added 500ms delay
      },
    ).as("customError");

    loginAsUploader();
    addFile("deliveryFiles/ilimodels_valid.xtf", true);
    uploadFile();
    cy.wait("@customError");

    // Should display the actual error message since there's no mapping for 418
    stepHasError("files", true, "I'm a teapot");
  });

  it("can show previous steps as read-only", () => {
    loginAsUploader();
    addFile("deliveryFiles/ilimodels_valid.xtf", true);
    uploadFile();

    cy.intercept("GET", "/api/v1/mandate?uploadId=*").as("getMandates");
    cy.wait("@getMandates");

    selectMandate(1);
    startProcessing();
    stepIsActive("processing");

    // Can navigate with back button
    cy.dataCy("back-button").click();
    stepIsActive("mandate");
    stepIsActive("processing", false);

    // Can navigate by clicking on the step
    selectStep("files");
    stepIsActive("files");
    cy.dataCy("upload-button").should("not.exist");

    cy.dataCy("continue-button").click();
    // Select mandate step shows previously selected mandate
    stepIsActive("mandate");
    cy.dataCy("mandate-1").should("have.class", "Mui-selected").should("have.class", "Mui-disabled");

    // Can not navigate to future steps
    selectStep("delivery");
    stepIsActive("delivery", false);

    selectStep("processing");
    stepIsActive("processing");
  });

  it("renders content carousel on mobile with only the active step mounted", () => {
    cy.viewport("iphone-x");
    loadWithoutAuth();
    stepIsActive("files", true);

    cy.dataCy("delivery-content-carousel").should("exist");
    cy.dataCy("file-dropzone").should("exist");
    cy.dataCy("mandate-selection-group").should("not.exist");
  });
});
