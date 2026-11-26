import type { DeliveryDetailResponse, DeliveryPageResponse, DeliveryResponse } from "@/api/generated";
import { ReactionState } from "@/api/generated";
import { getGridRowThatContains, loginAsAdmin } from "./helpers/appHelpers";
import { handlePrompt } from "./helpers/promptHelpers";

// The seeded deliveries have no reactions, so most tests answer the list with deliveries in different states.
const delivery = (id: number, forwardingState?: ReactionState, notificationState?: ReactionState) =>
  ({
    id,
    date: "2026-10-23T08:00:00Z",
    mandate: { id: 1, name: { de: "Mandat", en: "Mandate" } },
    deliveredByName: "Kelvin Spencer",
    deliveredByClientId: null,
    forwardingState,
    notificationState,
  }) satisfies DeliveryResponse;
const deliveries: DeliveryResponse[] = [
  delivery(9001, ReactionState.Done, ReactionState.Done),
  delivery(9002, ReactionState.Done, ReactionState.Error),
  delivery(9003, ReactionState.Pending, ReactionState.Pending),
  delivery(9004),
];
const detail = (id: number): DeliveryDetailResponse => ({
  id,
  date: "2026-10-23T08:00:00Z",
  mandate: { id: 1, name: { de: "Mandat", en: "Mandate" } },
  deliveredByName: "Kelvin Spencer",
  jobId: "3f2a9c1e-6b7d-4e58-9a0b-2c4d6e8f1a3b",
  files: [],
  comment: "",
  metadata: [],
  reactions: [],
});

const stubList = () =>
  cy
    .intercept(
      { method: "GET", pathname: "/api/v1/delivery" },
      { statusCode: 200, body: { totalCount: deliveries.length, items: deliveries } satisfies DeliveryPageResponse },
    )
    .as("deliveries");

// The latest request, as the grid may ask once more with the previous query while it fits its page to its height.
const latestQuery = (check: (query: URLSearchParams) => void) =>
  cy.get<{ request: { url: string } }[]>("@deliveries.all").should(calls => {
    check(new URL(calls[calls.length - 1].request.url).searchParams);
  });

const searchField = () => cy.dataCy("deliveryFilters").find('input[aria-label="Search by delivery ID"]');

const pickOption = (filter: string, option: string) => {
  cy.dataCy(filter).click();
  cy.get('[role="listbox"] [role="option"]').contains(option).click();
  cy.get("body").type("{esc}");
};

describe("Admin deliveries", () => {
  it("loads the newest deliveries one page at a time from the server", () => {
    cy.intercept({ method: "GET", pathname: "/api/v1/delivery" }).as("deliveries");
    loginAsAdmin();

    cy.visit("/admin/deliveries");

    cy.wait("@deliveries").then(({ request, response }) => {
      const query = new URL(request.url).searchParams;
      expect(query.get("page")).to.equal("0");
      const page: DeliveryPageResponse = response?.body;
      expect(page.items.length).to.be.at.most(Number(query.get("pageSize")));
      expect(page.totalCount).to.be.at.least(page.items.length);
      const dates = page.items.map(item => item.date);
      expect(dates).to.deep.equal([...dates].sort().reverse());
      cy.dataCy("deliveries-grid").find(".MuiDataGrid-row").should("have.length", page.items.length);
    });
  });
});

describe("Admin deliveries with reactions", () => {
  beforeEach(() => {
    stubList();
    loginAsAdmin();
    cy.visit("/admin/deliveries");
    cy.wait("@deliveries");
  });

  it("sums up the forwarding and the notifications of each delivery", () => {
    getGridRowThatContains("deliveries-grid", "9001")
      .find('[data-field="forwardingState"] [data-cy="reactionState-done"]')
      .should("exist");
    getGridRowThatContains("deliveries-grid", "9002")
      .find('[data-field="notificationState"] [data-cy="reactionState-error"]')
      .should("exist");
    getGridRowThatContains("deliveries-grid", "9003")
      .find('[data-field="forwardingState"] [data-cy="reactionState-pending"]')
      .should("exist");
    getGridRowThatContains("deliveries-grid", "9004").find('[data-cy^="reactionState-"]').should("not.exist");
  });

  it("opens the page of a delivery without reactions by a click on its row", () => {
    cy.intercept("GET", "/api/v1/delivery/9004", { statusCode: 200, body: detail(9004) }).as("delivery");

    getGridRowThatContains("deliveries-grid", "9004").find('[data-field="mandateName"]').click();

    cy.wait("@delivery");
    cy.location("pathname").should("eq", "/admin/deliveries/9004");
    cy.go("back");
    cy.location("pathname").should("eq", "/admin/deliveries");
    cy.dataCy("reactions-table").should("not.exist");
  });
});

