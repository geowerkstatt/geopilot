// Matches a rem length inside a string, e.g. "1rem", "0.5rem", ".5rem", "16rem" and composite
// values like "0 0.5rem". A digit (or leading dot) must sit right before "rem" so unrelated words
// that merely contain the letters "rem" are never flagged.
const REM_LENGTH = /(?:\d+\.?\d*|\.\d+)rem\b/;

// Matches a px length inside a string, e.g. "16px", "0.5px". Only used to check font-size values.
const PX_LENGTH = /(?:\d+\.?\d*|\.\d+)px\b/;

// Returns the static name of an object property key, or undefined for computed/spread keys.
const getPropertyKeyName = property => {
  if (property.computed) {
    return undefined;
  }
  const { key } = property;
  if (key.type === "Identifier") {
    return key.name;
  }
  if (key.type === "Literal" && typeof key.value === "string") {
    return key.value;
  }
  return undefined;
};

// A font-size value is a raw pixel size when it is a bare number (MUI/CSS-in-JS reads it as px) or a
// px string literal. rem string literals are already caught by the generic check below, so they are
// left out here to avoid reporting the same node twice. MUI icon tokens ("small", "inherit", ...)
// and any non-literal expression are not raw pixel sizes and pass through untouched.
const isRawPixelFontSize = node => {
  if (node.type !== "Literal") {
    return false;
  }
  if (typeof node.value === "number") {
    return true;
  }
  return typeof node.value === "string" && PX_LENGTH.test(node.value);
};

/** @type {import("eslint").Rule.RuleModule} */
const rule = {
  meta: {
    type: "problem",
    docs: {
      description:
        "Require rem-based sizes to go through the px2rem() helper instead of hardcoded 'rem' string " +
        "literals, and require every font size to use px2rem() rather than a raw px or numeric value, " +
        "so every size derives from the same px-to-rem scale",
    },
    messages: {
      usePx2rem:
        "Use px2rem(<pixels>) instead of the hardcoded rem literal '{{ value }}'. For example '1rem' " +
        "becomes px2rem(16).",
      usePx2remFontSize:
        "Font size must use px2rem(<pixels>) instead of the raw value '{{ value }}'. For example '16px' " +
        "or 16 becomes px2rem(16).",
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
    const allow = new Set(context.options[0]?.allow ?? []);
    const sourceCode = context.sourceCode ?? context.getSourceCode();

    return {
      Literal(node) {
        if (typeof node.value !== "string" || allow.has(node.value)) {
          return;
        }
        if (!REM_LENGTH.test(node.value)) {
          return;
        }

        context.report({
          node,
          messageId: "usePx2rem",
          data: { value: node.value },
        });
      },
      Property(node) {
        // Shorthand ({ fontSize }) forwards an outer binding we cannot resolve here.
        if (node.shorthand || getPropertyKeyName(node) !== "fontSize") {
          return;
        }
        if (!isRawPixelFontSize(node.value)) {
          return;
        }

        context.report({
          node: node.value,
          messageId: "usePx2remFontSize",
          data: { value: sourceCode.getText(node.value) },
        });
      },
    };
  },
};

export default rule;
