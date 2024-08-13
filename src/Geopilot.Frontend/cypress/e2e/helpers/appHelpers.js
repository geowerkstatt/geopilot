export const interceptApiCalls = () => {
  cy.intercept("/api/v1/user/auth").as("auth");
  cy.intercept("/api/v1/version").as("version");
  cy.intercept("/api/v1/user/self").as("self");
};

export const login = user => {
  cy.session(
    ["login", user],
    () => {
      cy.visit("/");
      cy.wait("@version");
      cy.get('[data-cy="login-button"]').click({ force: true });
      cy.origin("http://localhost:4011", { args: { user } }, ({ user }) => {
        cy.get("#username").type(user);
        cy.get("#password").type("geopilot_password");
        cy.get("[type=submit]").click({ force: true });
      });
    },
    {
      validate() {
        cy.getCookie("geopilot.auth").should("exist");
        cy.request({
          url: "/api/v1/user/self",
          failOnStatusCode: false,
        }).then(response => {
          expect(response.status).to.eq(200);
        });
      },
    },
  );
};

export const loginAsAdmin = () => {
  login("admin");
  cy.visit("/");
};

export const loginAsEditor = () => {
  login("user");
  cy.visit("/");
};

export const logout = () => {
  cy.get('[data-cy="loggedInUser-button"]').click();
  cy.get('[data-cy="logout-button"]').click();
};

export const selectLanguage = language => {
  cy.get('[data-cy="language-selector"]').click({ force: true });
  cy.get(`[data-cy="language-${language.toLowerCase()}"]`).click({ force: true });
  cy.wait(1000);
};
