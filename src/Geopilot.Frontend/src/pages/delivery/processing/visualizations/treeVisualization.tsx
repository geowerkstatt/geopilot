import { ReactNode, useEffect, useMemo, useState } from "react";
import { Box, Icon, Typography } from "@mui/material";
import { SimpleTreeView, TreeItem } from "@mui/x-tree-view";
import { useTranslation } from "react-i18next";
import useFetch from "../../../../hooks/useFetch";

type IconColor = "inherit" | "action" | "disabled" | "primary" | "secondary" | "error" | "info" | "success" | "warning";

const MUI_ICON_COLORS: IconColor[] = [
  "inherit",
  "action",
  "disabled",
  "primary",
  "secondary",
  "error",
  "info",
  "success",
  "warning",
];

const isMuiColor = (value: string): value is IconColor => (MUI_ICON_COLORS as string[]).includes(value);

interface TreeNode {
  message: string;
  icon?: string;
  color?: string;
  values?: TreeNode[];
}

const renderIcon = (node: TreeNode): ReactNode => {
  if (!node.icon) return null;
  if (node.color && !isMuiColor(node.color)) {
    return (
      <Icon fontSize="small" sx={{ color: node.color }}>
        {node.icon}
      </Icon>
    );
  }
  return (
    <Icon fontSize="small" color={(node.color as IconColor) ?? "inherit"}>
      {node.icon}
    </Icon>
  );
};

const renderLabel = (node: TreeNode): ReactNode => (
  <Box sx={{ display: "flex", alignItems: "center", gap: 0.5, py: 0.25 }}>
    {renderIcon(node)}
    <Typography variant="body2">{node.message}</Typography>
  </Box>
);

const renderItems = (nodes: TreeNode[], prefix = "n"): ReactNode =>
  nodes.map((node, index) => {
    const id = `${prefix}-${index}`;
    const hasChildren = node.values && node.values.length > 0;
    return (
      <TreeItem key={id} itemId={id} label={renderLabel(node)}>
        {hasChildren ? renderItems(node.values!, id) : null}
      </TreeItem>
    );
  });

interface TreeVisualizationProps {
  url: string;
}

export const TreeVisualization = ({ url }: TreeVisualizationProps) => {
  const { t } = useTranslation();
  const { fetchApi } = useFetch();
  const [nodes, setNodes] = useState<TreeNode[] | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setNodes(null);
    setErrorMessage(null);

    (async () => {
      try {
        const data = await fetchApi<TreeNode[]>(url);
        if (!cancelled) setNodes(data);
      } catch {
        if (!cancelled) setErrorMessage(t("treeVisualizationLoadFailed"));
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [url, t, fetchApi]);

  const items = useMemo(() => (nodes ? renderItems(nodes) : null), [nodes]);

  if (errorMessage) {
    return (
      <Typography variant="body2" color="error">
        {errorMessage}
      </Typography>
    );
  }

  if (!nodes) return null;
  if (nodes.length === 0) return null;

  return <SimpleTreeView>{items}</SimpleTreeView>;
};

export default TreeVisualization;
