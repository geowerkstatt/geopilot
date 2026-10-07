/**
 * Checks if a prompt is visible.
 * @param visible The expected visibility state.
 */
export const isPromptVisible = (visible = true) => {
  if (visible) {
    cy.dataCy("prompt").should("be.visible");
  } else {
    cy.dataCy("prompt").should("not.exist");
  }
};

/**
 * Checks if a prompt is visible and contains the expected action buttons.
 * @param actions An array of action button labels.
 */
export const checkPromptActions = (actions: string[]) => {
  isPromptVisible();
  cy.dataCy("prompt").within(() => {
    cy.get("button").should("have.length", actions.length);
    actions.forEach(action => {
      cy.dataCy(`prompt-button-${action.toLowerCase()}`).should("exist");
    });
  });
};

/**
 * Handles a prompt by clicking the action button.
 * @param message Name of the prompt message label.
 * @param action Name of the action button label.
 */
export const handlePrompt = (message: string, action: string) => {
  isPromptVisible();
  cy.contains(message);
  cy.dataCy("prompt").dataCy(`prompt-button-${action.toLowerCase()}`).click();
};
