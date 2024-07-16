import { GridMultiSelectColDef } from "../dataGrid/DataGridMultiSelectColumn.tsx";
import { GridActionsColDef, GridSingleSelectColDef, GridValidRowModel } from "@mui/x-data-grid";
import { GridBaseColDef } from "@mui/x-data-grid/internals";

export interface AdminGridProps {
  addLabel?: string;
  data: DataRow[];
  columns: GridColDef[];
  onSave: (row: DataRow) => void | Promise<void>;
  onDisconnect: (row: DataRow) => void | Promise<void>;
}

export interface DataRow {
  // eslint-disable-next-line
  [key: string]: any;
}

// eslint-disable-next-line
export type GridColDef<R extends GridValidRowModel = any, V = any, F = V> =
  | GridBaseColDef<R, V, F>
  | GridActionsColDef<R, V, F>
  | GridSingleSelectColDef<R, V, F>
  | GridMultiSelectColDef<R, V, F>;
