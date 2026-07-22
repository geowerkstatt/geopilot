"use strict";

// A property sets a border radius when its (camelCase) name starts with "border" and ends in
// "radius": borderRadius, the four corner properties (borderTopLeftRadius, ...) and the CSS logical
// variants (borderStartStartRadius, ...). The "border" prefix requirement avoids flagging unrelated
// "radius" properties, such as an OpenLayers circle style radius.
const isBorderRadiusProperty = name => typeof name === "string" && /^border[a-z]*radius$/i.test(name);

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

// True for a member access that reads a radius token: theme.radius.default, geopilotTheme.radius.full
// or a destructured radius.none. The object is either "<something>.radius" or a bare "radius".
const isRadiusTokenAccess = node => {
  if (!node || node.type !== "MemberExpression") {
    return false;
  }
  const { object } = node;
  if (object.type === "Identifier" && object.name === "radius") {
    return true;
  }
  return (
    object.type === "MemberExpression" &&
    !object.computed &&
    object.property.type === "Identifier" &&
    object.property.name === "radius"
  );
};

// Whether a border-radius value expression is permitted. Only a radius token is accepted; arrow/sx
// callbacks and conditionals are unwrapped so every branch must resolve to a token, and an explicit
// allow list can additionally permit specific literal values (e.g. "inherit").
const isAllowedValue = (node, allow) => {
  switch (node.type) {
    case "ArrowFunctionExpression":
    case "FunctionExpression":
      // sx callback form: borderRadius: theme => theme.radius.default
      return node.body.type !== "BlockStatement" && isAllowedValue(node.body, allow);
    case "ConditionalExpression":
      return isAllowedValue(node.consequent, allow) && isAllowedValue(node.alternate, allow);
    case "TSAsExpression":
    case "TSSatisfiesExpression":
      return isAllowedValue(node.expression, allow);
    case "MemberExpression":
      return isRadiusTokenAccess(node);
    case "Literal":
      return allow.has(String(node.value).toLowerCase());
    default:
      return false;
  }
};

/** @type {import("eslint").Rule.RuleModule} */
const rule = {
  meta: {
    type: "problem",
    docs: {
      description:
        "Require border-radius values to come from the app theme radius tokens (theme.radius.*); " +
        "literals, theme.spacing() and other expressions are not allowed",
    },
    messages: {
      useRadiusToken:
        "Border radius must use a theme radius token, not '{{ value }}'. Use theme.radius.default, " +
        "theme.radius.none or theme.radius.full instead.",
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
    const allow = new Set((context.options[0]?.allow ?? []).map(value => value.toLowerCase()));
    const sourceCode = context.sourceCode ?? context.getSourceCode();

    return {
      Property(node) {
        // Shorthand ({ borderRadius }) forwards an outer binding we cannot resolve here.
        if (node.shorthand) {
          return;
        }
        if (!isBorderRadiusProperty(getPropertyKeyName(node))) {
          return;
        }
        if (isAllowedValue(node.value, allow)) {
          return;
        }

        context.report({
          node: node.value,
          messageId: "useRadiusToken",
          data: { value: sourceCode.getText(node.value) },
        });
      },
    };
  },
};

module.exports = rule;
