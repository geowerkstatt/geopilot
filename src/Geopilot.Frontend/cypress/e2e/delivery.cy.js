import { loadWithoutAuth, loginAsNewUser, loginAsUploader } from "./helpers/appHelpers.js";
import {
  addFile,
  deliverableMandate,
  e2eMandateName,
  nonDeliverableMandate,
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
  waitForProcessingToFinish,
} from "./helpers/deliveryHelpers.js";
import { hasError, setInput, setSelect, toggleCheckbox } from "./helpers/formHelpers.js";

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

  // Runs the real pipeline: the uploaded file carries no XTF, so the validation fails its pre-condition
  it("shows processing error without log files", () => {
    loginAsUploader();
    addFile("deliveryFiles/ilimodels_not_conform.xml", true);
    uploadFile();
    selectMandate(e2eMandateName);
    startProcessing();
    waitForProcessingToFinish();

    stepHasError(
      "processing",
      true,
      /Exactly one XTF file must be uploaded|Es muss genau eine XTF-Datei hochgeladen werden/,
    );
    resultStepHasIcon("validation", "error");
    cy.dataCy("errorLog.log-button").should("not.exist");
    cy.dataCy("xtfLog.xtf-button").should("not.exist");
    stepIsActive("processing");
    stepIsActive("delivery", false); // Should not be active if processing has errors
    cy.dataCy("continue-button").should("be.disabled");
  });

  // Runs the real pipeline: the valid XTF passes the validation and gets delivered
  it("can submit delivery", () => {
    const comment = "Declared by the end-to-end test.";
    cy.intercept("/api/v1/delivery/summary?mandateId=*").as("precursors");

    loginAsUploader();
    addFile("deliveryFiles/ilimodels_valid.xtf", true);
    stepIsActive("files");
    uploadFile();

    stepIsActive("mandate");
    selectMandate(e2eMandateName);
    startProcessing();
    stepIsActive("processing");
    waitForProcessingToFinish();

    resultStepHasIcon("validation", "success");
    cy.dataCy("errorLog.log-button").should("exist");
    cy.dataCy("xtfLog.xtf-button").should("exist");
    stepperStepHasIcon("processing", "success");
    stepperStepHasIcon("delivery", "enabled");

    cy.dataCy("continue-button").should("be.enabled").click();
    stepIsActive("delivery");

    cy.dataCy("delivery-files-empty").should("not.exist");

    cy.wait("@precursors");
    cy.wait(200);

    cy.dataCy("createDelivery-button").should("be.disabled");

    setSelect("precursorDeliveryId", 0);
    hasError("precursorDeliveryId", false);
    toggleCheckbox("partialDelivery");
    setInput("comment", comment);

    cy.intercept("POST", "/api/v1/delivery").as("createDelivery");
    cy.dataCy("createDelivery-button").should("be.enabled").click();

    // The declared metadata reaches the API, not just the form
    cy.wait("@createDelivery")
      .its("request.body")
      .should("deep.include", { partialDelivery: true, comment })
      .and(body => expect(body.precursorDeliveryId).to.be.a("number"));

    cy.dataCy("createDelivery-button").should("not.exist");
    stepIsCompleted("delivery");
  });

  it("marks the step and blocks delivery when a delivery restriction applies", () => {
    const restrictedJob = processingJob("e2e-restricted-job", "deliveryRestriction", [
      processingStep("xtf_matching", "XTF Matching", "success"),
      processingStep("validation", "XTF Validation", "deliveryRestriction", "Validation was not successful."),
      processingStep("topology_check", "Topology Check", "deliveryRestriction", "Topology check failed."),
      processingStep("cleanup", "Cleanup", "success"),
    ]);

    runMockedProcessingJob(restrictedJob);

    resultStepHasIcon("validation", "deliveryRestriction");
    resultStepShowsMessage("validation", "Delivery is not possible");
    resultStepHasIcon("topology_check", "deliveryRestriction");
    resultStepHasIcon("cleanup", "success");

    stepperStepHasIcon("processing", "deliveryRestriction");
    stepperStepShowsMessage("processing", "Validation was not successful");
    stepperStepShowsMessage("processing", "Topology check failed");
    stepIsSkipped("delivery", true, "Blocked by processing");
  });

  it("enables the delivery step as ready and completes it once a delivery is created", () => {
    const successJob = processingJob("e2e-success-job", "success", [
      processingStep("xtf_matching", "XTF Matching", "success"),
      processingStep("validation", "XTF Validation", "success", undefined, ["file1.xtf"]),
      processingStep(
        "error_visualization",
        "Error Visualization",
        "skipped",
        "Skipped because no errors were reported.",
      ),
    ]);
    const runningJob = processingJob("e2e-success-job", "running", [
      processingStep("xtf_matching", "XTF Matching", "success"),
      processingStep("validation", "XTF Validation", "pending"),
    ]);

    cy.intercept("GET", "/api/v1/delivery/summary?mandateId=*", { statusCode: 200, body: [] }).as("precursors");
    runMockedProcessingJob(
      successJob,
      [deliverableMandate(1, "Test Mandate"), deliverableMandate(2, "Other Mandate")],
      runningJob,
    );

    // While the job runs the way forward is already there, but not usable yet.
    stepIsLoading("processing", true);
    cy.dataCy("continue-button").should("be.visible").and("be.disabled");

    cy.wait("@jobStatus");
    stepIsLoading("processing", false);

    resultStepHasIcon("validation", "success");
    resultStepHasIcon("error_visualization", "skipped");
    resultStepShowsMessage("error_visualization", "Skipped because no errors were reported.");

    stepperStepHasIcon("processing", "success");
    stepperStepHasIcon("delivery", "enabled");
    stepperStepMissingIcon("delivery", "skipped");

    // The ready step can be opened directly from the stepper.
    cy.dataCy("continue-button").should("be.enabled");
    selectStep("delivery");
    stepIsActive("delivery");

    // the delivery submit step shows a list of files to be delivered
    cy.dataCy("delivery-content-carousel").contains("file1.xtf").should("exist");

    cy.intercept("POST", "/api/v1/delivery", { statusCode: 200, body: { id: 1 } }).as("createDelivery");
    cy.dataCy("createDelivery-button").should("be.enabled").click();
    cy.wait("@createDelivery");

    cy.dataCy("createDelivery-button").should("not.exist");
    stepIsCompleted("delivery");
  });

  it("shows warnings on the steps without blocking delivery", () => {
    const warningJob = processingJob("e2e-warning-job", "warning", [
      processingStep("xtf_matching", "XTF Matching", "success"),
      processingStep("validation", "XTF Validation", "warning", "Validation completed with warnings.", ["file1.xtf"]),
      processingStep("topology_check", "Topology Check", "warning", "Minor topology issues detected."),
    ]);

    runMockedProcessingJob(warningJob);

    resultStepHasIcon("validation", "warning");
    resultStepShowsMessage("validation", "Validation completed with warnings.");
    resultStepHasIcon("topology_check", "warning");
    resultStepShowsMessage("topology_check", "Minor topology issues detected.");

    stepperStepHasIcon("processing", "warning");
    stepperStepShowsMessage("processing", "Validation completed with warnings.");
    stepperStepShowsMessage("processing", "Minor topology issues detected.");
    cy.dataCy("stepIcon-deliveryRestriction").should("not.exist");
    cy.dataCy("stepIcon-error").should("not.exist");

    cy.dataCy("continue-button").should("be.enabled").click();
    stepIsActive("delivery");

    // Continuing is a one-way move, the button is gone when the processing step is revisited
    selectStep("processing");
    stepIsActive("processing");
    cy.dataCy("continue-button").should("not.exist");
  });

  it("keeps warnings out of the stepper but visible in the accordion when delivery is restricted", () => {
    const mixedJob = processingJob("e2e-mixed-job", "deliveryRestriction", [
      processingStep("xtf_matching", "XTF Matching", "success"),
      processingStep("validation", "XTF Validation", "warning", "Validation completed with warnings.", ["file1.xtf"]),
      processingStep("topology_check", "Topology Check", "deliveryRestriction", "Topology check failed."),
    ]);

    runMockedProcessingJob(mixedJob);

    resultStepHasIcon("validation", "warning");
    resultStepShowsMessage("validation", "Validation completed with warnings.");
    resultStepHasIcon("topology_check", "deliveryRestriction");

    stepperStepHasIcon("processing", "deliveryRestriction");
    stepperStepShowsMessage("processing", "Topology check failed");
    stepperStepMissingMessage("processing", "Validation completed with warnings.");

    stepIsSkipped("delivery", true, "Blocked by processing");
    // A blocked delivery keeps the button visible so it stays apparent that the step exists
    cy.dataCy("continue-button").should("be.disabled");
  });

  it("stops the pipeline and blocks delivery when a step fails", () => {
    const failedJob = processingJob("e2e-failed-job", "failed", [
      processingStep("xtf_matching", "XTF Matching", "success"),
      processingStep("validation", "XTF Validation", "error", "Validation failed unexpectedly."),
      processingStep("topology_check", "Topology Check", "pending"),
    ]);

    runMockedProcessingJob(failedJob);

    resultStepHasIcon("validation", "error");
    resultStepShowsMessage("validation", "Validation failed unexpectedly.");
    resultStepHasIcon("topology_check", "pending");

    stepperStepHasIcon("processing", "error");
    stepperStepShowsMessage("processing", "Validation failed unexpectedly.");
    stepperStepHasIcon("delivery", "pending");
    stepperStepMissingIcon("delivery", "skipped");
    cy.dataCy("continue-button").should("be.disabled");
  });

  it("disables delivery without files", () => {
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

    cy.intercept("GET", "/api/v1/delivery/summary?mandateId=*", { statusCode: 200, body: [] }).as("precursors");
    runMockedProcessingJob(successJob, [deliverableMandate(1, "Test Mandate"), deliverableMandate(2, "Other Mandate")]);

    resultStepHasIcon("validation", "success");
    resultStepHasIcon("error_visualization", "skipped");
    resultStepShowsMessage("error_visualization", "Skipped because no errors were reported.");

    stepperStepHasIcon("processing", "success");
    stepperStepHasIcon("delivery", "enabled");
    stepperStepMissingIcon("delivery", "skipped");

    cy.dataCy("continue-button").should("be.enabled");
    selectStep("delivery");
    stepIsActive("delivery");

    cy.intercept("POST", "/api/v1/delivery", { statusCode: 200, body: { id: 1 } }).as("createDelivery");
    cy.dataCy("createDelivery-button").should("not.be.enabled");
    cy.dataCy("delivery-files-empty").should("exist");
  });

  it("offers no continue button when the mandate allows no delivery", () => {
    const successJob = processingJob("e2e-no-delivery-job", "success", [
      processingStep("xtf_matching", "XTF Matching", "success"),
      processingStep("validation", "XTF Validation", "success", undefined, ["file1.xtf"]),
    ]);

    runMockedProcessingJob(successJob, [nonDeliverableMandate(1, "Validation Only")]);

    stepperStepHasIcon("processing", "success");
    cy.dataCy("delivery-step").should("not.exist");
    cy.dataCy("continue-button").should("not.exist");
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
    // Registered before the upload: the mandate request follows the upload response immediately,
    // so an intercept set up afterwards can miss it.
    cy.intercept("GET", "/api/v1/mandate/summary?uploadId=*").as("getMandates");

    loginAsUploader();
    addFile("deliveryFiles/ilimodels_valid.xtf", true);
    uploadFile();
    cy.wait("@getMandates");

    selectMandate(e2eMandateName);
    startProcessing();
    stepIsActive("processing");

    // Navigation happens through the stepper, the steps carry no back button
    cy.dataCy("back-button").should("not.exist");
    selectStep("mandate");
    stepIsActive("mandate");
    stepIsActive("processing", false);

    selectStep("files");
    stepIsActive("files");
    cy.dataCy("upload-button").should("not.exist");

    selectStep("mandate");
    // Select mandate step shows previously selected mandate
    stepIsActive("mandate");
    cy.dataCy("mandate-selection-group")
      .contains(e2eMandateName)
      .should("have.class", "Mui-selected")
      .should("have.class", "Mui-disabled");

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

describe("File type filter per platform", () => {
  // The native iOS/iPadOS grey-out cannot be reproduced in a desktop browser, so we assert the
  // observable proxy instead: the file input carries an `accept` attribute on desktop (native
  // pre-filtering) but not on iOS/iPadOS, where it would grey out .xtf files (see platform.ts).
  // The iOS cases first upload a wrong type and expect a rejection: that proves the restriction is
  // actually applied, so a missing `accept` can only mean the iOS branch dropped it, not that the
  // processing settings are still loading.
  const visitAs = navigatorProps => {
    // A mandate that restricts the file types (no ".*") so the accept filter would be active.
    cy.intercept("GET", "/api/v2/processing", {
      statusCode: 200,
      body: { allowedFileExtensions: [".xtf"] },
    }).as("fileExtensions");
    cy.intercept("/api/v1/user/auth", { statusCode: 200, body: { authority: "", clientAudience: "" } });
    cy.visit("/", {
      onBeforeLoad(win) {
        Object.entries(navigatorProps).forEach(([key, value]) =>
          Object.defineProperty(win.navigator, key, { value, configurable: true }),
        );
      },
    });
    cy.wait("@fileExtensions");
    cy.dataCy("file-dropzone").should("exist");
  };

  const fileInput = () => cy.dataCy("file-dropzone").find("input[type=file]");

  it("keeps the native accept filter on desktop", () => {
    visitAs({ userAgent: "Mozilla/5.0 (Windows NT 10.0; Win64; x64)", platform: "Win32", maxTouchPoints: 0 });
    fileInput().should("have.attr", "accept").and("include", ".xtf");
  });

  it("drops the native accept filter on iPhone", () => {
    visitAs({
      userAgent: "Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15",
      platform: "iPhone",
      maxTouchPoints: 5,
    });
    addFile("deliveryFiles/picture-type.png", false);
    stepHasError("files", true, "The file type is not supported");
    fileInput().should("not.have.attr", "accept");
  });

  it("drops the native accept filter on iPad that reports as a Mac", () => {
    visitAs({
      userAgent: "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15",
      platform: "MacIntel",
      maxTouchPoints: 5,
    });
    addFile("deliveryFiles/picture-type.png", false);
    stepHasError("files", true, "The file type is not supported");
    fileInput().should("not.have.attr", "accept");
  });
});
