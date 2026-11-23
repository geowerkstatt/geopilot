import { TableCell, TableRow, Typography } from "@mui/material";
import { CopyButton } from "../../copyButton.tsx";

interface DetailRowProps {
  /** The translated field label. */
  label: string;
  value: string;
}

export const DetailRow = ({ label, value }: DetailRowProps) => (
  <TableRow
    sx={{
      "&:last-child td": { border: 0 },
      "&:hover .detail-copy-button": { opacity: 1 },
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
      <CopyButton
        text={value}
        size="small"
        className="detail-copy-button"
        sx={{
          mt: "-5px",
          opacity: 0,
          transition: "opacity 0.15s",
          "&:focus-visible, &[data-copied='true']": { opacity: 1 },
        }}
      />
    </TableCell>
  </TableRow>
);
