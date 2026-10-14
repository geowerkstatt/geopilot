import { FC, useEffect, useState } from "react";
import CheckIcon from "@mui/icons-material/Check";
import ContentCopyIcon from "@mui/icons-material/ContentCopy";
import { IconButton } from "./buttons";

/** The class of the button, for the parent that reveals it on hover, e.g. `[`&:hover .${COPY_BUTTON_CLASS}`]: { opacity: 1 }`. */
export const COPY_BUTTON_CLASS = "detail-copy-button";

interface CopyButtonProps {
  /** The text the button copies to the clipboard. */
  value: string;
}

/**
 * Copies a value to the clipboard and confirms it for a moment. It stays invisible until its parent reveals it on
 * hover (see {@link COPY_BUTTON_CLASS}) or it has the keyboard focus.
 */
export const CopyButton: FC<CopyButtonProps> = ({ value }) => {
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
    <IconButton
      size="small"
      icon={copied ? <CheckIcon /> : <ContentCopyIcon />}
      label={copied ? "copied" : "copy"}
      onClick={copyValue}
      className={COPY_BUTTON_CLASS}
      sx={{
        mt: "-5px",
        opacity: copied ? 1 : 0,
        transition: "opacity 0.15s",
        "&:focus-visible": { opacity: 1 },
      }}
    />
  );
};
