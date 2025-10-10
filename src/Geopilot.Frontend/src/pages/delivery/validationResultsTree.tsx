import { Typography } from "@mui/material";
import { RichTreeView, TreeItem, TreeItemProps, TreeViewBaseItem, useTreeItemModel } from "@mui/x-tree-view";
import React, { useMemo } from "react";
import StarBorder from "@mui/icons-material/StarBorder";
import StarHalf from "@mui/icons-material/StarHalf";
import CheckIcon from '@mui/icons-material/Check';
import ClearIcon from '@mui/icons-material/Clear';
import { LogData } from "./logData";

type ItemType = string | undefined; // broaden to accept any "type" from log data

type TreeItemWithLabel = {
  id: string;
  label: string;
  type?: ItemType;
  children?: TreeViewBaseItem<TreeItemWithLabel>[];
};

// Build tree items from LogData
interface LogEntry {
  message: string;
  type?: string;
  values?: LogEntry[];
}

function buildItems(entries: LogEntry[], prefix = "n"): TreeViewBaseItem<TreeItemWithLabel>[] {
  return entries.map((e, i) => {
    const id = `${prefix}-${i}`;
    return {
      id,
      label: e.message,
      type: e.type,
      children: e.values ? buildItems(e.values, id) : undefined,
    };
  });
}

interface CustomLabelProps {
  children: string;
  className: string;
  type?: ItemType;
}

// Icon selection (extend / adjust as needed)
function getTypeIcon(type?: string) {
  switch (type) {
    case "Info":
      return <CheckIcon fontSize="inherit" style={{ fontSize: 16, color: "#00de00ff" }} />;
    case "Error":
      return <ClearIcon fontSize="inherit" style={{ fontSize: 16, color: "#ff2c2cff" }} />;
    default:
      return null;
  }
}

function CustomLabel({ children, className, type }: CustomLabelProps) {
  return (
    <div className={className} style={{ display: "flex", alignItems: "center", gap: 4 }}>
      {getTypeIcon(type)}
      <Typography>{children}</Typography>
    </div>
  );
}

const CustomTreeItem = React.forwardRef(function CustomTreeItem(props: TreeItemProps, ref: React.Ref<HTMLLIElement>) {
  const item = useTreeItemModel<TreeItemWithLabel>(props.itemId)!;
  return (
    <TreeItem
      {...props}
      ref={ref}
      slots={{ label: CustomLabel }}
      slotProps={{ label: { type: item?.type } as CustomLabelProps }}
    />
  );
});

export const ValidationResultsTree = () => {
  const items = useMemo(() => buildItems(LogData as LogEntry[]), []);
  return <RichTreeView items={items} slots={{ item: CustomTreeItem }} />;
};
