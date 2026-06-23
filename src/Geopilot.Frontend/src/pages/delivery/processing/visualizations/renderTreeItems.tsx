import { ReactNode } from "react";
import { Box, Icon, Typography } from "@mui/material";
import { TreeItem } from "@mui/x-tree-view";
import { nodeId, TreeNode } from "./treeNode";

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

export const renderTreeItems = (nodes: TreeNode[], prefix = "n"): ReactNode =>
  nodes.map((node, index) => {
    const id = nodeId(prefix, index);
    const hasChildren = node.values && node.values.length > 0;
    return (
      <TreeItem key={id} itemId={id} label={renderLabel(node)}>
        {hasChildren ? renderTreeItems(node.values!, id) : null}
      </TreeItem>
    );
  });
