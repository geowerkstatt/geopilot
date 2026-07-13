import { createBaseSelector } from "./appHelpers";

/**
 * Checks if a form element has an error.
 * @param {string} fieldName The name of the form element.
 * @param {boolean} hasError The expected error state.
 * @param {string} parent  (optional) The parent of the form element.
 */
export const hasError = (fieldName, hasError = true, parent) => {
  const selector = createBaseSelector(parent) + `[data-cy^="${fieldName}-form"] .Mui-error`;
  if (hasError) {
    cy.get(selector).should("exist");
  } else {
    cy.get(selector).should("not.exist");
  }
};

/**
 * Checks if a form element (except formCheckbox) is disabled.
 * @param {string} fieldName The name of the form element.
 * @param {boolean} isDisabled The expected disabled state.
 * @param {string} parent  (optional) The parent of the form element.
 */
export const isDisabled = (fieldName, isDisabled = true, parent) => {
  const selector = createBaseSelector(parent) + `[data-cy^="${fieldName}-form"] .Mui-disabled`;
  if (isDisabled) {
    cy.get(selector).should("exist");
  } else {
    cy.get(selector).should("not.exist");
  }
};

/**
 * Gets a form element.
 * @param {any} fieldName The name of the form element.
 * @param {any} parent (optional) The parent of the form element.
 * @returns
 */
export const getFormField = (fieldName, parent) => {
  const selector = createBaseSelector(parent) + `[data-cy^="${fieldName}-form"]`;
  return cy.get(selector);
};

/**
 * Gets a form element's input field.
 * @param {any} fieldName The name of the form element.
 * @param {any} parent (optional) The parent of the form element.
 * @returns
 */
export const getFormInput = (fieldName, parent) => getFormField(fieldName, parent).find(`[name=${fieldName}]`);

/**
 * Sets the value for an input form element.
 * @param {string} fieldName The name of the input field.
 * @param {string} value The text to type into the input field.
 * @param {string} parent (optional) The parent of the form element.
 */
export const setInput = (fieldName, value, parent) => {
  const selector = createBaseSelector(parent) + `[data-cy="${fieldName}-formInput"]`;
  cy.get(selector)
    .click()
    .then(() => {
      cy.focused().clear();
      if (value.length > 0) {
        cy.get(selector).type(value, {
          delay: 10,
        });
      }
    });
};

/**
 * Evaluates the state of an input form element
 * @param {string} fieldName The name of the input field.
 * @param {number} expectedValue The expected value.
 * @param {string} parent (optional) The parent of the form element.
 */
export const evaluateInput = (fieldName, expectedValue, parent) => {
  const selector = createBaseSelector(parent) + `[data-cy="${fieldName}-formInput"] input`;
  cy.get(selector)
    .filter((k, input) => {
      return input.value === expectedValue;
    })
    .should("have.length", 1);
};

/**
 * Opens the dropdown for a select form element.
 * @param {string} selector The selector for the form element.
 */
export const openDropdown = selector => {
  cy.get(selector).find('[role="combobox"]').click();
};

/**
 * Selects an option from a dropdown.
 * @param {number} index The index of the option to select.
 */
export const selectDropdownOption = index => {
  cy.get('.MuiPaper-elevation [role="listbox"]').find('[role="option"]').eq(index).click();
};

/**
 * Evaluates the number of options in a dropdown.
 * @param {number} length The expected number of options in the dropdown.
 */
export const evaluateDropdownOptionsLength = length => {
  cy.get('.MuiPaper-elevation [role="listbox"]').should($listbox => {
    expect($listbox.find('[role="option"]')).to.have.length(length);
  });
};

/**
 * Sets the value for a select form element.
 * @param {string} fieldName The name of the select field.
 * @param {number} index The index of the option to select.
 * @param {number} expected (optional) The expected number of options in the dropdown.
 * @param {string} parent (optional) The parent of the form element.
 */
export const setSelect = (fieldName, index, expected, parent) => {
  const selector = createBaseSelector(parent) + `[data-cy="${fieldName}-formSelect"]`;
  openDropdown(selector);
  if (expected != null) {
    evaluateDropdownOptionsLength(expected);
  }
  selectDropdownOption(index);
};

/**
 * Evaluates the state of a select form element.
 * @param {string} fieldName The name of the select field.
 * @param {string|function} expectedValueOrPredicate The expected value of the select, or a predicate function that receives the value and returns true if it matches the expectation.
 * @param {string} parent (optional) The parent of the form element.
 */
export const evaluateSelect = (fieldName, expectedValueOrPredicate, parent) => {
  var selector = createBaseSelector(parent) + `[data-cy="${fieldName}-formSelect"] input`;
  cy.get(selector)
    .filter((k, input) => {
      if (typeof expectedValueOrPredicate === "function") {
        return expectedValueOrPredicate(input.value);
      }
      return input.value === expectedValueOrPredicate;
    })
    .should("have.length", 1);
};

