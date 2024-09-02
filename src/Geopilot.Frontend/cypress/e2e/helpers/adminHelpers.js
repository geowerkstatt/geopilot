import { createBaseSelector, isSelectedNavItem } from "./appHelpers.js";

/**
 * Selects an admin navigation item.
 * @param item The item to select (delivery-overview, users, mandates, organisations).
 */
export const selectAdminNavItem = item => {
  const selector = createBaseSelector("admin-navigation") + `[data-cy="admin-${item}-nav"]`;
  cy.get(selector).click();
  isSelectedNavItem(`admin-${item}-nav`, "admin-navigation");
  cy.location().should(location => {
    expect(location.pathname).to.eq(`/admin/${item}`);
  });
};
