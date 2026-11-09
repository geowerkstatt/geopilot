import { FC, ReactElement, ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { Stack, Typography } from "@mui/material";
import { TranslationKey } from "../../../appInterfaces";

/** A caption above a value, as in a summary, or a heading above a section, as inside a step. */
export type FieldLabelVariant = "caption" | "h5";

/** The label of a field or a section in the variant it asks for. */
export const FieldLabel: FC<{ variant: FieldLabelVariant; children: ReactNode }> = ({ variant, children }) => (
  <Typography variant={variant} sx={variant === "caption" ? { color: "text.secondary" } : { margin: 0 }}>
    {children}
  </Typography>
);

interface SummaryFieldProps {
  /** A translation key, or an element for a label that comes from the server. */
  label: TranslationKey | ReactElement;
  labelVariant?: FieldLabelVariant;
  /** The data-cy value; without it the label (a translation key) names the field. */
  dataCy?: string;
  /** Spans both columns, for a text that may run long. */
  wide?: boolean;
  children: ReactNode;
}

/** A labelled value in the summary of a delivery. */
export const SummaryField: FC<SummaryFieldProps> = ({ label, labelVariant = "caption", dataCy, wide, children }) => {
  const { t } = useTranslation();

  return (
    <Stack
      spacing={labelVariant === "h5" ? 1 : 0.5}
      sx={{ gridColumn: wide ? "1 / -1" : undefined, minWidth: 0 }}
      data-cy={dataCy ?? `summary-${label}`}>
      <FieldLabel variant={labelVariant}>{typeof label === "string" ? t(label) : label}</FieldLabel>
      <Typography variant="body1" component="div" sx={{ overflowWrap: "anywhere" }}>
        {children}
      </Typography>
    </Stack>
  );
};
