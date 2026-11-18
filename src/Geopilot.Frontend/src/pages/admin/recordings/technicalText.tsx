import { FC, ReactNode } from "react";
import { Typography } from "@mui/material";

interface TechnicalTextProps {
  children: ReactNode;
}

/** Text taken from code, configuration or files, such as a type name, an expression or a hash, set in monospace. */
export const TechnicalText: FC<TechnicalTextProps> = ({ children }) => (
  <Typography component="code" variant="inherit" sx={{ fontFamily: "monospace" }}>
    {children}
  </Typography>
);
