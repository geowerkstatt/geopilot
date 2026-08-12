"use strict";

// Flags hex colors that carry an alpha channel (8-digit #RRGGBBAA or 4-digit #RGBA shorthand) and
// points to alpha() from @mui/material/styles instead. Deriving transparency from a base color with
// alpha() keeps the base swappable and the opacity readable, rather than baking an opaque alpha byte
// into the hex. This complements no-hardcoded-colors, which is disabled in appPalette.ts where hex
// colors are defined; this rule stays on there to guard the palette's transparency values.

// Matches a hex color with an alpha channel: exactly 8 or exactly 4 hex digits (6- and 3-digit hex
// have no alpha and are left alone). The 8-digit branch is tried first so #RRGGBBAA is not partially
// matched as a 4-digit token.
const HEX_ALPHA_REGEX = /#(?:[0-9a-fA-F]{8}|[0-9a-fA-F]{4})\b/;

// Splits a matched alpha-hex into its 6-digit base color and a rounded 0..1 opacity, expanding the
// 4-digit shorthand (#RGBA -> #RRGGBB + AA) so the suggestion is a ready-to-use alpha() call.
const toAlphaSuggestion = hex => {
  const body = hex.slice(1);
  const [r, g, b, a] =
    body.length === 8
      ? [body.slice(0, 2), body.slice(2, 4), body.slice(4, 6), body.slice(6, 8)]
      : [body[0].repeat(2), body[1].repeat(2), body[2].repeat(2), body[3].repeat(2)];
  const base = `#${r}${g}${b}`;
  const opacity = Math.round((parseInt(a, 16) / 255) * 100) / 100;
  return { base, opacity };
};

/** @type {import("eslint").Rule.RuleModule} */
const rule = {
  meta: {
    type: "problem",
    docs: {
      description:
        "Disallow hex colors with an alpha channel; use alpha(baseColor, opacity) from " +
        "@mui/material/styles so the base color stays swappable and the opacity readable",
    },
    messages: {
      noHexTransparency:
        "Hex color '{{ color }}' carries an alpha channel. Use alpha(\"{{ base }}\", {{ opacity }}) " +
        "from @mui/material/styles instead.",
    },
    schema: [],
  },
  create(context) {
    const check = (node, value) => {
      const match = HEX_ALPHA_REGEX.exec(value);
      if (!match) {
        return;
      }
      const { base, opacity } = toAlphaSuggestion(match[0]);
      context.report({ node, messageId: "noHexTransparency", data: { color: match[0], base, opacity } });
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