/**
 * Toggles the checkbox for a checkbox form element.
 * @param {string} fieldName The name of the checkbox field.
 * @param {string} parent (optional) The parent of the form element.
 */
export const toggleCheckbox = (fieldName, parent) => {
  const selector = createBaseSelector(parent) + `[data-cy="${fieldName}-formCheckbox"]`;
  cy.get(selector).click();
};

/**
 * Evaluates the state of a checkbox form element.
 * @param {string} fieldName The name of the checkbox field.
 * @param {boolean} expectedValue The expected value of the checkbox (true for checked, false for unchecked).
 * @param {string} parent (optional) The parent of the form element.
 */
export const evaluateCheckbox = (fieldName, expectedValue, parent) => {
  const selector = createBaseSelector(parent) + `[data-cy="${fieldName}-formCheckbox"] input[type="checkbox"]`;
  cy.get(selector).should("have.prop", "checked", expectedValue);
};

/**
 * Checks if a formCheckbox is disabled.
 * @param {string} fieldName The name of the form element.
 * @param {boolean} isDisabled The expected disabled state.
 * @param {string} parent  (optional) The parent of the form element.
 */
export const isCheckboxDisabled = (fieldName, isDisabled = true, parent) => {
  const selector = createBaseSelector(parent) + `[data-cy^="${fieldName}-form"].Mui-disabled`;
  if (isDisabled) {
    cy.get(selector).should("exist");
  } else {
    cy.get(selector).should("not.exist");
  }
};

/**
 * Closes an open autocomplete dropdown. The dropdown stays open after selecting a value
 * (disableCloseOnSelect), so it must be dismissed explicitly or it covers elements clicked next.
 * @param {string} fieldName The name of the autocomplete field.
 * @param {string} parent (optional) The parent of the form element.
 */
export const closeAutocomplete = (fieldName, parent) => {
  const selector = createBaseSelector(parent) + `[data-cy="${fieldName}-formAutocomplete"]`;
  cy.get(selector).find("input").first().type("{esc}");
};

/**
 * Sets the value for an autocomplete form element, that is not free solo. Meaning the user must select from the provided dropdown.
 * For free solo autocomplete, use setFreeSoloAutocomplete instead.
 * @param {string} fieldName The name of the autocomplete field.
 * @param {string} value The text to type into the input field.
 * @param {string} parent (optional) The parent of the form element.
 */
export const setNonFreeSoloAutocomplete = (fieldName, value, parent) => {
  const selector = createBaseSelector(parent) + `[data-cy="${fieldName}-formAutocomplete"]`;
  cy.get(selector)
    .click()
    .then(() => {
      cy.get(selector).type(value, {
        delay: 10,
      });
      cy.get('.MuiPaper-elevation [role="listbox"]').find('[role="option"]').first().click();
      closeAutocomplete(fieldName, parent);
    });
};

/**
 * Sets the value for an autocomplete form element, that is free solo. Meaning the user can type any value, not just select from the provided dropdown.
 * @param {string} fieldName The name of the autocomplete field.
 * @param {string} value The text to type into the input field.
 * @param {string} parent (optional) The parent of the form element.
 */
export const setFreeSoloAutocomplete = (fieldName, value, parent) => {
  const selector = createBaseSelector(parent) + `[data-cy="${fieldName}-formAutocomplete"]`;
  cy.get(selector)
    .click()
    .then(() => {
      cy.focused().clear();
      if (value.length > 0) {
        cy.get(selector).type(value + "{enter}", {
          delay: 10,
        });
      }
      closeAutocomplete(fieldName, parent);
    });
};

/**
 * Removes a selected value from an autocomplete form element.
 * @param {string} fieldName The name of the autocomplete field.
 * @param {string} value The value to be deleted.
 * @param {string} parent (optional) The parent of the form element.
 */
export const removeAutocompleteValue = (fieldName, value, parent) => {
  const selector = createBaseSelector(parent) + `[data-cy="${fieldName}-formAutocomplete"]`;
  cy.get(selector).contains(".MuiChip-root:visible", value).find(".MuiChip-deleteIcon").click();
};

/**
 * Evaluates the state of an autocomplete form element.
 * @param {string} fieldName The name of the autocomplete field.
 * @param {string[]} expectedValues An array of expected values.
 * @param {string} parent (optional) The parent of the form element. */
export const evaluateAutocomplete = (fieldName, expectedValues, parent) => {
  const selector = createBaseSelector(parent) + `[data-cy="${fieldName}-formAutocomplete"]`;
  cy.get(selector).within(() => {
    // The visible row collapses overflowing chips into a "+N" chip, so not every selected value is shown there.
    // Assert against the hidden (visibility: hidden) measurement row instead: it always renders exactly one chip
    // per selected value, so the count and the values stay correct regardless of how many chips currently fit.
    cy.get(".MuiChip-root:hidden").should("have.length", expectedValues.length);
    expectedValues.forEach(value => {
      cy.get(".MuiChip-root:hidden span").contains(value);
    });
  });
};
