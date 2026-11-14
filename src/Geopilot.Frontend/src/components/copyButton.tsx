import { FC, useEffect, useState } from "react";
import CheckIcon from "@mui/icons-material/Check";
import ContentCopyIcon from "@mui/icons-material/ContentCopy";
import { IconButton, IconButtonProps } from "./buttons.tsx";

interface CopyButtonProps extends Omit<IconButtonProps, "icon" | "label" | "onClick"> {
  text: string;
}

/** Copies the text to the clipboard and shows a check for a moment; data-copied is "true" meanwhile. */
export const CopyButton: FC<CopyButtonProps> = ({ text, ...props }) => {
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    if (!copied) return;
    const timeout = window.setTimeout(() => setCopied(false), 1500);
    return () => window.clearTimeout(timeout);
  }, [copied]);

  const copyText = async () => {
    try {
      await navigator.clipboard.writeText(text);
      setCopied(true);
    } catch {
      setCopied(false);
    }
  };

  return (
    <IconButton
      {...props}
      icon={copied ? <CheckIcon /> : <ContentCopyIcon />}
      label={copied ? "copied" : "copy"}
      onClick={copyText}
      data-copied={copied}
    />
  );
};
