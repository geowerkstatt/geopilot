Cypress.Commands.add("dataCy", { prevSubject: "optional" }, (subject, key, options) => {
  if (subject) {
    return cy.wrap(subject).find(`[data-cy="${key}"]`, options);
  }
  return cy.get(`[data-cy="${key}"]`, options);
});
