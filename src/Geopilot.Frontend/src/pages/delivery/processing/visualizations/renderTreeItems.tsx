import { ComponentType, ReactNode } from "react";
import ErrorOutlineIcon from "@mui/icons-material/ErrorOutline";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import { Box, Icon, Stack, SvgIconProps, Typography } from "@mui/material";
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

// Crisp SVG icons for the ligatures the backend emits. The Material Icons webfont is served from
// fonts.gstatic.com, which the Content-Security-Policy blocks (no font-src, so it falls back to
// default-src 'self'); an <Icon> font ligature would then render as its literal text. Bundled SVG
// components need no webfont.
const SVG_ICONS: Record<string, ComponentType<SvgIconProps>> = {
  error_outline: ErrorOutlineIcon,
  warning_amber: WarningAmberIcon,
};

const isMuiColor = (value: string): value is IconColor => (MUI_ICON_COLORS as string[]).includes(value);

const iconColorProps = (node: TreeNode): { color?: IconColor; sx?: { color: string } } =>
  node.color && !isMuiColor(node.color)
    ? { sx: { color: node.color } }
    : { color: (node.color as IconColor) ?? "inherit" };

const renderIcon = (node: TreeNode): ReactNode => {
  if (!node.icon) return null;
  const colorProps = iconColorProps(node);
  const SvgIcon = SVG_ICONS[node.icon];
  if (SvgIcon) {
    return <SvgIcon fontSize="small" {...colorProps} />;
  }
  // Fall back to the font ligature for icon names without a mapped SVG.
  return (
    <Icon fontSize="small" {...colorProps}>
      {node.icon}
    </Icon>
  );
};

const renderLabel = (node: TreeNode): ReactNode => (
  <Stack direction="row" sx={{ alignItems: "flex-start", gap: 0.5, py: 0.25, minWidth: 0 }}>
    {renderIcon(node)}
    <Typography variant="body2" sx={{ wordBreak: "break-word", minWidth: 0 }}>
      {node.message}
    </Typography>
    {node.count > 0 && (
      <Typography variant="body2" color="text.secondary">
        ({node.count})
      </Typography>
    )}
  </Stack>
);

interface RenderTreeOptions {
  // When set, the panel is rendered directly below the matching item, indented to its level.
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
