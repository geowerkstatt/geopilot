export const interceptApiCalls = () => {
  cy.intercept("/api/v1/user/auth").as("auth");
  cy.intercept("/api/v1/version").as("version");
  cy.intercept("/api/v1/user/self").as("self");
  cy.intercept("terms-of-use*.md").as("termsOfUse");
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
      cy.dataCy("logIn-button").click();
      cy.origin("http://localhost:4011", { args: { user } }, ({ user }) => {
        cy.get("#username").type(user);
        cy.get("#password").type("geopilot_password");
        cy.get("[type=submit]").click({ force: true });
      });
      cy.wait("@token");
    },
    {
      validate() {
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
  cy.dataCy("loggedInUser-button").should("exist");
};

/**
 * Logs in with the uploader user and navigates to the home page.
 */
export const loginAsUploader = () => {
  login("uploader");
  cy.visit("/");
  cy.dataCy("loggedInUser-button").should("exist");
  cy.wait("@termsOfUse");
};

/**
 * Logs in with the new user and navigates to the home page.
 */
export const loginAsNewUser = () => {
  login("newuser");
  cy.visit("/");
  cy.dataCy("loggedInUser-button").should("exist");
};

/**
 * Loads the application without authentication so that no login is available.
 */
export const loadWithoutAuth = () => {
  cy.intercept("/api/v1/user/auth", {
    statusCode: 200,
    body: { authority: "", clientId: "" },
  });
  cy.visit("/");
};

/**
 * Logs out the user.
 */
export const logout = () => {
  openToolMenu();
  cy.dataCy("logOut-button").click();
};

/**
 * Selects a language from the language selector.
 * @param language The language to select (de, fr, it, en).
 */
export const selectLanguage = language => {
  // IMPORTANT: This wait is necessary due to React component initialization timing
  // issues (suspicion being languagePopup.tsx useEffect). We've tried multiple
  // alternatives but only this approach works reliably.
  cy.wait(200);

  cy.dataCy("language-selector").click();
  cy.dataCy(`language-${language.toLowerCase()}`).should("be.visible").click();
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
  cy.get("body").then($body => {
    const elementExists = $body.find('[data-cy="tool-navigation"]').length > 0;
    if (!elementExists) {
      cy.dataCy("loggedInUser-button").click();
    }
  });
};

/**
 * Opens the tool navigation to switch between delivery, administation and stac browser. Requires the user to be logged in.
 * @param tool The tool to open (delivery, admin, stacBrowser).
 */
export const openTool = tool => {
  openToolMenu();
  cy.dataCy(`${tool}-nav`).click();
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

export const getGridRowThatContains = (grid, text) => {
  return cy.dataCy(grid).find(".MuiDataGrid-row").contains(text).parents(".MuiDataGrid-row");
};
