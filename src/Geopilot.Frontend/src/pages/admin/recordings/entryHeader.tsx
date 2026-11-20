import { FC, ReactNode } from "react";
import { Box, Stack, Typography } from "@mui/material";

interface EntryHeaderProps {
  /** The icon of its state in a state icon frame. */
  icon: ReactNode;
  /** What the entry is, such as the name of a step. */
  title: ReactNode;
  /** Right aligned at the end, such as how it went and how long it took. */
  aside?: string;
}

/** The header of an entry in a list of a recording, the same for the preflight, a step, an event and the pipeline. */
export const EntryHeader: FC<EntryHeaderProps> = ({ icon, title, aside }) => (
  <Stack direction="row" sx={{ alignItems: "center", gap: 2, flexWrap: "wrap", width: "100%", paddingRight: 2 }}>
    {icon}
    <Box sx={{ flex: "1 1 0" }}>{title}</Box>
    {aside && (
      <Typography variant="body2" sx={{ color: "text.secondary", textAlign: "right" }}>
        {aside}
      </Typography>
    )}
  </Stack>
);
