import { FC, ReactNode } from "react";
import { Table, TableBody, TableCell, TableContainer, TableHead, TableRow } from "@mui/material";
import { px2rem } from "../appTheme.ts";

/** The width of the first column of every detail table, so the tables of a step line up. */
export const nameColumnWidth = "25%";

/** Below this width the columns get too narrow to read, so the table scrolls sideways instead. */
const minTableWidth = px2rem(600);

interface DetailTableProps {
  /** The headings with their widths; a column without a width takes the remaining space. */
  columns: { label: string; width?: string }[];
  dataCy?: string;
  /** The rows of the table. */
  children: ReactNode;
}

/**
 * A small table inside a box, with fixed column widths so long values wrap instead of widening a column. On a narrow
 * screen it scrolls sideways.
 */
export const DetailTable: FC<DetailTableProps> = ({ columns, dataCy, children }) => (
  <TableContainer>
    <Table
      size="small"
      data-cy={dataCy}
      sx={{
        tableLayout: "fixed",
        minWidth: minTableWidth,
        "& td": { overflowWrap: "anywhere" },
        "& th, & td": { paddingLeft: 1, textAlign: "left", verticalAlign: "top", fontSize: px2rem(16) },
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
  </TableContainer>
);
