const buttonImportRestriction = {
  name: "@mui/material",
  importNames: ["Button", "IconButton"],
  message: "Import Button and IconButton from components/buttons instead of @mui/material.",
};
const formFieldImportRestriction = {
  name: "@mui/material",
  importNames: ["TextField", "Autocomplete", "Checkbox", "Select"],
  message: "Import TextField, Autocomplete, Checkbox and Select from components/form instead of @mui/material.",
};
const muiWrapperImportRestriction = {
  name: "@mui/material",
  importNames: [...buttonImportRestriction.importNames, ...formFieldImportRestriction.importNames],
  message:
    "Import these components from the project wrappers instead of @mui/material: Button and IconButton from " +
    "components/buttons; TextField, Autocomplete, Checkbox and Select from components/form.",
};

module.exports = {
  root: true,
  env: {browser: true, es2020: true},
  extends: [
    "eslint:recommended",
    "plugin:react/recommended",
    "plugin:react/jsx-runtime",
    "plugin:react-hooks/recommended",
    "plugin:prettier/recommended",
    "plugin:@typescript-eslint/eslint-recommended",
    "plugin:@typescript-eslint/recommended",
  ],
  parser: "@typescript-eslint/parser",
  plugins: ["@typescript-eslint", "react-refresh", "prettier", "local-rules"],
  ignorePatterns: ["dist", ".eslintrc.cjs", "eslint-local-rules.cjs", "eslint-rules"],
  parserOptions: {ecmaVersion: "latest", sourceType: "module"},
  settings: {react: {version: "detect"}},
  rules: {
    "prettier/prettier": "error",
    "react-refresh/only-export-components": ["warn", {allowConstantExport: true}],
    "react/react-in-jsx-scope": "off",
    "react/prop-types": "off",
    "react/display-name": "off",
    "local-rules/no-hardcoded-colors": "warn",
    "no-restricted-imports": ["warn", {paths: [muiWrapperImportRestriction]}],
  },
  overrides: [
    {
      files: ["src/appPalette.ts", "cypress/**"],
      rules: {"local-rules/no-hardcoded-colors": "off"},
    },
    {
      files: ["src/components/buttons.tsx"],
      rules: {"no-restricted-imports": ["warn", {paths: [formFieldImportRestriction]}]},
    },
    {
      files: [
        "src/components/form/**",
        "src/components/searchField.tsx",
        "src/pages/delivery/processing/visualizations/layerSwitcher.tsx",
      ],
      rules: {"no-restricted-imports": ["warn", {paths: [buttonImportRestriction]}]},
    },
  ],
};
