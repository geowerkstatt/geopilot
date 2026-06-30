/** A multilingual string keyed by ISO 639 language code, as serialized by the backend LocalizedText. */
type LocalizedText = Record<string, string>;

/** A metadata value: a plain data string, or a multilingual label we generated on the backend. */
type MetadataValue = string | LocalizedText;

/** A flat tree item as shipped by the backend. The frontend derives the hierarchy by grouping items. */
export interface TreeItem {
  /** Stable id correlating this item with its map feature for cross-select. Absent on items without a feature. */
  id?: string;
  /** The text shown for the item's leaf node. */
  label: string;
  icon?: string;
  color?: string;
  /** Arbitrary metadata; values are plain strings (data) or LocalizedText (generated labels). */
  metadata: Record<string, MetadataValue>;
}

/** The tree-visualization payload produced by the error-tree pipeline step. */
export interface TreeVisualizationConfig {
  /** The flat items; the frontend groups them on {@link groupBy} to build the displayed tree. */
  items: TreeItem[];
  /** The metadata keys to group by, outermost first (e.g. ["Model", "Topic", "Class"]). */
  groupBy: string[];
}

/** A node of the displayed hierarchy, built in the frontend from the flat items. */
export interface TreeNode {
  /** The text shown for this node (a group value, or a leaf's label). */
  message: string;
  icon?: string;
  color?: string;
  /** Number of direct children; 0 for a leaf. Shown next to group labels. */
  childCount: number;
  /** Resolved (single-language) metadata shown when the node is selected. Set on leaves. */
  metadata?: Record<string, string>;
  /** Child nodes nested under this node. */
  values?: TreeNode[];
  /** Stable id of the validation error this leaf represents, shared with its map feature. Absent on group nodes. */
  errorId?: string;
}

export type MetadataFilters = Record<string, string[]>;

export interface MetadataAttribute {
  key: string;
  options: string[];
}

const SEVERITY_RANK: Record<string, number> = { error: 2, warning: 1 };
const ICON_BY_COLOR: Record<string, string> = { error: "error_outline", warning: "warning_amber" };

const severityRank = (color?: string): number => (color ? (SEVERITY_RANK[color] ?? 0) : 0);

/** Resolves a metadata value to its display string in the active language, with a stable fallback chain. */
const resolveLocalized = (value: MetadataValue, language: string): string => {
  if (typeof value === "string") return value;
  return value[language] ?? value.en ?? value.de ?? Object.values(value)[0] ?? "";
};

const resolveMetadata = (metadata: Record<string, MetadataValue>, language: string): Record<string, string> =>
  Object.fromEntries(Object.entries(metadata).map(([key, value]) => [key, resolveLocalized(value, language)]));

const toLeaf = (item: TreeItem, language: string): TreeNode => ({
  message: item.label,
  icon: item.icon,
  color: item.color,
  childCount: 0,
  metadata: resolveMetadata(item.metadata, language),
  errorId: item.id,
});

// Total number of leaves under a node, used only to order groups (most errors first).
const leafCount = (node: TreeNode): number =>
  node.values && node.values.length > 0 ? node.values.reduce((sum, child) => sum + leafCount(child), 0) : 1;

// The most severe color among a node's direct children (whose own color already reflects their subtree).
const reduceColor = (children: TreeNode[]): string | undefined =>
  children.reduce<string | undefined>(
    (worst, child) => (severityRank(child.color) > severityRank(worst) ? child.color : worst),
    undefined,
  );

const makeGroup = (message: string, children: TreeNode[]): TreeNode => {
  const color = reduceColor(children);
  return {
    message,
    color,
    icon: color ? ICON_BY_COLOR[color] : undefined,
    childCount: children.length,
    values: children,
  };
};

// Groups are ordered by severity, then by leaf count descending, then by label. The ungrouped bucket is appended last.
const sortGroups = (groups: TreeNode[]): TreeNode[] =>
  [...groups].sort(
    (a, b) =>
      severityRank(b.color) - severityRank(a.color) ||
      leafCount(b) - leafCount(a) ||
      a.message.localeCompare(b.message),
  );

