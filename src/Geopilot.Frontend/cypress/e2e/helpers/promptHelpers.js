/**
 * Checks if a prompt is visible.
 * @param {boolean} visible The expected visibility state.
 */
export const isPromptVisible = (visible = true) => {
  if (visible) {
    cy.get('[data-cy="prompt"]').should("be.visible");
  } else {
    cy.get('[data-cy="prompt"]').should("not.exist");
  }
};

/**
 * Checks if a prompt is visible and contains the expected action buttons.
 * @param {string[]} actions An array of action button labels.
 */
export const checkPromptActions = actions => {
  isPromptVisible();
  cy.get('[data-cy="prompt"]').within(() => {
    cy.get("button").should("have.length", actions.length);
    actions.forEach(action => {
      cy.get(`[data-cy="prompt-button-${action.toLowerCase()}"]`).should("exist");
    });
  });
};

/**
 * Handles a prompt by clicking the action button.
 * @param {string} message Name of the prompt message label.
 * @param {string} action Name of the action button label.
 */
export const handlePrompt = (message, action) => {
  isPromptVisible();
  cy.contains(message);
  cy.get('[data-cy="prompt"]').find(`[data-cy="prompt-button-${action.toLowerCase()}"]`).click();
};
