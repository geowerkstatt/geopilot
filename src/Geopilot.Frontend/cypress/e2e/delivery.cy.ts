import type {
  BrowserAuthOptions,
  Delivery,
  DeliveryRequest,
  MandateSummary,
  ProblemDetails,
  ProcessingSettingsResponse,
} from "@/api/generated";
import { loadWithoutAuth, loginAsNewUser, loginAsUploader } from "./helpers/appHelpers";
import {
  addFile,
  deliverableMandate,
  fileNameExists,
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
} from "./helpers/deliveryHelpers";
import { hasError, setChipInput, setInput, setSelect, toggleCheckbox } from "./helpers/formHelpers";

describe("Delivery tests", () => {
  it("can only upload supported file types", () => {
    // Limit the file types to a few extensions
    cy.intercept("GET", "/api/v2/processing", {
      statusCode: 200,
      body: {
        allowedFileExtensions: [".csv", ".gpkg", ".itf", ".xml", ".xtf", ".zip"],
      } satisfies ProcessingSettingsResponse,
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

    const dropFileNamed = (fileName: string) =>
      cy
        .dataCy("file-dropzone")
        .selectFile({ contents: Cypress.Buffer.from("<TRANSFER/>"), fileName }, { action: "drag-drop" });

    dropFileNamed("v1..2.xtf");
    stepHasError("files", true, "The file name contains invalid characters");
    fileNameExists("v1..2.xtf", false);

    addFile("deliveryFiles/ilimodels_valid.xtf", true);
    stepHasError("files", false);

    dropFileNamed("a:b.xtf");
    stepHasError("files", true, "The file name contains invalid characters");
    fileNameExists("a:b.xtf", false);

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
    selectMandate(1);
    startProcessing();
    waitForProcessingToFinish();

    // The stepper shows the condition message in the language the app runs in
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
    let created: { id: number; authorization: string | string[] };
    cy.intercept("/api/v1/delivery/summary?mandateId=*").as("precursors");

    loginAsUploader();
    addFile("deliveryFiles/ilimodels_valid.xtf", true);
    stepIsActive("files");
    uploadFile();

    stepIsActive("mandate");
    selectMandate(1);
    startProcessing();
    stepIsActive("processing");
    waitForProcessingToFinish();

    // The validation ran, so both log files are offered for download
    resultStepHasIcon("validation", "success");
    cy.dataCy("errorLog.log-button").should("exist");
    cy.dataCy("xtfLog.xtf-button").should("exist");
    stepperStepHasIcon("processing", "success");
    stepperStepHasIcon("delivery", "enabled");

    cy.dataCy("continue-button").should("be.enabled").click();
    stepIsActive("delivery");

    cy.dataCy("delivery-files-empty").should("not.exist");

    // Wait for select values to be present on DOM
    cy.wait("@precursors");
    cy.wait(200);

    setSelect("precursorDeliveryId", 1);
    hasError("precursorDeliveryId", false);
    toggleCheckbox("partialDelivery");

    cy.intercept("POST", "/api/v1/delivery").as("createDelivery");
    cy.dataCy("createDelivery-button").should("be.enabled").click();

    // The declared metadata reaches the API as text.
    cy.wait<DeliveryRequest, Delivery>("@createDelivery").then(({ request, response }) => {
      const body = typeof request.body === "string" ? JSON.parse(request.body) : request.body;
      expect(body.partialDelivery).to.equal(true);
      expect(body.precursorDeliveryId).to.be.a("number");

      created = { id: response!.body.id, authorization: request.headers.authorization };
    });

    cy.dataCy("createDelivery-button").should("not.exist");
    stepIsCompleted("delivery");

    // The delivery is real and would otherwise stay in the database, where the delivery overview tests
    // would find an extra row that their assertions do not expect.
    cy.then(() =>
      cy.request({
        method: "DELETE",
        url: `/api/v1/delivery/${created.id}`,
        headers: { authorization: created.authorization },
      }),
    );
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

  it("blocks the upload for an anonymous visitor without a public mandate available", () => {
    cy.intercept({ method: "GET", pathname: "/api/v1/mandate/summary" }, { statusCode: 200, body: [] }).as("mandates");
    cy.visit("/");
    cy.wait("@mandates");

    cy.dataCy("no-mandate-available").should(
      "contain",
      "No mandate is available for public processing. Please log in to use the mandates assigned to you.",
    );
    cy.dataCy("no-mandate-login-link").should("exist");
    cy.dataCy("file-dropzone").should("have.attr", "aria-disabled", "true");
    cy.dataCy("acceptTermsOfUse-formCheckbox").should("have.class", "Mui-disabled");
    addFile("deliveryFiles/ilimodels_valid.xtf", false);
  });

  it("blocks the upload for signed in user without a mandate available", () => {
    cy.intercept({ method: "GET", pathname: "/api/v1/mandate/summary" }, { statusCode: 200, body: [] }).as("mandates");
    loginAsNewUser();
    cy.wait("@mandates");

    cy.dataCy("no-mandate-available").should(
      "contain",
      "No mandate is available for your account, so processing is not possible. Please contact your administrator.",
    );
    cy.dataCy("file-dropzone").should("have.attr", "aria-disabled", "true");
    cy.dataCy("acceptTermsOfUse-formCheckbox").should("have.class", "Mui-disabled");
    addFile("deliveryFiles/ilimodels_valid.xtf", false);
  });

  it("keeps the upload open when the mandate check fails", () => {
    cy.intercept({ method: "GET", pathname: "/api/v1/mandate/summary" }, { statusCode: 500 }).as("mandates");
    loadWithoutAuth();
    cy.wait("@mandates");

    cy.dataCy("no-mandate-available").should("not.exist");
    cy.dataCy("file-dropzone").should("not.have.attr", "aria-disabled");
    cy.dataCy("acceptTermsOfUse-formCheckbox").should("not.have.class", "Mui-disabled");
    addFile("deliveryFiles/ilimodels_valid.xtf", true);
    cy.dataCy("file-list-item").should("have.length", 1);
  });

  it("displays custom error messages when they don't match predefined errors", () => {
    cy.intercept(
      { url: "/api/v2/upload", method: "POST" },
      {
        statusCode: 418, // I'm a teapot
        body: {
          detail: "I'm a teapot",
        } satisfies ProblemDetails,
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

  it("stays on the mandate step until the processing job is accepted", () => {
    const runningJob = processingJob("e2e-delayed-job", "pending", [
      processingStep("validation", "XTF Validation", "pending"),
    ]);
    cy.intercept("GET", "/api/v1/mandate/summary?uploadId=*").as("getMandates");

    loginAsUploader();
    addFile("deliveryFiles/ilimodels_valid.xtf", true);
    uploadFile();
    cy.wait("@getMandates");
    selectMandate(1);

    cy.intercept("POST", "/api/v2/processing", { statusCode: 202, body: runningJob, delay: 1000 }).as(
      "startProcessing",
    );
    cy.intercept("GET", "/api/v2/processing/*", { statusCode: 200, body: runningJob }).as("jobStatus");
    cy.dataCy("startProcessing-button").click();

    stepIsActive("mandate");
    cy.dataCy("startProcessing-button").should("be.disabled");
    cy.dataCy("mandate-selection-group").dataCy("mandate-1").should("have.class", "Mui-disabled");

    cy.wait("@startProcessing");
    stepIsActive("processing");
    cy.dataCy("processing-step-validation").should("exist");
  });

  it("shows a failed processing start on the mandate step", () => {
    cy.intercept("GET", "/api/v1/mandate/summary?uploadId=*").as("getMandates");

    loginAsUploader();
    addFile("deliveryFiles/ilimodels_valid.xtf", true);
    uploadFile();
    cy.wait("@getMandates");
    selectMandate(1);

    cy.intercept("POST", "/api/v2/processing", {
      statusCode: 500,
      body: { detail: "An unexpected error occured." } satisfies ProblemDetails,
    }).as("startProcessing");
    cy.dataCy("startProcessing-button").click();
    cy.wait("@startProcessing");

    stepIsActive("mandate");
    stepHasError("mandate", true, "Unexpected server error during processing. (Error 500)");
    cy.dataCy("startProcessing-button").should("be.enabled");
  });

  it("stops polling a running job on restart", () => {
    const runningJob = processingJob("e2e-restart-job", "running", [
      processingStep("validation", "XTF Validation", "running"),
    ]);
    runMockedProcessingJob(runningJob, [deliverableMandate(1, "Test Mandate")]);
    stepIsActive("processing");

    cy.dataCy("delivery-stepper").dataCy("restart-button").click();
    cy.dataCy("prompt-button-restart").click();
    stepIsActive("files");

    // The status is polled every two seconds, so a polling that survived the reset shows within three.
    cy.get<unknown[]>("@jobStatus.all").then(polls => {
      const pollsAtReset = polls.length;
      cy.wait(3000);
      cy.get<unknown[]>("@jobStatus.all").should("have.length", pollsAtReset);
    });
  });

  it("can show previous steps as read-only", () => {
    // Registered before the upload: the mandate request follows the upload response immediately,
    // so an intercept set up afterwards can miss it.
    cy.intercept("GET", "/api/v1/mandate/summary?uploadId=*").as("getMandates");

    loginAsUploader();
    addFile("deliveryFiles/ilimodels_valid.xtf", true);
    uploadFile();
    cy.wait("@getMandates");

    selectMandate(1);
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

describe("Delivery metadata fields", () => {
  const title = (de: string) => ({ de, en: de });
  const mandateWithMetadata: MandateSummary = {
    ...deliverableMandate(1, "Test Mandate"),
    metadataFields: [
      { id: "project_number", title: title("Projektnummer"), type: "text", evaluation: "required" },
      { id: "municipalities", title: title("Gemeinden"), type: "textList", evaluation: "optional" },
      {
        id: "responsible",
        title: title("Zuständig"),
        type: "selection",
        evaluation: "required",
        options: [
          { id: "canton", title: title("Kanton") },
          { id: "municipality", title: title("Gemeinde") },
        ],
      },
      { id: "parcel_count", title: title("Anzahl Parzellen"), type: "integer", evaluation: "required" },
      { id: "area", title: title("Fläche"), type: "decimal", evaluation: "optional" },
      { id: "due_date", title: title("Fällig am"), type: "date", evaluation: "optional" },
    ],
  };

  const openDeliveryStep = () => {
    const successJob = processingJob("e2e-metadata-job", "success", [
      processingStep("validation", "XTF Validation", "success", undefined, ["file1.xtf"]),
    ]);
    cy.intercept("GET", "/api/v1/delivery/summary?mandateId=*", { statusCode: 200, body: [] }).as("precursors");
    runMockedProcessingJob(successJob, [mandateWithMetadata]);
    // The page mounts twice on a fresh visit: act only once the finished job has enabled the way forward.
    cy.dataCy("continue-button").should("be.enabled");
    selectStep("delivery");
    stepIsActive("delivery");
    cy.dataCy("metadata.project_number-formInput").should("exist");
  };

  it("sends the metadata fields of the mandate with the delivery", () => {
    openDeliveryStep();
    cy.contains("No further information is required").should("not.exist");
    cy.contains("keine weiteren Angaben erforderlich").should("not.exist");

    setInput("metadata.project_number", "2026-117");
    setChipInput("metadata.municipalities", "Aarau");
    setSelect("metadata.responsible", 0);
    setInput("metadata.parcel_count", "12");
    setInput("metadata.due_date", "2026-11-04");

    cy.intercept("POST", "/api/v1/delivery", { statusCode: 200, body: { id: 1 } }).as("createDelivery");
    cy.dataCy("createDelivery-button").should("be.enabled").click();
    cy.wait<DeliveryRequest, Delivery>("@createDelivery").then(({ request }) => {
      const body = typeof request.body === "string" ? JSON.parse(request.body) : request.body;
      expect(body.metadata).to.deep.equal({
        project_number: "2026-117",
        municipalities: ["Aarau"],
        responsible: "canton",
        parcel_count: 12,
        area: null,
        due_date: "2026-11-04",
      });
    });
  });

  it("keeps the delivery disabled while a required number is missing or has decimals", () => {
    openDeliveryStep();
    setInput("metadata.project_number", "2026-117");
    setSelect("metadata.responsible", 0);
    cy.dataCy("createDelivery-button").should("be.disabled");

    setInput("metadata.parcel_count", "12.5");
    hasError("metadata.parcel_count");
    cy.dataCy("metadata.parcel_count-formInput")
      .find(".MuiFormHelperText-root")
      .should("have.text", "A number without decimal places");
    cy.dataCy("createDelivery-button").should("be.disabled");

    setInput("metadata.parcel_count", "12");
    hasError("metadata.parcel_count", false);
    cy.dataCy("createDelivery-button").should("be.enabled");
  });
});

describe("File type filter per platform", () => {
  // The native iOS/iPadOS grey-out cannot be reproduced in a desktop browser, so we assert the
  // observable proxy instead: the file input carries an `accept` attribute on desktop (native
  // pre-filtering) but not on iOS/iPadOS, where it would grey out .xtf files (see platform.ts).
  // The iOS cases first upload a wrong type and expect a rejection: that proves the restriction is
  // actually applied, so a missing `accept` can only mean the iOS branch dropped it, not that the
  // processing settings are still loading.
  const visitAs = (navigatorProps: Partial<Navigator>) => {
    // A mandate that restricts the file types (no ".*") so the accept filter would be active.
    cy.intercept("GET", "/api/v2/processing", {
      statusCode: 200,
      body: { allowedFileExtensions: [".xtf"] } satisfies ProcessingSettingsResponse,
    });
    cy.intercept("/api/v1/user/auth", {
      statusCode: 200,
      body: { authority: "", publicClientId: "", scope: "" } satisfies BrowserAuthOptions,
    });
    cy.intercept("GET", "/api/v1/mandate/summary").as("mandateCheck");
    cy.visit("/", {
      onBeforeLoad(win) {
        Object.entries(navigatorProps).forEach(([key, value]) =>
          Object.defineProperty(win.navigator, key, { value, configurable: true }),
        );
      },
    });
    // The page mounts again once the auth configuration has arrived, which resets the processing settings, and
    // only that mount checks the mandates. Its dropzone shows the limits once it knows the allowed file types.
    cy.wait("@mandateCheck");
    cy.dataCy("file-dropzone-limits").should("exist");
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
