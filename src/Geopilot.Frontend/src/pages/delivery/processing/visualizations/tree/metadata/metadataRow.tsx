import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import CheckIcon from "@mui/icons-material/Check";
import ContentCopyIcon from "@mui/icons-material/ContentCopy";
import { IconButton, TableCell, TableRow, Tooltip, Typography } from "@mui/material";

interface MetadataRowProps {
  label: string;
  value: string;
}

export const MetadataRow = ({ label, value }: MetadataRowProps) => {
  const { t } = useTranslation();
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
    <TableRow sx={{ "&:last-child td": { border: 0 } }}>
      <TableCell sx={{ width: "35%", verticalAlign: "top", color: "text.secondary", px: 0 }}>
        <Typography variant="body2">{label}</Typography>
      </TableCell>
      <TableCell sx={{ verticalAlign: "top", wordBreak: "break-word", px: 1 }}>
        <Typography variant="body2">{value}</Typography>
      </TableCell>
      <TableCell sx={{ width: 40, verticalAlign: "top", px: 0, textAlign: "right" }}>
        <Tooltip title={copied ? t("copied") : t("copy")}>
          <IconButton
            size="small"
            color="primary"
            onClick={copyValue}
            data-cy="metadata-copy-button"
            sx={{ mt: "-5px" }}>
            {copied ? <CheckIcon fontSize="small" color="success" /> : <ContentCopyIcon fontSize="small" />}
          </IconButton>
        </Tooltip>
      </TableCell>
    </TableRow>
  );
};
