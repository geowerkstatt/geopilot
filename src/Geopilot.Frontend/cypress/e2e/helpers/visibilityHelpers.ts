/** Whether the element, not something laid over it such as the page header, is what shows at that height. */
export const isShownAt = (element: HTMLElement, y: number) => {
  const { left, right } = element.getBoundingClientRect();
  const shown = element.ownerDocument.elementFromPoint((left + right) / 2, y);
  return shown !== null && element.contains(shown);
};

/** Asserts that the whole element is in view and nothing covers its top and bottom edge. */
export const shouldBeFullyShown = (subject: Cypress.Chainable<JQuery<HTMLElement>>) =>
  subject.should(element => {
    const { top, bottom } = element[0].getBoundingClientRect();
    expect(isShownAt(element[0], top + 2), "top edge not covered").to.equal(true);
    expect(isShownAt(element[0], bottom - 2), "bottom edge not covered").to.equal(true);
  });
