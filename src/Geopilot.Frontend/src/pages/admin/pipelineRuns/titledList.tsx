import { FC, ReactNode } from "react";
import { Box, Stack, Typography } from "@mui/material";

interface TitledListProps {
  title: string;
  /** The entries, each a list item. */
  children: ReactNode;
}

/** One part of a step, such as its inputs, its conditions of one kind or its downloads, as a titled list. */
export const TitledList: FC<TitledListProps> = ({ title, children }) => (
  <Stack spacing={0.5}>
    <Typography variant="caption" sx={{ color: "text.secondary" }}>
      {title}
    </Typography>
    <Box component="ul" sx={{ margin: 0, paddingLeft: 3 }}>
      {children}
    </Box>
  </Stack>
);
