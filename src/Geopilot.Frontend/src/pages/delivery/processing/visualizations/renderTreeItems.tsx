import { ComponentType, ReactNode } from "react";
import ErrorOutlineIcon from "@mui/icons-material/ErrorOutline";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import { Box, Stack, SvgIconProps, Typography } from "@mui/material";
import { TreeItem } from "@mui/x-tree-view";
import { nodeId, TreeNode } from "./treeNode";

const SEVERITY_ICON: Record<string, ComponentType<SvgIconProps>> = {
  error: ErrorOutlineIcon,
  warning: WarningAmberIcon,
};

const renderIcon = (node: TreeNode): ReactNode => {
  const SvgIcon = node.color ? SEVERITY_ICON[node.color] : undefined;
  if (!SvgIcon) return null;
  return <SvgIcon fontSize="small" color={node.color === "warning" ? "warning" : "error"} />;
};

const renderLabel = (node: TreeNode): ReactNode => (
  <Stack direction="row" sx={{ alignItems: "flex-start", gap: 0.5, minWidth: 0 }}>
    {renderIcon(node)}
    <Typography variant="body2" sx={{ wordBreak: "break-word", minWidth: 0 }}>
      {node.message}
    </Typography>
    {node.count > 0 && (
      <Typography variant="body2" color="text.secondary" ml={1} sx={{ flexShrink: 0, whiteSpace: "nowrap" }}>
        ( {node.count} )
      </Typography>
    )}
  </Stack>
);

interface RenderTreeOptions {
  selectedId?: string | null;
  inlinePanel?: ReactNode;
}

export const renderTreeItems = (nodes: TreeNode[], prefix = "n", options?: RenderTreeOptions): ReactNode =>
  nodes.flatMap((node, index) => {
    const id = nodeId(prefix, index);
    const hasChildren = node.values && node.values.length > 0;
    const item = (
      <TreeItem key={id} itemId={id} label={renderLabel(node)}>
        {hasChildren ? renderTreeItems(node.values!, id, options) : null}
      </TreeItem>
    );

    if (options?.inlinePanel && options.selectedId === id) {
      return [
        item,
        <Box key={`${id}-panel`} sx={{ pl: 4, py: 1 }}>
          {options.inlinePanel}
        </Box>,
      ];
    }

    return item;
  });