const groupItems = (
  items: TreeItem[],
  groupBy: string[],
  level: number,
  language: string,
  ungroupedLabel: string,
): TreeNode[] => {
  if (level >= groupBy.length) {
    return items.map(item => toLeaf(item, language));
  }

  const key = groupBy[level];
  const groups = new Map<string, TreeItem[]>();
  const ungrouped: TreeItem[] = [];

  for (const item of items) {
    const raw = item.metadata[key];
    if (raw === undefined) {
      ungrouped.push(item);
      continue;
    }
    const value = resolveLocalized(raw, language);
    const bucket = groups.get(value);
    if (bucket) bucket.push(item);
    else groups.set(value, [item]);
  }

  const named = sortGroups(
    [...groups.entries()].map(([value, bucket]) =>
      makeGroup(value, groupItems(bucket, groupBy, level + 1, language, ungroupedLabel)),
    ),
  );

  // Items missing this key are shown as leaves directly under a single "ungrouped" group, never recursed into
  // the remaining keys, so a missing attribute does not produce a chain of virtual "ungrouped" subgroups.
  if (ungrouped.length > 0) {
    named.push(
      makeGroup(
        ungroupedLabel,
        ungrouped.map(item => toLeaf(item, language)),
      ),
    );
  }

  return named;
};

/** Builds the displayed hierarchy from the flat items by grouping them on the given metadata keys. */
export const buildTree = (items: TreeItem[], groupBy: string[], language: string, ungroupedLabel: string): TreeNode[] =>
  groupItems(items, groupBy, 0, language, ungroupedLabel);

const itemMatchesFilters = (
  item: TreeItem,
  messageQuery: string,
  metadataFilters: MetadataFilters,
  language: string,
): boolean => {
  if (messageQuery) {
    // Match the leaf label and every metadata value, so an error can be found by any of its attributes.
    // messageQuery is already lower-cased.
    const haystacks = [item.label, ...Object.values(item.metadata).map(value => resolveLocalized(value, language))];
    if (!haystacks.some(value => value.toLowerCase().includes(messageQuery))) {
      return false;
    }
  }

  return Object.entries(metadataFilters).every(([key, selected]) => {
    if (selected.length === 0) return true;
    const value = item.metadata[key];
    return value !== undefined && selected.includes(resolveLocalized(value, language));
  });
};

/** Keeps the items that match the active filters. The tree is rebuilt from the survivors. */
export const filterItems = (
  items: TreeItem[],
  messageQuery: string,
  metadataFilters: MetadataFilters,
  language: string,
): TreeItem[] => items.filter(item => itemMatchesFilters(item, messageQuery, metadataFilters, language));

/** Collects every metadata attribute across the items together with its distinct (resolved) values. */
export const collectMetadataAttributes = (items: TreeItem[], language: string): MetadataAttribute[] => {
  const valuesByKey = new Map<string, Set<string>>();

  for (const item of items) {
    for (const [key, value] of Object.entries(item.metadata)) {
      const values = valuesByKey.get(key) ?? new Set<string>();
      values.add(resolveLocalized(value, language));
      valuesByKey.set(key, values);
    }
  }

  return Array.from(valuesByKey.entries()).map(([key, values]) => ({
    key,
    options: Array.from(values).sort((a, b) => a.localeCompare(b)),
  }));
};

export const nodeId = (prefix: string, index: number): string => `${prefix}-${index}`;

export const indexNodes = (nodes: TreeNode[], target: Map<string, TreeNode>, prefix = "n"): void => {
  nodes.forEach((node, index) => {
    const id = nodeId(prefix, index);
    target.set(id, node);
    if (node.values && node.values.length > 0) {
      indexNodes(node.values, target, id);
    }
  });
};

export const collectItemIds = (nodes: TreeNode[], target: string[], prefix = "n"): void => {
  nodes.forEach((node, index) => {
    const id = nodeId(prefix, index);
    target.push(id);
    if (node.values && node.values.length > 0) {
      collectItemIds(node.values, target, id);
    }
  });
};

/**
 * Walks the tree (with the same structural ids as indexNodes/renderTreeItems) and returns the bidirectional
 * correlation between an error's id and its structural tree-node id, plus, per node id, the set of error ids
 * in that node's subtree (one for a leaf, many for a group).
 */
export const buildErrorIdIndex = (
  nodes: TreeNode[],
  prefix = "n",
): { nodeIdByErrorId: Map<string, string>; errorIdsByNodeId: Map<string, string[]> } => {
  const nodeIdByErrorId = new Map<string, string>();
  const errorIdsByNodeId = new Map<string, string[]>();

  const visit = (node: TreeNode, id: string): string[] => {
    const childIds = (node.values ?? []).flatMap((child, index) => visit(child, nodeId(id, index)));
    const own = node.errorId ? [node.errorId] : [];
    const subtree = [...own, ...childIds];
    if (node.errorId) nodeIdByErrorId.set(node.errorId, id);
    errorIdsByNodeId.set(id, subtree);
    return subtree;
  };

  nodes.forEach((node, index) => visit(node, nodeId(prefix, index)));
  return { nodeIdByErrorId, errorIdsByNodeId };
};
