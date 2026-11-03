import type { DeliveryDetailResponse, DeliveryResponse } from "@/api/generated";
import { ReactionState } from "@/api/generated";
import { getGridRowThatContains, loginAsAdmin } from "./helpers/appHelpers";
import { handlePrompt } from "./helpers/promptHelpers";

describe("Admin Delivery Overview tests", () => {
  it("can delete delivery", () => {
    cy.intercept({ url: "/api/v1/delivery", method: "GET" }).as("deliveries");
    loginAsAdmin();

    cy.visit("/admin/delivery-overview");
    cy.wait("@deliveries");

    // Newest delivery first
    cy.dataCy("deliveryOverview-grid")
      .find('[role="columnheader"][data-field="date"]')
      .should("have.attr", "aria-sort", "descending");

    // Unsorted, in the order of the server
    cy.dataCy("deliveryOverview-grid").contains("Delivery date").click();
    cy.dataCy("deliveryOverview-grid").find(".MuiDataGrid-row").first().contains("1");
    cy.dataCy("deliveryOverview-grid").find(".MuiDataGrid-row").first().contains("Kelvin Spencer");

    // Sort by delivery date ascending
    cy.dataCy("deliveryOverview-grid").contains("Delivery date").click();
    cy.dataCy("deliveryOverview-grid").find(".MuiDataGrid-row").first().contains("2022");

    // Sort to get newest delivery first
    cy.dataCy("deliveryOverview-grid").contains("Delivered by").click();
    cy.dataCy("deliveryOverview-grid").find(".MuiDataGrid-row").first().contains("Kelvin Spencer");

    // Sort by Mandate
    cy.dataCy("deliveryOverview-grid").contains("Mandate").click();
    cy.dataCy("deliveryOverview-grid").find(".MuiDataGrid-row").first().contains("Fantastic Fresh Tuna");

    // Sort by Id
    cy.dataCy("deliveryOverview-grid").contains("ID").click();
    cy.dataCy("deliveryOverview-grid").find(".MuiDataGrid-row").first().contains("1");

    // A delivery is deleted on its own page
    cy.intercept({ url: "/api/v1/delivery/1", method: "GET" }).as("delivery");
    cy.dataCy("deliveryOverview-grid").find(".MuiDataGrid-row").first().find('[data-cy="details"]').click();
    cy.wait("@delivery");

    cy.intercept("DELETE", "/api/v1/delivery/*", () => {
      throw new Error("Cancel should not trigger delete");
    });
    cy.dataCy("delete-button").click();
    handlePrompt("Do you really want to delete the delivery data? This action cannot be undone.", "cancel");
    // make sure no delete request was sent in this time frame
    cy.wait(400);

    cy.intercept("DELETE", "/api/v1/delivery/*", { statusCode: 200 }).as("deleteDelivery");
    cy.dataCy("delete-button").click();
    handlePrompt("Do you really want to delete the delivery data? This action cannot be undone.", "delete");
    cy.wait("@deleteDelivery");
    cy.wait("@deliveries");
    cy.location("pathname").should("eq", "/admin/delivery-overview");
  });
});

describe("Admin delivery overview with reactions", () => {
  // The seeded deliveries have no reactions, so the list is stubbed with deliveries in different states.
  const delivery = (id: number, forwardingState?: ReactionState, notificationState?: ReactionState) =>
    ({
      id,
      date: "2026-10-23T08:00:00Z",
      mandate: { id: 1, name: { de: "Mandat", en: "Mandate" } },
      declarerName: "Kelvin Spencer",
      comment: "",
      forwardingState,
      notificationState,
      reactions: [],
    }) satisfies DeliveryResponse;
  const deliveries: DeliveryResponse[] = [
    delivery(9001, ReactionState.Done, ReactionState.Done),
    delivery(9002, ReactionState.Done, ReactionState.Error),
    delivery(9003, ReactionState.Pending, ReactionState.Pending),
    delivery(9004),
  ];

  beforeEach(() => {
    cy.intercept("GET", "/api/v1/delivery", { statusCode: 200, body: deliveries }).as("deliveries");
    loginAsAdmin();
    cy.visit("/admin/delivery-overview");
    cy.wait("@deliveries");
  });

  it("sums up the forwarding and the notifications of each delivery", () => {
    getGridRowThatContains("deliveryOverview-grid", "9001")
      .find('[data-field="forwardingState"] [data-cy="reactionState-done"]')
      .should("exist");
    getGridRowThatContains("deliveryOverview-grid", "9002")
      .find('[data-field="notificationState"] [data-cy="reactionState-error"]')
      .should("exist");
    getGridRowThatContains("deliveryOverview-grid", "9003")
      .find('[data-field="forwardingState"] [data-cy="reactionState-pending"]')
      .should("exist");
    getGridRowThatContains("deliveryOverview-grid", "9004").find('[data-cy^="reactionState-"]').should("not.exist");
  });

  it("opens the page of a delivery without reactions by a click on its row", () => {
    cy.intercept("GET", "/api/v1/delivery/9004", {
      statusCode: 200,
      body: {
        id: 9004,
        date: "2026-10-23T08:00:00Z",
        mandate: { id: 1, name: { de: "Mandat", en: "Mandate" } },
        declarerName: "Kelvin Spencer",
        jobId: "3f2a9c1e-6b7d-4e58-9a0b-2c4d6e8f1a3b",
        files: [],
        comment: "",
        reactions: [],
      } satisfies DeliveryDetailResponse,
    }).as("delivery");

    getGridRowThatContains("deliveryOverview-grid", "9004").find('[data-field="mandateName"]').click();

    cy.wait("@delivery");
    cy.location("pathname").should("eq", "/admin/delivery-overview/9004");
    cy.go("back");
    cy.location("pathname").should("eq", "/admin/delivery-overview");
    cy.dataCy("reactions-grid").should("not.exist");
  });
});
