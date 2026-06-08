import { ReactNode, useEffect, useMemo, useState } from "react";
import { Box, Typography } from "@mui/material";
import { SimpleTreeView, TreeItem } from "@mui/x-tree-view";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutline";
import ErrorOutlineIcon from "@mui/icons-material/ErrorOutline";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import { useTranslation } from "react-i18next";

type TreeEntryType = "Info" | "Warning" | "Error";

interface TreeEntry {
  message: string;
  type?: TreeEntryType;
  values?: TreeEntry[];
}

const getTypeIcon = (type?: TreeEntryType) => {
  switch (type) {
    case "Error":
      return <ErrorOutlineIcon fontSize="inherit" color="error" sx={{ fontSize: 18 }} />;
    case "Warning":
      return <WarningAmberIcon fontSize="inherit" color="warning" sx={{ fontSize: 18 }} />;
    case "Info":
      return <CheckCircleOutlineIcon fontSize="inherit" color="success" sx={{ fontSize: 18 }} />;
    default:
      return null;
  }
};

const renderLabel = (entry: TreeEntry): ReactNode => (
  <Box sx={{ display: "flex", alignItems: "center", gap: 0.5, py: 0.25 }}>
    {getTypeIcon(entry.type)}
    <Typography variant="body2">{entry.message}</Typography>
  </Box>
);

const renderItems = (entries: TreeEntry[], prefix = "n"): ReactNode =>
  entries.map((entry, index) => {
    const id = `${prefix}-${index}`;
    const hasChildren = entry.values && entry.values.length > 0;
    return (
      <TreeItem key={id} itemId={id} label={renderLabel(entry)}>
        {hasChildren ? renderItems(entry.values!, id) : null}
      </TreeItem>
    );
  });

interface TreeVisualizationProps {
  url: string;
}

export const TreeVisualization = ({ url }: TreeVisualizationProps) => {
  const { t } = useTranslation();
  const [entries, setEntries] = useState<TreeEntry[] | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setEntries(null);
    setErrorMessage(null);

    (async () => {
      try {
        const response = await fetch(url);
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        const data = (await response.json()) as TreeEntry[];
        if (!cancelled) setEntries(data);
      } catch {
        if (!cancelled) setErrorMessage(t("treeVisualizationLoadFailed"));
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [url, t]);

  const items = useMemo(() => (entries ? renderItems(entries) : null), [entries]);

  if (errorMessage) {
    return (
      <Typography variant="body2" color="error">
        {errorMessage}
      </Typography>
    );
  }

  if (!entries) return null;
  if (entries.length === 0) return null;

  return <SimpleTreeView>{items}</SimpleTreeView>;
};

export default TreeVisualization;
