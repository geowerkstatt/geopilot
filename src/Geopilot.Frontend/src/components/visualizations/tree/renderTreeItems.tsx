import { ComponentType, ReactNode } from "react";
import ErrorOutlineIcon from "@mui/icons-material/ErrorOutlineOutlined";
import MapOutlinedIcon from "@mui/icons-material/MapOutlined";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import { Box, Stack, SvgIconProps, Typography } from "@mui/material";
import { TreeItem } from "@mui/x-tree-view";
import { IconButton } from "../../../components/buttons";
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

const renderLabel = (node: TreeNode, action?: ReactNode): ReactNode => (
  <Stack direction="row" sx={{ alignItems: "center", gap: 0.5, minWidth: 0 }}>
    {renderIcon(node)}
    <Typography variant="body2" sx={{ wordBreak: "break-word", minWidth: 0 }}>
      {node.message}
    </Typography>
    {node.count > 0 && (
      <Typography
        variant="body2"
        sx={{
          color: "text.secondary",
          ml: 1,
          flexShrink: 0,
          whiteSpace: "nowrap",
        }}>
        ( {node.count} )
      </Typography>
    )}
    {action}
  </Stack>
);

interface RenderTreeOptions {
  selectedId?: string | null;
  inlinePanel?: ReactNode;
  /** Zooms the map to a node's errors; wired only when a map is present. */
  onZoom?: (nodeId: string) => void;
  /** Structural ids of zoomable nodes; the zoom control is rendered only for these. */
  zoomableNodeIds?: ReadonlySet<string>;
}

export const renderTreeItems = (nodes: TreeNode[], prefix = "n", options?: RenderTreeOptions, depth = 0): ReactNode =>
  nodes.flatMap((node, index) => {
    const id = nodeId(prefix, index);
    const hasChildren = node.values && node.values.length > 0;
    const onZoom = options?.onZoom;
    const zoomButton =
      onZoom && options?.zoomableNodeIds?.has(id) ? (
        <IconButton
          className="tree-zoom-button"
          size="small"
          color="primary"
          label="zoomToError"
          tooltipPlacement="left"
          icon={<MapOutlinedIcon />}
          sx={{ ml: "auto", flexShrink: 0 }}
          onMouseDown={event => event.stopPropagation()}
          onClick={event => {
            event.stopPropagation();
            onZoom(id);
          }}
        />
      ) : null;
    const item = (
      <TreeItem key={id} itemId={id} label={renderLabel(node, zoomButton)}>
        {hasChildren ? renderTreeItems(node.values!, id, options, depth + 1) : null}
      </TreeItem>
    );

    if (options?.inlinePanel && options.selectedId === id) {
      return [
        item,
        <Box
          key={`${id}-panel`}
          sx={{
            pl: theme =>
              `calc(${theme.spacing(4)} + var(--TreeView-itemChildrenIndentation, ${theme.spacing(1.5)}) * ${depth})`,
            py: 1,
          }}>
          {options.inlinePanel}
        </Box>,
      ];
    }

    return item;
  });
