import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

// Checks a locale file (public/locale/<lng>/common.json) against the English reference: every
// language must define exactly the reference keys, and every key must be referenced in src as a
// double-quoted string literal ("key"). Keys are passed around as props (label="save") before they
// reach t(), so the scan matches the literal anywhere rather than only inside t() calls. The
// translation types in src/i18next.d.ts already reject keys that are used but not defined.

const REFERENCE_PATH = fileURLToPath(new URL("../public/locale/en/common.json", import.meta.url));
const SOURCE_DIR = fileURLToPath(new URL("../src", import.meta.url));
const SOURCE_FILE = /\.(?:ts|tsx|js|jsx)$/;

// i18next stores plural forms as <key>_one, <key>_other, ... while code references only <key>.
const PLURAL_SUFFIX = /_(?:zero|one|two|few|many|other)$/;

const readSources = dir =>
  fs
    .readdirSync(dir, { withFileTypes: true })
    .flatMap(entry => {
      const entryPath = path.join(dir, entry.name);
      if (entry.isDirectory()) {
        return readSources(entryPath);
      }
      return SOURCE_FILE.test(entry.name) ? [fs.readFileSync(entryPath, "utf8")] : [];
    })
    .join("\n");

const getKeyName = property =>
  property.type === "Property" && property.key.type === "Literal" ? String(property.key.value) : undefined;

/** @type {import("eslint").Rule.RuleModule} */
const rule = {
  meta: {
    type: "problem",
    docs: {
      description: "Require every locale to define the same keys as English and every key to be used in src",
    },
    messages: {
      missingKey: "Key '{{ key }}' is defined in en/common.json but missing here.",
      unknownKey: "Key '{{ key }}' is not defined in en/common.json. Add it there or remove it here.",
      unusedKey: "Key '{{ key }}' is not used in src. Remove it from all locales.",
    },
    schema: [],
  },
  create(context) {
    return {
      "Program > ExpressionStatement > ObjectExpression"(node) {
        const referenceKeys = new Set(Object.keys(JSON.parse(fs.readFileSync(REFERENCE_PATH, "utf8"))));
        const sources = readSources(SOURCE_DIR);
        const keys = new Set();

        for (const property of node.properties) {
          const key = getKeyName(property);
          if (key === undefined) {
            continue;
          }
          keys.add(key);
          if (!referenceKeys.has(key)) {
            context.report({ node: property.key, messageId: "unknownKey", data: { key } });
          } else if (!sources.includes(`"${key.replace(PLURAL_SUFFIX, "")}"`)) {
            context.report({ node: property.key, messageId: "unusedKey", data: { key } });
          }
        }

        for (const key of referenceKeys) {
          if (!keys.has(key)) {
            context.report({ node, messageId: "missingKey", data: { key } });
          }
        }
      },
    };
  },
};

export default rule;
