Cypress.Commands.add("dataCy", key => {
  return cy.get(`[data-cy=${key}]`);
});
