import { FC, ReactNode } from "react";
import { Box, Stack } from "@mui/material";
import { FieldLabel, FieldLabelVariant } from "../deliveries/summaryField.tsx";

interface TitledListProps {
  title: string;
  titleVariant?: FieldLabelVariant;
  /** The entries, each a list item. */
  children: ReactNode;
}

/** One part of a step, such as its inputs, its conditions of one kind or its downloads, as a titled list. */
export const TitledList: FC<TitledListProps> = ({ title, titleVariant = "caption", children }) => (
  <Stack spacing={0.5}>
    <FieldLabel variant={titleVariant}>{title}</FieldLabel>
    <Box component="ul" sx={{ margin: 0, paddingLeft: 3 }}>
      {children}
    </Box>
  </Stack>
);
