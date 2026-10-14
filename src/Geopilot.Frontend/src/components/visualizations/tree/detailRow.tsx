import { TableCell, TableRow, Typography } from "@mui/material";
import { COPY_BUTTON_CLASS, CopyButton } from "../../copyButton";

interface DetailRowProps {
  /** The translated field label. */
  label: string;
  value: string;
}

export const DetailRow = ({ label, value }: DetailRowProps) => (
  <TableRow
    sx={{
      "&:last-child td": { border: 0 },
      [`&:hover .${COPY_BUTTON_CLASS}`]: { opacity: 1 },
    }}>
    <TableCell sx={{ width: "35%", verticalAlign: "top", wordBreak: "break-word", color: "text.secondary", px: 0 }}>
      <Typography variant="body2">{label}</Typography>
    </TableCell>
    <TableCell sx={{ verticalAlign: "top", wordBreak: "break-word", px: 1 }}>
      <Typography variant="body2">{value}</Typography>
    </TableCell>
    <TableCell
      sx={{
        width: 40,
        verticalAlign: "top",
        px: 0,
        textAlign: "right",
        display: "none",
        "@media (hover: hover) and (pointer: fine)": { display: "table-cell" },
      }}>
      <CopyButton value={value} />
    </TableCell>
  </TableRow>
);
