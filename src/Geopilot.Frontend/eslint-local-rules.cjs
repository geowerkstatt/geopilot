"use strict";

// Registry of project-local ESLint rules, loaded by eslint-plugin-local-rules.
// Referenced in .eslintrc.cjs as "local-rules/<rule-name>".
module.exports = {
  "no-hardcoded-colors": require("./eslint-rules/no-hardcoded-colors.cjs"),
  "require-theme-radius": require("./eslint-rules/require-theme-radius.cjs"),
};
