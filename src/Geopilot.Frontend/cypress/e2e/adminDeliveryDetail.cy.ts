import type { DeliveryDetailResponse, DeliveryReactionResponse } from "@/api/generated";
import { ReactionKind, ReactionState } from "@/api/generated";
import { getTableRowThatContains, loginAsAdmin } from "./helpers/appHelpers";
import { handlePrompt } from "./helpers/promptHelpers";

describe("Admin delivery detail", () => {
  // The seeded deliveries have no reactions, so the delivery is stubbed.
  const transfer = (
    state: ReactionState,
    details: Partial<DeliveryReactionResponse> = {},
  ): DeliveryReactionResponse => ({
    reactionId: "ablage",
    displayName: { de: "Ablage", en: "File drop" },
    kind: ReactionKind.Transfer,
    state,
    attempts: 1,
    ...details,
  });
  const infoMail = (
    state: ReactionState,
    details: Partial<DeliveryReactionResponse> = {},
  ): DeliveryReactionResponse => ({
    reactionId: "info_mail",
    displayName: { de: "Info-Mail", en: "Info mail" },
    kind: ReactionKind.Notification,
    state,
    attempts: 0,
    ...details,
  });
  const delivery = (
    id: number,
    reactions: DeliveryReactionResponse[],
    details: Partial<DeliveryDetailResponse> = {},
  ): DeliveryDetailResponse => ({
    id,
    date: "2026-10-23T08:00:00Z",
    mandate: { id: 1, name: { de: "Mandat", en: "Mandate" } },
    declarerName: "Kelvin Spencer",
    declaringUserId: 7,
    jobId: "3f2a9c1e-6b7d-4e58-9a0b-2c4d6e8f1a3b",
    files: [
      { id: 41, fileName: "strassen.xtf" },
      { id: 42, fileName: "errorLog.log" },
    ],
    comment: "Second delivery with the corrected profiles",
    reactions,
    ...details,
  });

  const visit = (detail: DeliveryDetailResponse) => {
    cy.intercept("GET", `/api/v1/delivery/${detail.id}`, { statusCode: 200, body: detail }).as("delivery");
    loginAsAdmin();
    cy.visit(`/admin/deliveries/${detail.id}`);
    cy.wait("@delivery");
  };

  it("sums up a forwarded delivery with links to its files and its STAC item", () => {
    visit(
      delivery(9001, [transfer(ReactionState.Done, { finishedAt: "2026-10-23T08:05:00Z" })], {
        precursorDelivery: { id: 9000, date: "2026-10-20T08:00:00Z" },
        partial: false,
        stacItem: { collectionId: "coll_1", itemId: "item_9001" },
      }),
    );

    cy.dataCy("summary-deliveredBy").find('a[href="/admin/users/7"]').should("contain", "Kelvin Spencer");
    cy.dataCy("summary-mandate").find('a[href="/admin/mandates/1"]').should("contain", "Mandate");
    cy.dataCy("summary-precursor").find('a[href="/admin/deliveries/9000"]').should("contain", "9000");
    cy.dataCy("summary-partialDelivery").should("contain", "No");
    cy.dataCy("summary-files").find('a[href="/api/v1/delivery/assets/41"]').should("contain", "strassen.xtf");
    cy.dataCy("summary-stacBrowser")
      .find('a[href="/browser/stac/collections/coll_1/items/item_9001"]')
      .should("contain", "item_9001");
    cy.dataCy("summary-comment").should("contain", "Second delivery with the corrected profiles");
    getTableRowThatContains("reactions-table", "ablage").should("contain", "File drop").and("contain", "Ended");
    cy.dataCy("retryReaction").should("not.exist");
  });

  it("links neither the files nor the STAC item before the delivery is forwarded", () => {
    visit(delivery(9003, [transfer(ReactionState.Pending, { attempts: 0, nextAttemptAt: "2026-10-23T22:00:00Z" })]));

    cy.dataCy("summary-files").should("contain", "strassen.xtf").find("a").should("not.exist");
    cy.dataCy("summary-stacBrowser").should("contain", "Only available after forwarding");
    cy.dataCy("summary-precursor").should("not.exist");
    cy.dataCy("summary-partialDelivery").should("not.exist");
    getTableRowThatContains("reactions-table", "ablage").should("contain", "Waits until");
  });

  it("shows that the notifications wait for the forwarding", () => {
    visit(
      delivery(9002, [
        transfer(ReactionState.Error, { attempts: 7, message: "The base directory <drop> does not exist." }),
        infoMail(ReactionState.Pending, { nextAttemptAt: "2026-10-23T08:02:00Z" }),
      ]),
    );

    getTableRowThatContains("reactions-table", "info_mail").should("contain", "Waits for the forwarding");
  });

  it("retries a reaction that gave up", () => {
    visit(
      delivery(9002, [
        // A reaction the definition no longer has comes without a name.
        transfer(ReactionState.Error, {
          displayName: null,
          attempts: 7,
          message: "The base directory <drop> does not exist.",
        }),
      ]),
    );
    cy.intercept("POST", "/api/v1/delivery/9002/reactions/ablage/reset", { statusCode: 204 }).as("retry");

    getTableRowThatContains("reactions-table", "ablage")
      .should("not.contain", "File drop")
      .and("contain", "does not exist")
      .find('[data-cy="retryReaction"]')
      .click();

    cy.wait("@retry");
    cy.wait("@delivery");
  });

  it("names the forwarding before a forwarded delivery is deleted", () => {
    visit(
      delivery(9001, [transfer(ReactionState.Done)], { stacItem: { collectionId: "coll_1", itemId: "item_9001" } }),
    );
    cy.intercept("DELETE", "/api/v1/delivery/9001", { statusCode: 200 }).as("deleteDelivery");
    cy.intercept("GET", "/api/v1/delivery", { statusCode: 200, body: [] }).as("deliveries");

    cy.dataCy("delete-button").click();
    handlePrompt(
      "The delivery has already been forwarded to: File drop. The files stay there. Do you still want to delete the delivery data? This action cannot be undone.",
      "delete",
    );

    cy.wait("@deleteDelivery");
    cy.location("pathname").should("eq", "/admin/deliveries");
  });
});
