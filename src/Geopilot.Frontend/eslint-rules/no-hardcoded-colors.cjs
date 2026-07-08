"use strict";

// Matches 3, 4, 6 and 8 digit hex colors (e.g. #fff, #ffff, #124A4F, #124A4F33).
const HEX_COLOR_REGEX = /#(?:[0-9a-fA-F]{8}|[0-9a-fA-F]{6}|[0-9a-fA-F]{3,4})\b/;
// Matches rgb() and rgba() colors (e.g. rgb(18, 74, 79), rgba(18, 74, 79, 0.5)).
const RGB_COLOR_REGEX = /rgba?\(\s*\d{1,3}\s*,\s*\d{1,3}\s*,\s*\d{1,3}\s*(?:,\s*[\d.]+\s*)?\)/;

// CSS named colors. Many of these are also ordinary English words, so they are only flagged when
// they appear in the value of a CSS color property (see isColorProperty). CSS keyword values such
// as currentColor, transparent, inherit, initial, unset, revert and none are intentionally absent
// and therefore never flagged.
const NAMED_COLORS = new Set([
  "aliceblue", "antiquewhite", "aqua", "aquamarine", "azure", "beige", "bisque", "black",
  "blanchedalmond", "blue", "blueviolet", "brown", "burlywood", "cadetblue", "chartreuse",
  "chocolate", "coral", "cornflowerblue", "cornsilk", "crimson", "cyan", "darkblue", "darkcyan",
  "darkgoldenrod", "darkgray", "darkgreen", "darkgrey", "darkkhaki", "darkmagenta", "darkolivegreen",
  "darkorange", "darkorchid", "darkred", "darksalmon", "darkseagreen", "darkslateblue",
  "darkslategray", "darkslategrey", "darkturquoise", "darkviolet", "deeppink", "deepskyblue",
  "dimgray", "dimgrey", "dodgerblue", "firebrick", "floralwhite", "forestgreen", "fuchsia",
  "gainsboro", "ghostwhite", "gold", "goldenrod", "gray", "green", "greenyellow", "grey", "honeydew",
  "hotpink", "indianred", "indigo", "ivory", "khaki", "lavender", "lavenderblush", "lawngreen",
  "lemonchiffon", "lightblue", "lightcoral", "lightcyan", "lightgoldenrodyellow", "lightgray",
  "lightgreen", "lightgrey", "lightpink", "lightsalmon", "lightseagreen", "lightskyblue",
  "lightslategray", "lightslategrey", "lightsteelblue", "lightyellow", "lime", "limegreen", "linen",
  "magenta", "maroon", "mediumaquamarine", "mediumblue", "mediumorchid", "mediumpurple",
  "mediumseagreen", "mediumslateblue", "mediumspringgreen", "mediumturquoise", "mediumvioletred",
  "midnightblue", "mintcream", "mistyrose", "moccasin", "navajowhite", "navy", "oldlace", "olive",
  "olivedrab", "orange", "orangered", "orchid", "palegoldenrod", "palegreen", "paleturquoise",
  "palevioletred", "papayawhip", "peachpuff", "peru", "pink", "plum", "powderblue", "purple",
  "rebeccapurple", "red", "rosybrown", "royalblue", "saddlebrown", "salmon", "sandybrown",
  "seagreen", "seashell", "sienna", "silver", "skyblue", "slateblue", "slategray", "slategrey",
  "snow", "springgreen", "steelblue", "tan", "teal", "thistle", "tomato",
  "turquoise", "violet", "wheat", "white", "whitesmoke", "yellow", "yellowgreen",
]);

// CSS color-capable properties whose (camelCase) name does not end in "color". SVG paint properties
// take a bare color; the rest are shorthands where a color is one token among several (e.g.
// border: "1px solid red"). Because their values are scanned token by token, a stray word that
// happens to be a CSS color name (rare in these positions) can produce a false positive.
const COLOR_PROPERTIES = new Set([
  "fill", "stroke", "background",
  "border", "borderTop", "borderRight", "borderBottom", "borderLeft",
  "outline", "boxShadow", "textShadow", "columnRule", "textDecoration",
]);

// A property accepts a color when its (camelCase) name ends in "color" (color, backgroundColor,
// borderColor, borderTopColor, outlineColor, caretColor, ...) or is one of the properties above.
const isColorProperty = name =>
  typeof name === "string" && (/color$/i.test(name) || COLOR_PROPERTIES.has(name));

// Returns the static key name of the object property the given value node belongs to, or undefined
// when it is not a plain property value (e.g. array element, computed key, spread). The value node
// is the literal itself, or the enclosing template literal for a TemplateElement.
const getColorPropertyKey = valueNode => {
  const parent = valueNode.parent;
  if (!parent || parent.type !== "Property" || parent.value !== valueNode || parent.computed) {
    return undefined;
  }

  const { key } = parent;
  if (key.type === "Identifier") {
    return key.name;
  }
  if (key.type === "Literal" && typeof key.value === "string") {
    return key.value;
  }
  return undefined;
};

/** @type {import("eslint").Rule.RuleModule} */
const rule = {
  meta: {
    type: "problem",
    docs: {
      description:
        "Disallow hardcoded hex/rgb/rgba colors and named CSS colors on color properties; " +
        "use tokens from the app theme instead",
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

    const checkHexAndRgb = (node, value) => {
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

    // Returns the first named CSS color found among the whitespace/comma/slash/parenthesis separated
    // tokens of a value (e.g. finds "red" in "1px solid red"), preserving its original casing.
    const findNamedColor = value => {
      for (const token of value.split(/[\s,/()]+/)) {
        const lower = token.toLowerCase();
        if (NAMED_COLORS.has(lower) && !allow.has(lower)) {
          return token;
        }
      }
      return undefined;
    };

    // Reports a named color when the value belongs to a color-capable property.
    const checkNamedColor = (node, value, valueNode) => {
      if (!isColorProperty(getColorPropertyKey(valueNode))) {
        return;
      }

      const color = findNamedColor(value);
      if (color) {
        context.report({ node, messageId: "noHardcodedColor", data: { color } });
      }
    };

    return {
      Literal(node) {
        if (typeof node.value !== "string") {
          return;
        }

        checkHexAndRgb(node, node.value);
        checkNamedColor(node, node.value, node);
      },
      TemplateElement(node) {
        checkHexAndRgb(node, node.value.raw);
        checkNamedColor(node, node.value.raw, node.parent);
      },
    };
  },
};

module.exports = rule;
