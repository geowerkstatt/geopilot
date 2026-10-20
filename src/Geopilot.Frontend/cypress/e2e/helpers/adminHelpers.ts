import { createBaseSelector, isSelectedNavItem } from "./appHelpers";

/**
 * Selects an admin navigation item.
 * @param item The item to select.
 */
export const selectAdminNavItem = (
  item: "delivery-overview" | "users" | "mandates" | "organisations" | "machine-clients",
) => {
  const selector = createBaseSelector("admin-navigation") + `[data-cy="admin-${item}-nav"]`;
  cy.get(selector).click();
  isSelectedNavItem(`admin-${item}-nav`, "admin-navigation");
  cy.location().should(location => {
    expect(location.pathname).to.eq(`/admin/${item}`);
  });
};
