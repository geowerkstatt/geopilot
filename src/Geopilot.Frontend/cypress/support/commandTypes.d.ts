declare namespace Cypress {
  interface Chainable {
    dataCy(
      key: string,
      options?: Partial<Loggable & Timeoutable & Withinable & Shadow>,
    ): Chainable<JQuery<HTMLElement>>;
  }
}
