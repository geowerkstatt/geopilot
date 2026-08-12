"use strict";

const fs = require("fs");
const path = require("path");
const ts = require("typescript");

// Enforces that every explicit palette access (theme.palette.X member access and MUI sx string
// shorthands such as color: "text.primary") refers to a path actually defined in appPalette.ts. MUI
// merges its own default palette under any custom one, so undefined paths like background.paper or
// action.disabledBackground resolve silently to MUI defaults at runtime instead of failing. This
// rule surfaces those. Implicit fallbacks (simply not setting a value) are unaffected.

// Palette members that are configuration/utilities rather than colors; never flagged.
const IGNORED_ROOTS = new Set(["mode", "contrastThreshold", "tonalOffset", "getContrastText", "augmentColor"]);

// Shape of a palette reference inside an sx/style string: a dotted path of identifiers/indexes with
// no whitespace, e.g. "primary.main", "background.paper", "grey.700". This distinguishes palette
// tokens from ordinary CSS values (lengths, keywords, functions) so only the former are validated,
// without hardcoding any list of group names; the allowed paths come solely from themePalette.
const PALETTE_TOKEN = /^[a-zA-Z][a-zA-Z0-9]*(?:\.[a-zA-Z0-9]+)+$/;

const APP_PALETTE_PATH = path.resolve(__dirname, "..", "src", "appPalette.ts");

// Reads appPalette.ts, finds the `themePalette` object literal, and returns the set of every dotted
// path in it (intermediate and leaf), e.g. "primary", "primary.states", "primary.states.hover".
const loadPalettePaths = filePath => {
  const source = fs.readFileSync(filePath, "utf8");
  const sourceFile = ts.createSourceFile(filePath, source, ts.ScriptTarget.Latest, true);

  let paletteObject;
  const findPaletteObject = node => {
    if (
      ts.isVariableDeclaration(node) &&
      ts.isIdentifier(node.name) &&
      node.name.text === "themePalette" &&
      node.initializer
    ) {
      // The initializer is `{ ... } satisfies PaletteOptions`; unwrap to the object literal.
      let initializer = node.initializer;
      while (ts.isSatisfiesExpression(initializer) || ts.isAsExpression(initializer)) {
        initializer = initializer.expression;
      }
      if (ts.isObjectLiteralExpression(initializer)) {
        paletteObject = initializer;
      }
    }
    ts.forEachChild(node, findPaletteObject);
  };
  findPaletteObject(sourceFile);

  if (!paletteObject) {
    throw new Error(`Could not find the 'themePalette' object literal in ${filePath}`);
  }

  const paths = new Set();
  const collect = (objectLiteral, prefix) => {
    for (const property of objectLiteral.properties) {
      if (!ts.isPropertyAssignment(property)) {
        continue;
      }
      let name;
      if (ts.isIdentifier(property.name)) {
        name = property.name.text;
      } else if (ts.isStringLiteral(property.name)) {
        name = property.name.text;
      } else {
        continue;
      }

      const dottedPath = prefix ? `${prefix}.${name}` : name;
      paths.add(dottedPath);
      if (ts.isObjectLiteralExpression(property.initializer)) {
        collect(property.initializer, dottedPath);
      }
    }
  };
  collect(paletteObject, "");
  return paths;
};

// Parsed once per process; the palette definition does not change during a lint run.
let cachedPaths;
const getPalettePaths = () => {
  if (!cachedPaths) {
    cachedPaths = loadPalettePaths(APP_PALETTE_PATH);
  }
  return cachedPaths;
};

// Walks a member-expression chain outward-in to extract the palette path it accesses. Returns the
// dotted path after `palette` (e.g. "primary.states.hover" for theme.palette.primary.states.hover,
// or "primary.main" for a destructured palette.primary.main), or undefined when the chain is not a
// static palette access (not rooted at `<obj>.palette` / a `palette` identifier, or uses a computed
// member such as palette[severity]).
const getPalettePath = node => {
  const segments = [];
  let current = node;
  while (current && current.type === "MemberExpression") {
    if (current.property.type === "Identifier" && current.property.name === "palette" && !current.computed) {
      // Reached `<obj>.palette`; the segments collected so far form the path.
      return segments.length ? segments.reverse().join(".") : undefined;
    }
    if (current.computed || current.property.type !== "Identifier") {
      return undefined; // dynamic segment, e.g. palette[severity] or palette.grey[700]
    }
    segments.push(current.property.name);
    current = current.object;
  }
  // Chain bottomed out on a bare `palette` identifier (destructured from theme).
  if (current && current.type === "Identifier" && current.name === "palette") {
    return segments.length ? segments.reverse().join(".") : undefined;
  }
  return undefined;
};

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

// sx/style color-capable shorthand property names beyond those ending in "color".
const COLOR_PROPERTIES = new Set(["bgcolor", "fill", "stroke", "background", "borderColor"]);
const isColorProperty = name =>
  typeof name === "string" && (/color$/i.test(name) || COLOR_PROPERTIES.has(name));

/** @type {import("eslint").Rule.RuleModule} */
const rule = {
  meta: {
    type: "problem",
    docs: {
      description:
        "Disallow palette accesses that are not defined in appPalette.ts; undefined paths resolve " +
        "silently to MUI's default theme instead of the app palette",
    },
    messages: {
      undefinedPaletteColor:
        "'palette.{{ path }}' is not defined in appPalette.ts and falls back to the MUI default " +
        "theme. Use a defined token or add '{{ path }}' to appPalette.ts.",
    },
    schema: [],
  },
  create(context) {
    const palettePaths = getPalettePaths();
    const isDefined = path => palettePaths.has(path) || IGNORED_ROOTS.has(path.split(".")[0]);

    return {
      MemberExpression(node) {
        // Only inspect the outermost expression of a chain to report each access once.
        if (node.parent.type === "MemberExpression" && node.parent.object === node) {
          return;
        }
        const path = getPalettePath(node);
        if (path && !isDefined(path)) {
          context.report({ node, messageId: "undefinedPaletteColor", data: { path } });
        }
      },
      Literal(node) {
        if (typeof node.value !== "string" || !PALETTE_TOKEN.test(node.value)) {
          return;
        }
        if (!isColorProperty(getColorPropertyKey(node))) {
          return;
        }
        if (!isDefined(node.value)) {
          context.report({ node, messageId: "undefinedPaletteColor", data: { path: node.value } });
        }
      },
    };
  },
};

module.exports = rule;
