import { FC, ReactNode } from "react";
import { Table, TableBody, TableCell, TableHead, TableRow } from "@mui/material";

/** The width of the first column of every detail table, so the tables of a step line up. */
export const nameColumnWidth = "25%";

interface DetailTableProps {
  /** The headings with their widths; a column without a width takes the remaining space. */
  columns: { label: string; width?: string }[];
  /** The rows of the table. */
  children: ReactNode;
}

/** A small table inside a step, with fixed column widths so long values wrap instead of widening a column. */
export const DetailTable: FC<DetailTableProps> = ({ columns, children }) => (
  <Table
    size="small"
    sx={{
      tableLayout: "fixed",
      "& td": { overflowWrap: "anywhere" },
      "& th, & td": { paddingLeft: 0, textAlign: "left", verticalAlign: "top" },
    }}>
    <TableHead>
      <TableRow>
        {columns.map(column => (
          <TableCell key={column.label} sx={{ width: column.width }}>
            {column.label}
          </TableCell>
        ))}
      </TableRow>
    </TableHead>
    <TableBody>{children}</TableBody>
  </Table>
);
