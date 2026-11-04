import { FC, ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { Stack, Typography } from "@mui/material";
import { TranslationKey } from "../../../appInterfaces";

interface SummaryFieldProps {
  label: TranslationKey;
  /** Spans both columns, for a text that may run long. */
  wide?: boolean;
  children: ReactNode;
}

/** A labelled value in the summary of a delivery. */
export const SummaryField: FC<SummaryFieldProps> = ({ label, wide, children }) => {
  const { t } = useTranslation();

  return (
    <Stack spacing={0.5} sx={{ gridColumn: wide ? "1 / -1" : undefined, minWidth: 0 }} data-cy={`summary-${label}`}>
      <Typography variant="caption" sx={{ color: "text.secondary" }}>
        {t(label)}
      </Typography>
      <Typography variant="body1" component="div" sx={{ overflowWrap: "anywhere" }}>
        {children}
      </Typography>
    </Stack>
  );
};
