import "./commands.js";
import { interceptApiCalls } from "../e2e/helpers/appHelpers";
import "cypress-file-upload";

Cypress.on("uncaught:exception", () => {
  return false;
});

beforeEach(() => {
  interceptApiCalls();
});
