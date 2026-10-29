import { createBaseSelector } from "./appHelpers";

/**
 * Checks if a form element has an error.
 * @param fieldName The name of the form element.
 * @param hasError The expected error state.
 * @param parent (optional) The parent of the form element.
 */
export const hasError = (fieldName: string, hasError = true, parent?: string) => {
  const selector = createBaseSelector(parent) + `[data-cy^="${fieldName}-form"] .Mui-error`;
  if (hasError) {
    cy.get(selector).should("exist");
  } else {
    cy.get(selector).should("not.exist");
  }
};

/**
 * Checks if a form element (except formCheckbox) is disabled.
 * @param fieldName The name of the form element.
 * @param isDisabled The expected disabled state.
 * @param parent (optional) The parent of the form element.
 */
export const isDisabled = (fieldName: string, isDisabled = true, parent?: string) => {
  const selector = createBaseSelector(parent) + `[data-cy^="${fieldName}-form"] .Mui-disabled`;
  if (isDisabled) {
    cy.get(selector).should("exist");
  } else {
    cy.get(selector).should("not.exist");
  }
};

/**
 * Gets a form element.
 * @param fieldName The name of the form element.
 * @param parent (optional) The parent of the form element.
 * @returns
 */
export const getFormField = (fieldName: string, parent?: string) => {
  const selector = createBaseSelector(parent) + `[data-cy^="${fieldName}-form"]`;
  return cy.get(selector);
};

/**
 * Gets a form element's input field.
 * @param fieldName The name of the form element.
 * @param parent (optional) The parent of the form element.
 * @returns
 */
export const getFormInput = (fieldName: string, parent?: string) =>
  getFormField(fieldName, parent).find(`[name=${fieldName}]`);

/**
 * Sets the value for an input form element.
 * @param fieldName The name of the input field.
 * @param value The text to type into the input field.
 * @param parent (optional) The parent of the form element.
 */
