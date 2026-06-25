export interface TreeNode {
  message: string;
  icon?: string;
  color?: string;
  metadata?: Record<string, string>;
  values?: TreeNode[];
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
