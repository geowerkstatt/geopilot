import { useEffect, useState } from "react";
import CheckIcon from "@mui/icons-material/Check";
import ContentCopyIcon from "@mui/icons-material/ContentCopy";
import { TableCell, TableRow, Typography } from "@mui/material";
import { IconButton } from "../../../../components/buttons";

interface MetadataRowProps {
  label: string;
  value: string;
}

export const MetadataRow = ({ label, value }: MetadataRowProps) => {
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
    <TableRow
      sx={{
        "&:last-child td": { border: 0 },
        "&:hover .metadata-copy-button": { opacity: 1 },
      }}>
      <TableCell sx={{ width: "35%", verticalAlign: "top", color: "text.secondary", px: 0 }}>
        <Typography variant="body2">{label}</Typography>
      </TableCell>
      <TableCell sx={{ verticalAlign: "top", wordBreak: "break-word", px: 1 }}>
        <Typography variant="body2">{value}</Typography>
      </TableCell>
      <TableCell sx={{ width: 40, verticalAlign: "top", px: 0, textAlign: "right" }}>
        <IconButton
          size="small"
          icon={copied ? <CheckIcon /> : <ContentCopyIcon />}
          label={copied ? "copied" : "copy"}
          onClick={copyValue}
          data-cy="metadata-copy-button"
          className="metadata-copy-button"
          sx={{
            mt: "-5px",
            opacity: copied ? 1 : 0,
            transition: "opacity 0.15s",
            "&:focus-visible": { opacity: 1 },
          }}
        />
      </TableCell>
    </TableRow>
  );
};
