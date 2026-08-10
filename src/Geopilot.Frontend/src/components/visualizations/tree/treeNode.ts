import { TreeField, TreeItem } from "../../../api/apiInterfaces";
import { LocalizedResolver } from "../../../hooks/useLocalized";

/** A node of the error tree's displayed hierarchy, built in the frontend from the flat items by {@link buildTree}. */
export interface TreeNode {
  /** The text shown for this node (a group value, or a leaf's text). */
  message: string;
  /** Severity ("error"/"warning") of the node's most severe leaf; drives the leaf/group icon and colour. */
  color?: string;
  /** Number of contained leaf nodes (errors) in this node's subtree; 0 for a leaf. Shown next to group labels. */
  count: number;
  /** The underlying error item, shown in the detail panel when the node is selected. Set on leaves. */
  item?: TreeItem;
  /** Child nodes nested under this node. */
  values?: TreeNode[];
  /** Stable id of the validation error this leaf represents, shared with its map feature. Absent on group nodes. */
  errorId?: string;
}

/** All groupable/filterable fields, in a stable iteration order. */
const TREE_FIELDS: readonly TreeField[] = ["errorType", "model", "topic", "class"];

/** The selected filter values per field. */
export type FieldFilters = Partial<Record<TreeField, string[]>>;

/** A filterable field together with its distinct (resolved) value options. */
export interface FilterAttribute {
  field: TreeField;
  options: string[];
}

const SEVERITY_RANK: Record<string, number> = { error: 2, warning: 1 };

const severityRank = (color?: string): number => (color ? (SEVERITY_RANK[color] ?? 0) : 0);

/** Resolves a groupable field of an item: the localized error category, or one of the plain string fields. */
const fieldValue = (item: TreeItem, field: TreeField, localize: LocalizedResolver): string | undefined => {
  if (field === "errorType") {
    return item.errorType ? localize(item.errorType) : undefined;
  }
  return item[field];
};

// The leaf text; the ?? "" guards against legacy payloads that carry neither field.
const leafText = (item: TreeItem): string => item.tid ?? item.message ?? "";

const toLeaf = (item: TreeItem): TreeNode => ({
  message: leafText(item),
  color: item.severity,
  count: 0,
  item,
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
    count: children.reduce((sum, child) => sum + leafCount(child), 0),
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
  groupBy: TreeField[],
  level: number,
  localize: LocalizedResolver,
  ungroupedLabel: string,
): TreeNode[] => {
  if (level >= groupBy.length) {
    return items.map(item => toLeaf(item));
  }

  const field = groupBy[level];
  const groups = new Map<string, TreeItem[]>();
  const ungrouped: TreeItem[] = [];

  for (const item of items) {
    const value = fieldValue(item, field, localize);
    if (value === undefined) {
      ungrouped.push(item);
      continue;
    }
    const bucket = groups.get(value);
    if (bucket) bucket.push(item);
    else groups.set(value, [item]);
  }

  const named = sortGroups(
    [...groups.entries()].map(([value, bucket]) =>
      makeGroup(value, groupItems(bucket, groupBy, level + 1, localize, ungroupedLabel)),
    ),
  );

  // Items missing this field are shown as leaves directly under a single "ungrouped" group, never recursed into
  // the remaining fields, so a missing field does not produce a chain of virtual "ungrouped" subgroups.
  if (ungrouped.length > 0) {
    named.push(
      makeGroup(
        ungroupedLabel,
        ungrouped.map(item => toLeaf(item)),
      ),
    );
  }

  return named;
};

/** Builds the displayed hierarchy from the flat items by grouping them on the given fields. */
export const buildTree = (
  items: TreeItem[],
  groupBy: TreeField[],
  localize: LocalizedResolver,
  ungroupedLabel: string,
): TreeNode[] => groupItems(items, groupBy, 0, localize, ungroupedLabel);

const itemMatchesFilters = (
  item: TreeItem,
  messageQuery: string,
  fieldFilters: FieldFilters,
  localize: LocalizedResolver,
): boolean => {
  if (messageQuery) {
    // Match every field of the error, so it can be found by any of its attributes. messageQuery is already lower-cased.
    const haystacks = [
      item.message,
      item.tid,
      item.model,
      item.topic,
      item.class,
      item.line?.toString(),
      item.coordinates,
      item.errorType ? localize(item.errorType) : undefined,
    ];
    if (!haystacks.some(value => value !== undefined && value.toLowerCase().includes(messageQuery))) {
      return false;
    }
  }

  return TREE_FIELDS.every(field => {
    const selected = fieldFilters[field];
    if (!selected || selected.length === 0) return true;
    const value = fieldValue(item, field, localize);
    return value !== undefined && selected.includes(value);
  });
};

/** Keeps the items that match the active filters. The tree is rebuilt from the survivors. */
export const filterItems = (
  items: TreeItem[],
  messageQuery: string,
  fieldFilters: FieldFilters,
  localize: LocalizedResolver,
): TreeItem[] => items.filter(item => itemMatchesFilters(item, messageQuery, fieldFilters, localize));

/**
 * Collects the filterable fields together with their distinct (resolved) values. Only the fields listed in
 * {@link filterBy} are offered, in that display order; fields without any value in the items are skipped. An
 * empty {@link filterBy} offers no filters.
 */
export const collectFilterAttributes = (
  items: TreeItem[],
  localize: LocalizedResolver,
  filterBy: TreeField[],
): FilterAttribute[] =>
  filterBy
    .map(field => {
      const values = items.flatMap(item => {
        const value = fieldValue(item, field, localize);
        return value === undefined ? [] : [value];
      });
      return { field, options: [...new Set(values)].sort((a, b) => a.localeCompare(b)) };
    })
    .filter(attribute => attribute.options.length > 0);

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

/** Collects the structural ids of the nodes that have children, i.e. the ones that can be expanded. */
export const collectExpandableIds = (nodes: TreeNode[], target: string[], prefix = "n"): void => {
  nodes.forEach((node, index) => {
    if (node.values && node.values.length > 0) {
      const id = nodeId(prefix, index);
      target.push(id);
      collectExpandableIds(node.values, target, id);
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
