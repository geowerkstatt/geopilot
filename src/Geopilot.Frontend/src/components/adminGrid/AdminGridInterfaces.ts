import { GridColDef } from "../dataGrid/DataGridMultiSelectColumn.tsx";

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
