import { FC, ReactNode } from "react";
import { Table, TableBody, TableCell, TableHead, TableRow } from "@mui/material";
import { px2rem } from "../appTheme.ts";

/** The width of the first column of every detail table, so the tables of a step line up. */
export const nameColumnWidth = "25%";

interface DetailTableProps {
  /** The headings with their widths; a column without a width takes the remaining space. */
  columns: { label: string; width?: string }[];
  dataCy?: string;
  /** The rows of the table. */
  children: ReactNode;
}

/** A small table inside a box, with fixed column widths so long values wrap instead of widening a column. */
export const DetailTable: FC<DetailTableProps> = ({ columns, dataCy, children }) => (
  <Table
    size="small"
    data-cy={dataCy}
    sx={{
      tableLayout: "fixed",
      "& td": { overflowWrap: "anywhere" },
      "& th, & td": { paddingLeft: 0, textAlign: "left", verticalAlign: "top", fontSize: px2rem(16) },
      "& thead th": { paddingTop: 0 },
      // The same hover as the rows of a data grid.
      "& tbody tr:hover": { backgroundColor: "action.hover" },
    }}>
    <TableHead>
      <TableRow>
        {columns.map((column, index) => (
          <TableCell key={index} sx={{ width: column.width }}>
            {column.label}
          </TableCell>
        ))}
      </TableRow>
    </TableHead>
    <TableBody>{children}</TableBody>
  </Table>
);