export const setInput = (fieldName: string, value: string, parent?: string) => {
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
 * Selects the language that the localized form inputs are edited in.
 * @param language The language code of the tab, for example "de".
 * @param parent (optional) The parent of the language tabs.
 */
export const setFormLanguage = (language: string, parent?: string) => {
  const selector = createBaseSelector(parent) + `[data-cy="language-tab-${language}"]`;
  cy.get(selector).click();
};

/**
 * Evaluates the state of an input form element
 * @param fieldName The name of the input field.
 * @param expectedValue The expected value.
 * @param parent (optional) The parent of the form element.
 */
export const evaluateInput = (fieldName: string, expectedValue: string, parent?: string) => {
  const selector = createBaseSelector(parent) + `[data-cy="${fieldName}-formInput"] input`;
  cy.get(selector)
    .filter((k, input) => {
      return "value" in input && input.value === expectedValue;
    })
    .should("have.length", 1);
};

/**
 * Opens the dropdown for a select form element.
 * @param selector The selector for the form element.
 */
export const openDropdown = (selector: string) => {
  cy.get(selector).find('[role="combobox"]').click();
};

/**
 * Selects an option from a dropdown.
 * @param index The index of the option to select.
 */
export const selectDropdownOption = (index: number) => {
  cy.get('.MuiPaper-elevation [role="listbox"]').find('[role="option"]').eq(index).click();
};

/**
 * Evaluates the number of options in a dropdown.
 * @param length The expected number of options in the dropdown.
 */
export const evaluateDropdownOptionsLength = (length: number) => {
  cy.get('.MuiPaper-elevation [role="listbox"]').should($listbox => {
    expect($listbox.find('[role="option"]')).to.have.length(length);
  });
};

/**
 * Sets the value for a select form element.
 * @param fieldName The name of the select field.
 * @param index The index of the option to select.
 * @param expected (optional) The expected number of options in the dropdown.
 * @param parent (optional) The parent of the form element.
 */
export const setSelect = (fieldName: string, index: number, expected?: number, parent?: string) => {
  const selector = createBaseSelector(parent) + `[data-cy="${fieldName}-formSelect"]`;
  openDropdown(selector);
  if (expected != null) {
    evaluateDropdownOptionsLength(expected);
  }
  selectDropdownOption(index);
};

/**
 * Evaluates the state of a select form element.
 * @param fieldName The name of the select field.
 * @param expectedValueOrPredicate The expected value of the select, or a predicate function that receives the value and returns true if it matches the expectation.
 * @param parent (optional) The parent of the form element.
 */
export const evaluateSelect = (
  fieldName: string,
  expectedValueOrPredicate: string | ((value: string | undefined) => boolean),
  parent?: string,
) => {
  const selector = createBaseSelector(parent) + `[data-cy="${fieldName}-formSelect"] input`;
  cy.get(selector)
    .filter((k, input) => {
      const value = "value" in input ? (input.value as string) : undefined;
      if (typeof expectedValueOrPredicate === "function") {
        return expectedValueOrPredicate(value);
      }
      return value === expectedValueOrPredicate;
    })
    .should("have.length", 1);
};

/**
 * Toggles the checkbox for a checkbox form element.
 * @param fieldName The name of the checkbox field.
 * @param parent (optional) The parent of the form element.
 */
export const toggleCheckbox = (fieldName: string, parent?: string) => {
  const selector = createBaseSelector(parent) + `[data-cy="${fieldName}-formCheckbox"]`;
  cy.get(selector).click();
};

/**
 * Evaluates the state of a checkbox form element.
 * @param fieldName The name of the checkbox field.
 * @param expectedValue The expected value of the checkbox (true for checked, false for unchecked).
 * @param parent (optional) The parent of the form element.
 */
export const evaluateCheckbox = (fieldName: string, expectedValue: boolean, parent?: string) => {
  const selector = createBaseSelector(parent) + `[data-cy="${fieldName}-formCheckbox"] input[type="checkbox"]`;
  cy.get(selector).should("have.prop", "checked", expectedValue);
};

/**
 * Checks if a formCheckbox is disabled.
 * @param fieldName The name of the form element.
 * @param isDisabled The expected disabled state.
 * @param parent  (optional) The parent of the form element.
 */
export const isCheckboxDisabled = (fieldName: string, isDisabled = true, parent?: string) => {
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
 * @param fieldName The name of the autocomplete field.
 * @param parent (optional) The parent of the form element.
 */
export const closeAutocomplete = (fieldName: string, parent?: string) => {
  const selector = createBaseSelector(parent) + `[data-cy="${fieldName}-formAutocomplete"]`;
  cy.get(selector).find("input").first().type("{esc}");
};

/**
 * Sets the value for an autocomplete form element. The user must select from the provided dropdown.
 * For a field that collects free text as chips, use setChipInput instead.
 * @param fieldName The name of the autocomplete field.
 * @param value The text to type into the input field.
 * @param parent (optional) The parent of the form element.
 */
export const setAutocomplete = (fieldName: string, value: string, parent?: string) => {
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
 * Adds a value to a chip input form element, where the user types free text and confirms it.
 * @param fieldName The name of the chip input field.
 * @param value The text to type into the input field.
 * @param confirmKey (optional) The key that confirms the entry, "{enter}" by default.
 * @param parent (optional) The parent of the form element.
 */
export const setChipInput = (fieldName: string, value: string, confirmKey = "{enter}", parent?: string) => {
  const selector = createBaseSelector(parent) + `[data-cy="${fieldName}-formChipInput"]`;
  cy.get(selector)
    .click()
    .then(() => {
      cy.focused().clear();
      if (value.length > 0) {
        cy.get(selector).type(value + confirmKey, {
          delay: 10,
        });
      }
    });
};

/**
 * Pastes text into a chip input form element: sets the value through the prototype setter and fires one input
 * event, the way a paste does. React patches the setter on the element to track changes, so a plain assignment
 * would update that tracking as well and no change would reach the component.
 * @param fieldName The name of the chip input field.
 * @param value The text to paste into the input field.
 * @param parent (optional) The parent of the form element.
 */
export const pasteIntoChipInput = (fieldName: string, value: string, parent?: string) => {
  const selector = createBaseSelector(parent) + `[data-cy="${fieldName}-formChipInput"] input`;
  cy.get(selector).then($input => {
    const input = $input[0];
    const nativeValue = Object.getOwnPropertyDescriptor(Object.getPrototypeOf(input), "value");
    nativeValue!.set!.call(input, value);
    input.dispatchEvent(new input.ownerDocument.defaultView!.Event("input", { bubbles: true }));
  });
};

/**
 * Removes a value from a chip input form element.
 * @param fieldName The name of the chip input field.
 * @param value The value to be deleted.
 * @param parent (optional) The parent of the form element.
 */
export const removeChipInputValue = (fieldName: string, value: string, parent?: string) => {
  const selector = createBaseSelector(parent) + `[data-cy="${fieldName}-selectedChips"]`;
  cy.get(selector).contains(".MuiChip-root", value).find(".MuiChip-deleteIcon").click();
};

/**
 * Evaluates the chips of a chip input form element.
 * @param fieldName The name of the chip input field.
 * @param expectedValues An array of expected values.
 * @param parent (optional) The parent of the form element.
 */
export const evaluateChipInput = (fieldName: string, expectedValues: string[], parent?: string) => {
  const selector = createBaseSelector(parent) + `[data-cy="${fieldName}-selectedChips"]`;
  if (expectedValues.length === 0) {
    // No chips are rendered at all while nothing is selected, so the container itself is absent.
    cy.get(selector).should("not.exist");
    return;
  }
  cy.get(selector).within(() => {
    cy.get(".MuiChip-root").should("have.length", expectedValues.length);
    expectedValues.forEach(value => {
      cy.get(".MuiChip-label").contains(value);
    });
  });
};

/**
 * Removes a selected value from an autocomplete form element.
 * @param fieldName The name of the autocomplete field.
 * @param value The value to be deleted.
 * @param parent (optional) The parent of the form element.
 */
export const removeAutocompleteValue = (fieldName: string, value: string, parent?: string) => {
  const selector = createBaseSelector(parent) + `[data-cy="${fieldName}-formAutocomplete"]`;
  cy.get(selector).contains(".MuiChip-root:visible", value).find(".MuiChip-deleteIcon").click();
};

/**
 * Evaluates the state of an autocomplete form element.
 * @param fieldName The name of the autocomplete field.
 * @param expectedValues An array of expected values.
 * @param parent (optional) The parent of the form element. */
export const evaluateAutocomplete = (fieldName: string, expectedValues: string[], parent?: string) => {
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
