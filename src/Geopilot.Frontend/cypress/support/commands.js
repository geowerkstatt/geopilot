Cypress.Commands.add("dataCy", { prevSubject: "optional" }, (subject, key) => {
  if (subject) {
    return cy.wrap(subject).find(`[data-cy=${key}]`);
  }
  return cy.get(`[data-cy=${key}]`);
});
