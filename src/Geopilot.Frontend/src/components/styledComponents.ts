import { Stack } from "@mui/material";
import { styled } from "@mui/system";

export const AppBox = styled(Stack)({
  height: "100vh",
});

export const LayoutBox = styled(Stack)(({ theme }) => ({
  backgroundColor: theme.palette.background.base,
  paddingTop: "60px",
  flex: "1",
}));

export const pageContentPadding = "40px";

export const PageContentBox = styled(Stack)({
  height: "100%",
  padding: pageContentPadding,
  flex: "1",
  alignItems: "center",
});

export const CenteredBox = styled(Stack)({
  width: "100%",
  maxWidth: "1200px",
});

export const GeopilotBox = styled(Stack)(({ theme }) => ({
  backgroundColor: "white",
  border: `1px solid ${theme.palette.primary.light}`,
  borderRadius: "4px",
  padding: "16px",
}));
