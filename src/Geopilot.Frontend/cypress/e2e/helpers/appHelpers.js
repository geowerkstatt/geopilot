export const interceptApiCalls = () => {
  cy.intercept("/api/v1/user/auth").as("auth");
  cy.intercept("/api/v1/version").as("version");
  cy.intercept("/api/v1/user/self").as("self");
  cy.intercept("terms-of-use.md", {
    statusCode: 200,
    fixture: "../fixtures/terms-of-use.md",
  }).as("termsOfUse");
};

/**
 * Logs in a user and sets the session token.
 * @param user
 */
export const login = user => {
  cy.session(
    ["login", user],
    () => {
      cy.intercept("http://localhost:4011/realms/geopilot/protocol/openid-connect/token").as("token");
      cy.visit("/");
      cy.wait("@termsOfUse");
      cy.get('[data-cy="logIn-button"]').click();
      cy.origin("http://localhost:4011", { args: { user } }, ({ user }) => {
        cy.get("#username").type(user);
        cy.get("#password").type("geopilot_password");
        cy.get("[type=submit]").click({ force: true });
      });
      cy.wait("@token")
        .then(interception => interception.response.body.id_token)
        .then(token => window.localStorage.setItem("id_token", token));
    },
    {
      validate() {
        cy.window()
          .then(win => win.localStorage.getItem("id_token"))
          .as("id_token");
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

/**
 * Logs in with the admin user and navigates to the home page.
 */
export const loginAsAdmin = () => {
  login("admin");
  cy.visit("/");
  cy.get('[data-cy="loggedInUser-button"]').should("exist");
};

/**
 * Logs in with the uploader user and navigates to the home page.
 */
export const loginAsUploader = () => {
  login("uploader");
  cy.visit("/");
  cy.get('[data-cy="loggedInUser-button"]').should("exist");
  cy.wait("@termsOfUse");
};

/**
 * Logs in with the new user and navigates to the home page.
 */
export const loginAsNewUser = () => {
  login("newuser");
  cy.visit("/");
  cy.get('[data-cy="loggedInUser-button"]').should("exist");
};

/**
 * Loads the application without authentication so that no login is available.
 */
export const loadWithoutAuth = () => {
  cy.visit("/");
  cy.intercept("/api/v1/user/auth", {
    statusCode: 200,
    body: { authority: "", clientId: "" },
  });
};

/**
 * Logs out the user.
 */
export const logout = () => {
  openToolMenu();
  cy.get('[data-cy="logOut-button"]').click();
};

/**
 * Selects a language from the language selector.
 * @param language The language to select (de, fr, it, en).
 */
export const selectLanguage = language => {
  cy.get('[data-cy="language-selector"]').click({ force: true });
  cy.get(`[data-cy="language-${language.toLowerCase()}"]`).click({ force: true });
  cy.wait(1000);
};

/**
 * Creates a base selector for an element with an optional parent.
 * @param {string} parent  (optional) The parent of the element.
 * @returns {string} The base selector.
 */
export const createBaseSelector = parent => {
  if (parent) {
    return `[data-cy="${parent}"] `;
  } else {
    return "";
  }
};

/**
 * Opens the tool navigation. Requires the user to be logged in.
 */
export const openToolMenu = () => {
  if (!cy.get('[data-cy="tool-navigation"]').should("be.visible")) {
    cy.get('[data-cy="loggedInUser-button"]').click();
  }
};

/**
 * Opens the tool navigation to switch between delivery, administation and stac browser. Requires the user to be logged in.
 * @param tool The tool to open (delivery, admin, stacBrowser).
 */
export const openTool = tool => {
  openToolMenu();
  cy.get(`[data-cy="${tool}-nav"]`).click();
};

/**
 * Checks if a navigation item is selected.
 * @param item The item to check.
 * @param {string} parent  (optional) The parent of the item.
 */
export const isSelectedNavItem = (item, parent) => {
  const selector = createBaseSelector(parent) + `[data-cy="${item}"]`;
  cy.get(selector).should("have.class", "Mui-selected");
};
