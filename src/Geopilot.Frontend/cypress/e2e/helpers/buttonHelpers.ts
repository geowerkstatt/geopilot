import { createBaseSelector } from "./appHelpers";

/**
 * Clicks the cancel button.
 * @param parent (optional) The parent of the button.
 */
export const clickCancel = (parent?: string) => {
  const selector = createBaseSelector(parent) + '[data-cy="cancel-button"]';
  cy.get(selector).click();
};
