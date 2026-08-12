import js from "@eslint/js";
import prettierRecommended from "eslint-plugin-prettier/recommended";
import react from "eslint-plugin-react";
import reactHooks from "eslint-plugin-react-hooks";
import reactRefresh from "eslint-plugin-react-refresh";
import { defineConfig, globalIgnores } from "eslint/config";
import globals from "globals";
import tseslint from "typescript-eslint";
import noHardcodedColors from "./eslint-rules/no-hardcoded-colors.cjs";
import requirePx2rem from "./eslint-rules/require-px2rem.cjs";
import requireThemeRadius from "./eslint-rules/require-theme-radius.cjs";

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

const localRules = {
  rules: {
    "no-hardcoded-colors": noHardcodedColors,
    "require-theme-radius": requireThemeRadius,
    "require-px2rem": requirePx2rem,
  },
};

export default defineConfig([
  globalIgnores(["**/dist", "tsconfig.json", "eslint.config.mjs", "**/.vscode", "devPublic", "eslint-rules"]),
  js.configs.recommended,
  tseslint.configs.recommended,
  react.configs.flat.recommended,
  react.configs.flat["jsx-runtime"],
  reactRefresh.configs.vite,
  reactHooks.configs.flat.recommended,
  prettierRecommended,
  {
    plugins: { "local-rules": localRules },
    languageOptions: {
      globals: globals.browser,
      parserOptions: { ecmaVersion: "latest", sourceType: "module" },
    },
    settings: { react: { version: "detect" } },
    rules: {
      "react/prop-types": "off",
      "react/display-name": "off",
      "local-rules/no-hardcoded-colors": "warn",
      "local-rules/require-theme-radius": "warn",
      "local-rules/require-px2rem": "warn",
      "no-restricted-imports": ["warn", { paths: [muiWrapperImportRestriction] }],
    },
  },
  {
    // TODO: Existing violations of the React Compiler rules that came with eslint-plugin-react-hooks v7.
    // Suppressed per file so the rules still guard new code. Remove each entry once the file is fixed,
    // most cases derive state during render instead of syncing it in an effect.
    files: [
      "src/appContext.tsx",
      "src/auth/userProvider.tsx",
      "src/components/alert/alertProvider.tsx",
      "src/components/fileDropzone.tsx",
      "src/components/visualizations/map/layerSwitcherRow.tsx",
      "src/components/visualizations/tree/treeVisualization.tsx",
      "src/components/visualizations/visualizationLoader.tsx",
      "src/pages/admin/mandates/mandateDetail.tsx",
      "src/pages/admin/organisations/organisationDetail.tsx",
      "src/pages/admin/users/userDetail.tsx",
      "src/pages/delivery/deliveryContentCarousel.tsx",
      "src/pages/delivery/deliveryProcessing.tsx",
      "src/pages/delivery/deliveryProvider.tsx",
      "src/pages/delivery/deliverySelectMandate.tsx",
    ],
    rules: { "react-hooks/set-state-in-effect": "off" },
  },
  {
    files: ["src/appPalette.ts", "cypress/**"],
    rules: { "local-rules/no-hardcoded-colors": "off" },
  },
  {
    files: ["src/appTheme.ts", "cypress/**"],
    rules: { "local-rules/require-theme-radius": "off" },
  },
  {
    files: ["cypress/**"],
    rules: {
      "no-undef": "off",
      // Chai assertions such as `expect(x).to.be.true` are bare expressions by design.
      "@typescript-eslint/no-unused-expressions": "off",
    },
  },
  {
    files: ["src/components/buttons.tsx"],
    rules: { "no-restricted-imports": ["warn", { paths: [formFieldImportRestriction] }] },
  },
  {
    files: ["src/components/form/**", "src/components/searchField.tsx"],
    rules: { "no-restricted-imports": ["warn", { paths: [buttonImportRestriction] }] },
  },
  {
    files: ["**/*.json"],
    rules: {
      "@typescript-eslint/no-unused-expressions": "off",
    },
  },
]);
