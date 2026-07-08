import { Stack } from "@mui/material";
import { styled } from "@mui/system";

export const FullPageStack = styled(Stack)({
  height: "100vh", // fallback for older browsers
  "@supports (height: 100dvh)": {
    height: "100dvh", // preferred for modern browsers
  },
});

export const ScrollableContent = styled(Stack)(({ theme }) => ({
  backgroundColor: theme.palette.background.base,
  paddingTop: "60px",
  flex: "1",
}));

export const pageContentPadding = "40px";

export const PageContent = styled(Stack)({
  height: "100%",
  padding: pageContentPadding,
  flex: "1",
  alignItems: "center",
});

export const CenteredContent = styled(Stack)({
  width: "100%",
  maxWidth: "1200px",
});

export const GeopilotBox = styled(Stack)(({ theme }) => ({
  backgroundColor: theme.palette.background.content,
  border: `1px solid ${theme.palette.primary.light}`,
  borderRadius: theme.spacing(0.5),
  padding: theme.spacing(2),
}));
