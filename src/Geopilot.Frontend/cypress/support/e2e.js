import { interceptApiCalls } from "../e2e/helpers/appHelpers";

Cypress.on("uncaught:exception", () => {
  return false;
});

beforeEach(() => {
  interceptApiCalls();
});