describe("Admin delivery filters", () => {
  beforeEach(() => {
    stubList();
    loginAsAdmin();
  });

  it("puts filters and search into the address and the query", () => {
    cy.visit("/admin/deliveries");
    cy.wait("@deliveries");

    pickOption("deliveryFilter-failed", "Forwarding");
    pickOption("deliveryFilter-failed", "Notifications");
    latestQuery(query => {
      expect(query.getAll("failedReactionKinds")).to.deep.equal(["transfer", "notification"]);
      expect(query.get("page")).to.equal("0");
    });
    cy.location("search").should(search => {
      expect(new URLSearchParams(search).getAll("failed")).to.deep.equal(["transfer", "notification"]);
    });

    searchField().type("12");
    latestQuery(query => expect(query.get("idPrefix")).to.equal("12"));
    cy.location("search").should("contain", "id=12");

    // Text that holds more than digits leaves the list as it is, and the field as it was typed.
    searchField().type("a");
    searchField().should("have.value", "12a");
    cy.wait(400);
    latestQuery(query => expect(query.get("idPrefix")).to.equal("12"));
    cy.location("search").should("contain", "id=12");

    cy.dataCy("resetFilters-button").click();
    searchField().should("have.value", "");
    cy.location("search").should("equal", "");
    latestQuery(query => {
      expect(query.has("idPrefix")).to.equal(false);
      expect(query.has("failedReactionKinds")).to.equal(false);
    });
  });

  it("sends the chosen supplier and mandate and returns to the first page", () => {
    cy.visit("/admin/deliveries?page=2");
    cy.wait("@deliveries");

    cy.dataCy("deliveryFilter-deliveredBy").click();
    cy.get('[role="listbox"] [role="option"]').first().click();
    cy.get("body").type("{esc}");
    cy.dataCy("deliveryFilter-mandate").click();
    cy.get('[role="listbox"] [role="option"]').first().click();
    cy.get("body").type("{esc}");

    cy.location("search").should("match", /^\?deliveredBy=(user|machineClient):\d+&mandate=\d+$/);
    cy.location("search").then(search => {
      const address = new URLSearchParams(search);
      const [kind, supplierId] = (address.get("deliveredBy") ?? "").split(":");
      latestQuery(query => {
        expect(query.getAll(kind === "user" ? "userIds" : "machineClientIds")).to.deep.equal([supplierId]);
        expect(query.getAll("mandateIds")).to.deep.equal([address.get("mandate")]);
        expect(query.get("page")).to.equal("0");
      });
    });
  });

  it("sends the period and who delivered from the address", () => {
    cy.visit(
      "/admin/deliveries?from=2026-10-28T08:00&to=2026-10-30T23:59&deliveredBy=user:7&deliveredBy=machineClient:3",
    );

    cy.wait("@deliveries").then(({ request }) => {
      const query = new URL(request.url).searchParams;
      expect(query.get("from")).to.equal(new Date("2026-10-28T08:00:00").toISOString());
      // The last minute is included, so the period ends where the next minute starts.
      expect(query.get("to")).to.equal(new Date("2026-10-31T00:00:00").toISOString());
      expect(query.getAll("userIds")).to.deep.equal(["7"]);
      expect(query.getAll("machineClientIds")).to.deep.equal(["3"]);
    });
  });

  it("sets no filter with what it cannot read in the address", () => {
    cy.visit(
      "/admin/deliveries?failed=transfer&failed=bogus&failed=error&mandate=abc&deliveredBy=foo" +
        "&deliveredBy=user:7&id=1a&from=xyz&page=-1",
    );

    cy.wait("@deliveries").then(({ request }) => {
      const query = new URL(request.url).searchParams;
      expect(query.getAll("failedReactionKinds")).to.deep.equal(["transfer"]);
      expect(query.getAll("userIds")).to.deep.equal(["7"]);
      ["mandateIds", "machineClientIds", "idPrefix", "from"].forEach(name =>
        expect(query.has(name), name).to.equal(false),
      );
      expect(query.get("page")).to.equal("0");
    });
    searchField().should("have.value", "");
  });

  it("returns from a delivery and after deleting it to the filtered list", () => {
    const filtered = "?failed=transfer&id=900";
    cy.intercept("GET", "/api/v1/delivery/9001", { statusCode: 200, body: detail(9001) }).as("delivery");
    cy.intercept("DELETE", "/api/v1/delivery/9001", { statusCode: 200 }).as("deleteDelivery");
    cy.visit(`/admin/deliveries${filtered}`);
    cy.wait("@deliveries");
    searchField().should("have.value", "900");

    getGridRowThatContains("deliveries-grid", "9001").find('[data-cy="details"]').click();
    cy.wait("@delivery");
    cy.dataCy("backToDeliveries-button").click();

    cy.location("pathname").should("eq", "/admin/deliveries");
    cy.location("search").should("equal", filtered);
    latestQuery(query => {
      expect(query.getAll("failedReactionKinds")).to.deep.equal(["transfer"]);
      expect(query.get("idPrefix")).to.equal("900");
    });
    searchField().should("have.value", "900");

    getGridRowThatContains("deliveries-grid", "9001").find('[data-field="mandateName"]').click();
    cy.wait("@delivery");
    cy.dataCy("delete-button").click();
    handlePrompt("Do you really want to delete the delivery data? This action cannot be undone.", "delete");
    cy.wait("@deleteDelivery");

    cy.location("pathname").should("eq", "/admin/deliveries");
    cy.location("search").should("equal", filtered);
  });

  it("shows the whole list when reached through the menu", () => {
    cy.intercept("GET", "/api/v1/delivery/9001", { statusCode: 200, body: detail(9001) }).as("delivery");
    cy.visit("/admin/deliveries?failed=transfer");
    cy.wait("@deliveries");
    getGridRowThatContains("deliveries-grid", "9001").find('[data-field="mandateName"]').click();
    cy.wait("@delivery");

    cy.dataCy("admin-deliveries-nav").click();

    cy.location("pathname").should("eq", "/admin/deliveries");
    cy.location("search").should("equal", "");
    latestQuery(query => expect(query.has("failedReactionKinds")).to.equal(false));
  });

  it("clears filters and search when the menu is chosen on the filtered list", () => {
    cy.visit("/admin/deliveries?failed=transfer&id=900");
    cy.wait("@deliveries");
    searchField().should("have.value", "900");

    cy.dataCy("admin-deliveries-nav").click();

    cy.location("search").should("equal", "");
    searchField().should("have.value", "");
    latestQuery(query => {
      expect(query.has("failedReactionKinds")).to.equal(false);
      expect(query.has("idPrefix")).to.equal(false);
    });
  });
});
