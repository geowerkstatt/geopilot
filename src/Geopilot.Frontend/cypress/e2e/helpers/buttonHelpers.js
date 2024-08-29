import { createBaseSelector } from "./appHelpers.js";

/**
 * Clicks the cancel button.
 * @param {string} parent (optional) The parent of the button.
 */
export const clickCancel = parent => {
  const selector = createBaseSelector(parent) + '[data-cy="cancel-button"]';
  cy.get(selector).click();
};
