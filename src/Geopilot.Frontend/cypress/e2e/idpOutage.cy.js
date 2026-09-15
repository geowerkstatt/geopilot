import { loginAsAdmin } from "./helpers/appHelpers.js";

const expectOidcSession = exists => {
  cy.window().should(win => {
    const hasSession = Object.keys(win.localStorage).some(key => key.startsWith("oidc.user:"));
    expect(hasSession).to.eq(exists);
  });
};

describe("Identity provider outage", () => {
  it("keeps the user logged in when the API answers 503 during the session", () => {
    loginAsAdmin();

    cy.intercept("/api/v1/**", { statusCode: 503 }).as("outage");
    cy.dataCy("loggedInUser-button").click();
    cy.dataCy("admin-nav").click();
    cy.wait("@outage");

    cy.dataCy("loggedInUser-button").should("exist");
    expectOidcSession(true);
  });

  it("keeps the session when the user info request answers 503 on reload", () => {
    loginAsAdmin();

    cy.intercept(
      { pathname: "/api/v1/user/self", times: 1 },
      {
        statusCode: 503,
        body: {
          status: 503,
          title: "Authentication unavailable",
          detail: "Authentication currently not possible.",
        },
      },
    ).as("selfOutage");
    cy.reload();
    cy.wait("@selfOutage");

    cy.dataCy("logIn-button").should("not.exist");
    cy.dataCy("delivery").should("exist");
    cy.contains("Could not load user information: Authentication currently not possible.");
    expectOidcSession(true);

    cy.dataCy("loggedInUser-button", { timeout: 15_000 }).should("exist");
  });

  it("signs the user out when the user info request answers 401 on reload", () => {
    loginAsAdmin();

    cy.intercept({ pathname: "/api/v1/user/self", times: 1 }, { statusCode: 401 }).as("selfRejected");
    cy.reload();
    cy.wait("@selfRejected");

    expectOidcSession(false);
    cy.dataCy("loggedInUser-button").should("not.exist");
  });
});
