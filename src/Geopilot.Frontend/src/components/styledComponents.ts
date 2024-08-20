import { Box } from "@mui/material";
import { styled } from "@mui/system";

export const FlexRowBox = styled(Box)({
  display: "flex",
  flexDirection: "row",
});

export const FlexColumnBox = styled(Box)({
  display: "flex",
  flexDirection: "column",
});

export const AppBox = styled(FlexColumnBox)({
  height: "100vh",
});

export const LayoutBox = styled(FlexColumnBox)({
  marginTop: "60px",
  flex: "1 1 100%",
  minHeight: "calc(100vh - 60px)",
});

export const PageContentBox = styled(FlexColumnBox)({
  padding: "20px",
  flex: "1 1 100%",
  alignItems: "center",
});

export const CenteredBox = styled(FlexColumnBox)({
  margin: "40px 0",
  width: "100%",
  maxWidth: "1000px",
});
