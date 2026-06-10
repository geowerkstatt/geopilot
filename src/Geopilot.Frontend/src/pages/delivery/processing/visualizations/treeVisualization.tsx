import { ReactNode, SyntheticEvent, useEffect, useMemo, useState } from "react";
import { Box, Icon, IconButton, Table, TableBody, TableCell, TableRow, Tooltip, Typography } from "@mui/material";
import CheckIcon from "@mui/icons-material/Check";
import ContentCopyIcon from "@mui/icons-material/ContentCopy";
import { SimpleTreeView, TreeItem } from "@mui/x-tree-view";
import { useTranslation } from "react-i18next";
import { GeopilotBox } from "../../../../components/styledComponents";
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
  metadata?: Record<string, string>;
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

const nodeId = (prefix: string, index: number): string => `${prefix}-${index}`;

const renderItems = (nodes: TreeNode[], prefix = "n"): ReactNode =>
  nodes.map((node, index) => {
    const id = nodeId(prefix, index);
    const hasChildren = node.values && node.values.length > 0;
    return (
      <TreeItem key={id} itemId={id} label={renderLabel(node)}>
        {hasChildren ? renderItems(node.values!, id) : null}
      </TreeItem>
    );
  });

const indexNodes = (nodes: TreeNode[], target: Map<string, TreeNode>, prefix = "n"): void => {
  nodes.forEach((node, index) => {
    const id = nodeId(prefix, index);
    target.set(id, node);
    if (node.values && node.values.length > 0) {
      indexNodes(node.values, target, id);
    }
  });
};

interface MetadataRowProps {
  label: string;
  value: string;
}

const MetadataRow = ({ label, value }: MetadataRowProps) => {
  const { t } = useTranslation();
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    if (!copied) return;
    const timeout = window.setTimeout(() => setCopied(false), 1500);
    return () => window.clearTimeout(timeout);
  }, [copied]);

  const copyValue = async () => {
    try {
      await navigator.clipboard.writeText(value);
      setCopied(true);
    } catch {
      setCopied(false);
    }
  };

  return (
    <TableRow sx={{ "&:last-child td": { border: 0 } }}>
      <TableCell sx={{ width: "35%", verticalAlign: "top", color: "text.secondary", px: 0 }}>
        <Typography variant="body2">{label}</Typography>
      </TableCell>
      <TableCell sx={{ verticalAlign: "top", wordBreak: "break-word", px: 1 }}>
        <Typography variant="body2">{value}</Typography>
      </TableCell>
      <TableCell sx={{ width: 40, verticalAlign: "top", px: 0, textAlign: "right" }}>
        <Tooltip title={copied ? t("copied") : t("copy")}>
          <IconButton size="small" onClick={copyValue} data-cy="metadata-copy-button">
            {copied ? <CheckIcon fontSize="small" color="success" /> : <ContentCopyIcon fontSize="small" />}
          </IconButton>
        </Tooltip>
      </TableCell>
    </TableRow>
  );
};

interface MetadataPanelProps {
  node: TreeNode | null;
}

const MetadataPanel = ({ node }: MetadataPanelProps) => {
  const { t } = useTranslation();
  const entries = node?.metadata ? Object.entries(node.metadata) : [];

  return (
    <GeopilotBox sx={{ width: 380, flexShrink: 0, gap: 1 }}>
      <Typography variant="subtitle2">{t("treeVisualizationMetadataTitle")}</Typography>
      {entries.length === 0 ? (
        <Typography variant="body2" color="text.secondary">
          {t("treeVisualizationMetadataEmpty")}
        </Typography>
      ) : (
        <Table size="small" sx={{ tableLayout: "fixed" }}>
          <TableBody>
            {entries.map(([key, value]) => (
              <MetadataRow key={key} label={key} value={value} />
            ))}
          </TableBody>
        </Table>
      )}
    </GeopilotBox>
  );
};

interface TreeVisualizationProps {
  url: string;
}

export const TreeVisualization = ({ url }: TreeVisualizationProps) => {
  const { t } = useTranslation();
  const { fetchApi } = useFetch();
  const [nodes, setNodes] = useState<TreeNode[] | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setNodes(null);
    setErrorMessage(null);
    setSelectedId(null);

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

  const nodesById = useMemo(() => {
    const map = new Map<string, TreeNode>();
    if (nodes) indexNodes(nodes, map);
    return map;
  }, [nodes]);

  const selectedNode = selectedId ? (nodesById.get(selectedId) ?? null) : null;

  if (errorMessage) {
    return (
      <Typography variant="body2" color="error">
        {errorMessage}
      </Typography>
    );
  }

  if (!nodes) return null;
  if (nodes.length === 0) return null;

  return (
    <Box sx={{ display: "flex", flexWrap: "wrap", gap: 2, alignItems: "flex-start" }}>
      <SimpleTreeView
        sx={{ flex: "1 1 auto", minWidth: 0 }}
        selectedItems={selectedId}
        onSelectedItemsChange={(_: SyntheticEvent, itemId: string | null) => setSelectedId(itemId)}>
        {items}
      </SimpleTreeView>
      <MetadataPanel node={selectedNode} />
    </Box>
  );
};

export default TreeVisualization;
