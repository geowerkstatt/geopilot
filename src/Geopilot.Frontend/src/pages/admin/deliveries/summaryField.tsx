import { FC, ReactElement, ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { Stack, Typography } from "@mui/material";
import { TranslationKey } from "../../../appInterfaces";

interface SummaryFieldProps {
  /** A translation key, or an element for a label that comes from the server. */
  label: TranslationKey | ReactElement;
  /** The data-cy value; without it the label (a translation key) names the field. */
  dataCy?: string;
  /** Spans both columns, for a text that may run long. */
  wide?: boolean;
  children: ReactNode;
}

/** A labelled value in the summary of a delivery. */
export const SummaryField: FC<SummaryFieldProps> = ({ label, dataCy, wide, children }) => {
  const { t } = useTranslation();

  return (
    <Stack
      spacing={0.5}
      sx={{ gridColumn: wide ? "1 / -1" : undefined, minWidth: 0 }}
      data-cy={dataCy ?? `summary-${label}`}>
      <Typography variant="caption" sx={{ color: "text.secondary" }}>
        {typeof label === "string" ? t(label) : label}
      </Typography>
      <Typography variant="body1" component="div" sx={{ overflowWrap: "anywhere" }}>
        {children}
      </Typography>
    </Stack>
  );
};
