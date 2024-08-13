import { Box } from "@mui/material";
import { styled } from "@mui/system";

export const AppBox = styled(Box)({
  display: "flex",
  flexDirection: "column",
  height: "100vh",
});

export const LayoutBox = styled(Box)({
  marginTop: "60px",
  flex: "1 1 100%",
  display: "flex",
  flexDirection: "column",
  minHeight: "calc(100vh - 60px)",
});

export const PageContentBox = styled(Box)({
  padding: "20px",
  flex: "1 1 100%",
  display: "flex",
  flexDirection: "column",
});
