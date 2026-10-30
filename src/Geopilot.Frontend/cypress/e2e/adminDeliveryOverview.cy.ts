import type { DeliveryReactionResponse, DeliveryResponse } from "@/api/generated";
import { ReactionKind, ReactionState } from "@/api/generated";
import { getGridRowThatContains, loginAsAdmin } from "./helpers/appHelpers";
import { assertCancelDoesNotDeleteDelivery, assertConfirmDeletesDelivery } from "./helpers/deliveryOverviewHelper";
import { handlePrompt } from "./helpers/promptHelpers";

describe("Admin Delivery Overview tests", () => {
  it("can delete delivery", () => {
    cy.intercept({ url: "/api/v1/delivery", method: "GET" }).as("deliveries");
    cy.intercept("DELETE", "/api/v1/delivery/*", { statusCode: 200 }).as("deleteDelivery");
    loginAsAdmin();

    // Delete delivery
    cy.visit("/admin/delivery-overview");
    cy.wait("@deliveries");
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

    // Sort by comment
    cy.dataCy("deliveryOverview-grid").contains("Comment").click();
    cy.dataCy("deliveryOverview-grid")
      .find(".MuiDataGrid-row")
      .first()
      .contains("heard about this on wonky radio, decided to give it a try.");

    // Sort by Id
    cy.dataCy("deliveryOverview-grid").contains("ID").click();
    cy.dataCy("deliveryOverview-grid").find(".MuiDataGrid-row").first().contains("1");

    assertCancelDoesNotDeleteDelivery();
    assertConfirmDeletesDelivery();

    cy.wait("@deliveries");
  });
});

describe("Admin delivery overview with reactions", () => {
  // The seeded deliveries have no reactions, so the list is stubbed with one delivery per state of its forwarding.
  const transfer = (state: ReactionState, details: Partial<DeliveryReactionResponse>): DeliveryReactionResponse => ({
    reactionId: "ablage",
    displayName: { de: "Ablage", en: "File drop" },
    kind: ReactionKind.Transfer,
    state,
    attempts: 1,
    ...details,
  });
  const delivery = (id: number, reactions: DeliveryReactionResponse[], forwardingState?: ReactionState) =>
    ({
      id,
      date: "2026-10-23T08:00:00Z",
      mandate: { id: 1, name: { de: "Mandat", en: "Mandate" } },
      declarerName: "Kelvin Spencer",
      comment: `Delivery ${id}`,
      forwardingState,
      reactions,
    }) satisfies DeliveryResponse;
  const deliveries: DeliveryResponse[] = [
    delivery(9001, [transfer(ReactionState.Done, { message: "Delivered to <target>." })], ReactionState.Done),
    delivery(
      9002,
      // A reaction the definition no longer has comes without a name.
      [
        transfer(ReactionState.Error, {
          displayName: null,
          attempts: 7,
          message: "The base directory <drop> does not exist.",
        }),
      ],
      ReactionState.Error,
    ),
    delivery(
      9003,
      [transfer(ReactionState.Pending, { attempts: 0, nextAttemptAt: "2026-10-23T22:00:00Z" })],
      ReactionState.Pending,
    ),
    delivery(9004, []),
  ];

  const openForwarding = (deliveryId: number) =>
    getGridRowThatContains("deliveryOverview-grid", `Delivery ${deliveryId}`)
      .find('[data-cy="forwardingState-button"]')
      .click();

  beforeEach(() => {
    cy.intercept("GET", "/api/v1/delivery", { statusCode: 200, body: deliveries }).as("deliveries");
    loginAsAdmin();
    cy.visit("/admin/delivery-overview");
    cy.wait("@deliveries");
  });

  it("shows how far the forwarding of each delivery got", () => {
    getGridRowThatContains("deliveryOverview-grid", "Delivery 9001")
      .find('[data-cy="reactionState-done"]')
      .should("exist");
    getGridRowThatContains("deliveryOverview-grid", "Delivery 9002")
      .find('[data-cy="reactionState-error"]')
      .should("exist");
    getGridRowThatContains("deliveryOverview-grid", "Delivery 9003")
      .find('[data-cy="reactionState-pending"]')
      .should("exist");
    getGridRowThatContains("deliveryOverview-grid", "Delivery 9004")
      .find('[data-cy="forwardingState-button"]')
      .should("not.exist");

    openForwarding(9003);
    cy.dataCy("reaction-ablage").should("contain", "File drop").and("contain", "ablage").and("contain", "Waits until");
    cy.dataCy("resetReactions-button").should("not.exist");
    cy.dataCy("close-button").click();
    cy.dataCy("forwarding-dialog").should("not.exist");
  });

  it("resets the failed reactions of a delivery", () => {
    cy.intercept("POST", "/api/v1/delivery/9002/reactions/reset", { statusCode: 204 }).as("reset");

    openForwarding(9002);
    cy.dataCy("reaction-ablage").should("contain", "ablage").and("not.contain", "File drop");
    cy.dataCy("forwarding-dialog").should("contain", "does not exist");
    cy.dataCy("resetReactions-button").click();

    cy.wait("@reset");
    cy.wait("@deliveries");
    cy.dataCy("forwarding-dialog").should("not.exist");
  });

  it("names the forwarding before a forwarded delivery is deleted", () => {
    cy.intercept("DELETE", "/api/v1/delivery/*", () => {
      throw new Error("Cancel should not trigger delete");
    });

    getGridRowThatContains("deliveryOverview-grid", "Delivery 9001").find('[data-cy="delete"]').click();
    handlePrompt(
      "The delivery has already been forwarded to: File drop. The files stay there. Do you still want to delete the delivery data? This action cannot be undone.",
      "cancel",
    );
  });
});
