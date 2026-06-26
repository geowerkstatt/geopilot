export interface TreeNode {
  message: string;
  icon?: string;
  color?: string;
  metadata?: Record<string, string>;
  values?: TreeNode[];
  /** Stable id of the validation error this leaf represents, shared with its map feature for cross-select. Absent on group nodes. */
  errorId?: string;
}

/** The tree-visualization payload produced by the error-tree pipeline step. */
export interface TreeVisualizationConfig {
  /** The root nodes of the tree, rendered in order. */
  nodes: TreeNode[];
}

export type MetadataFilters = Record<string, string[]>;

export interface MetadataAttribute {
  key: string;
  options: string[];
}

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

/** Collects every metadata attribute that appears in the tree together with its distinct values, in first-seen order. */
export const collectMetadataAttributes = (nodes: TreeNode[]): MetadataAttribute[] => {
  const valuesByKey = new Map<string, Set<string>>();

  const visit = (node: TreeNode): void => {
    if (node.metadata) {
      Object.entries(node.metadata).forEach(([key, value]) => {
        const values = valuesByKey.get(key) ?? new Set<string>();
        values.add(value);
        valuesByKey.set(key, values);
      });
    }
    node.values?.forEach(visit);
  };

  nodes.forEach(visit);

  return Array.from(valuesByKey.entries()).map(([key, values]) => ({
    key,
    options: Array.from(values).sort((a, b) => a.localeCompare(b)),
  }));
};

const nodeMatchesFilters = (node: TreeNode, messageQuery: string, metadataFilters: MetadataFilters): boolean => {
  if (messageQuery && !node.message.toLowerCase().includes(messageQuery)) {
    return false;
  }

  return Object.entries(metadataFilters).every(([key, selected]) => {
    if (selected.length === 0) return true;
    const value = node.metadata?.[key];
    return value !== undefined && selected.includes(value);
  });
};

/**
 * Keeps a node when it matches the active filters itself, or when any of its descendants does, so that the
 * path to every match stays visible. Children are filtered recursively, never the whole subtree of a match.
 */
export const filterNodes = (nodes: TreeNode[], messageQuery: string, metadataFilters: MetadataFilters): TreeNode[] =>
  nodes.reduce<TreeNode[]>((kept, node) => {
    const filteredChildren = node.values ? filterNodes(node.values, messageQuery, metadataFilters) : [];
    if (nodeMatchesFilters(node, messageQuery, metadataFilters) || filteredChildren.length > 0) {
      kept.push({ ...node, values: filteredChildren });
    }
    return kept;
  }, []);

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
