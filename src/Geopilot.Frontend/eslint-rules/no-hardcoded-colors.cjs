"use strict";

// Matches 3, 4, 6 and 8 digit hex colors (e.g. #fff, #ffff, #124A4F, #124A4F33).
const HEX_COLOR_REGEX = /#(?:[0-9a-fA-F]{8}|[0-9a-fA-F]{6}|[0-9a-fA-F]{3,4})\b/;
// Matches rgb() and rgba() colors (e.g. rgb(18, 74, 79), rgba(18, 74, 79, 0.5)).
const RGB_COLOR_REGEX = /rgba?\(\s*\d{1,3}\s*,\s*\d{1,3}\s*,\s*\d{1,3}\s*(?:,\s*[\d.]+\s*)?\)/;

/** @type {import("eslint").Rule.RuleModule} */
const rule = {
  meta: {
    type: "problem",
    docs: {
      description: "Disallow hardcoded hex/rgb/rgba colors; use tokens from the app theme instead",
    },
    messages: {
      noHardcodedColor:
        "Hardcoded color '{{ color }}' is not allowed. Use a token from the app theme " +
        "instead, e.g. theme.palette.primary.main or sx={{ color: 'primary.main' }}.",
    },
    schema: [
      {
        type: "object",
        properties: {
          allow: { type: "array", items: { type: "string" } },
        },
        additionalProperties: false,
      },
    ],
  },
  create(context) {
    const allow = new Set((context.options[0]?.allow ?? []).map(color => color.toLowerCase()));

    const check = (node, value) => {
      if (typeof value !== "string") {
        return;
      }

      const hexMatch = HEX_COLOR_REGEX.exec(value);
      if (hexMatch && !allow.has(hexMatch[0].toLowerCase())) {
        context.report({ node, messageId: "noHardcodedColor", data: { color: hexMatch[0] } });
        return;
      }

      const rgbMatch = RGB_COLOR_REGEX.exec(value);
      if (rgbMatch && !allow.has(rgbMatch[0].toLowerCase())) {
        context.report({ node, messageId: "noHardcodedColor", data: { color: rgbMatch[0] } });
      }
    };

    return {
      Literal(node) {
        if (typeof node.value === "string") {
          check(node, node.value);
        }
      },
      TemplateElement(node) {
        check(node, node.value.raw);
      },
    };
  },
};

module.exports = rule;
